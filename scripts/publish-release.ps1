param(
    [string]$Configuration = "Release",
    [string]$Repo = $env:GITHUB_REPOSITORY,
    [string]$GameDir,
    [string]$UpdateRepo,
    [string]$TelemetryEndpoint,
    [string[]]$TelemetryEndpoints,
    [string]$EnableAutoUpdate,
    [string]$EnableTelemetry,
    [string]$NotesPath,
    [switch]$Upload,
    [switch]$Prerelease,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")

function Get-Manifest {
    return Get-ModManifest -ProjectRoot $projectRoot
}

function New-ReleaseNotes {
    param(
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$PortableName,
        [Parameter(Mandatory)][string]$InstallerName,
        [Parameter(Mandatory)][string]$ModName
    )

$content = @"
# $ModName $Version

## Assets

- $InstallerName
- $PortableName

## Notes

- Adds native-style multiplayer trading for gold, potions, and relics.
- Includes GitHub portable-package auto-update support.
- Includes anonymous telemetry heartbeat support and a public usage-curve Pages payload.
"@

    Set-Content -LiteralPath $OutputPath -Value $content -Encoding UTF8
}

function Invoke-BuildArtifacts {
    param([Parameter(Mandatory)][string]$BuildConfiguration, [AllowNull()][string]$BuildGameDir)

    $commonArgs = @()
    if ($BuildGameDir) { $commonArgs += @("-GameDir", $BuildGameDir) }
    if ($PSBoundParameters.ContainsKey("UpdateRepo") -and $UpdateRepo) { $commonArgs += @("-UpdateRepo", $UpdateRepo) }
    if ($PSBoundParameters.ContainsKey("TelemetryEndpoint")) { $commonArgs += @("-TelemetryEndpoint", $TelemetryEndpoint) }
    if ($PSBoundParameters.ContainsKey("TelemetryEndpoints")) { foreach ($endpoint in $TelemetryEndpoints) { $commonArgs += @("-TelemetryEndpoints", $endpoint) } }
    if ($PSBoundParameters.ContainsKey("EnableAutoUpdate")) { $commonArgs += @("-EnableAutoUpdate", $EnableAutoUpdate) }
    if ($PSBoundParameters.ContainsKey("EnableTelemetry")) { $commonArgs += @("-EnableTelemetry", $EnableTelemetry) }

    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-mod-artifacts.ps1") -Configuration $BuildConfiguration @commonArgs
    if ($LASTEXITCODE -ne 0) { throw "build-mod-artifacts failed." }

    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-portable-package.ps1") -Configuration $BuildConfiguration -SkipBuild @commonArgs
    if ($LASTEXITCODE -ne 0) { throw "build-portable-package failed." }

    & powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "build-installer.ps1") -Configuration $BuildConfiguration -SkipBuild @commonArgs
    if ($LASTEXITCODE -ne 0) { throw "build-installer failed." }
}

$manifest = Get-Manifest
$version = [string]$manifest.version
$modId = [string]$manifest.id
$modName = [string]$manifest.name
if ([string]::IsNullOrWhiteSpace($version)) { throw "Mod manifest is missing version." }
if ([string]::IsNullOrWhiteSpace($modId)) { throw "Mod manifest is missing id." }
if ([string]::IsNullOrWhiteSpace($modName)) { $modName = $modId }

if (-not $SkipBuild) {
    Invoke-BuildArtifacts -BuildConfiguration $Configuration -BuildGameDir $GameDir
}

$releaseDir = Join-Path $projectRoot "dist\release"
$portablePath = Join-Path $releaseDir ("{0}-portable-{1}.zip" -f $modId, $version)
$installerPath = Join-Path $projectRoot ("dist\installer\output\{0}-Setup-{1}.exe" -f $modId, $version)

foreach ($artifact in @($portablePath, $installerPath)) {
    if (-not (Test-Path -LiteralPath $artifact)) {
        throw "Missing release artifact: $artifact"
    }
}

if (-not $NotesPath) {
    $NotesPath = Join-Path $releaseDir ("{0}-release-notes-{1}.md" -f $modId, $version)
    New-ReleaseNotes `
        -Version $version `
        -OutputPath $NotesPath `
        -PortableName (Split-Path $portablePath -Leaf) `
        -InstallerName (Split-Path $installerPath -Leaf) `
        -ModName $modName
}

if (-not $Upload) {
    Write-Host "Built release artifacts:"
    Write-Host " - $portablePath"
    Write-Host " - $installerPath"
    Write-Host " - $NotesPath"
    return
}

if ([string]::IsNullOrWhiteSpace($Repo)) {
    throw "Repository is required for -Upload. Pass -Repo owner/repo or set GITHUB_REPOSITORY."
}

$tagName = "v$version"
$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    throw "GitHub CLI is required for -Upload in this simplified release flow."
}

& gh release view $tagName --repo $Repo *> $null
if ($LASTEXITCODE -eq 0) {
    & gh release upload $tagName $portablePath $installerPath --clobber --repo $Repo
    if ($LASTEXITCODE -ne 0) { throw "gh release upload failed." }
    return
}

$args = @(
    "release", "create", $tagName,
    "--repo", $Repo,
    "--title", "$modName $version",
    "--notes-file", $NotesPath
)
if ($Prerelease) { $args += "--prerelease" }
$args += @($portablePath, $installerPath)

& gh @args
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed."
}
