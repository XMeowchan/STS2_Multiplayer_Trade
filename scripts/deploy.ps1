param(
    [string]$GameDir,
    [string]$UpdateRepo,
    [string]$TelemetryEndpoint,
    [string[]]$TelemetryEndpoints,
    [string]$EnableAutoUpdate,
    [string]$EnableTelemetry,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")

$manifest = Get-ModManifest -ProjectRoot $projectRoot
$manifestPath = Get-ModManifestPath -ProjectRoot $projectRoot
$modId = [string]$manifest.id
if ([string]::IsNullOrWhiteSpace($modId)) {
    throw "Mod manifest is missing id."
}

$resolvedGameDir = Resolve-Sts2GameDir -RequestedPath $GameDir
$srcDir = Join-Path $projectRoot "src"
$buildOut = Join-Path $srcDir "bin\$Configuration"
$dllPath = Join-Path $buildOut "$modId.dll"
$pckPath = Join-Path $buildOut "$modId.pck"
$modDir = Join-Path (Resolve-Sts2ModsRoot -GameDir $resolvedGameDir) $modId

$buildArgs = @(
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "build-mod-artifacts.ps1"),
    "-Configuration", $Configuration,
    "-GameDir", $resolvedGameDir
)
& powershell @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw "build-mod-artifacts failed."
}

New-Item -ItemType Directory -Force -Path $modDir | Out-Null
Copy-Item $dllPath (Join-Path $modDir "$modId.dll") -Force
Copy-Item $pckPath (Join-Path $modDir "$modId.pck") -Force
Set-PckCompatibilityHeader -Path (Join-Path $modDir "$modId.pck") -EngineMinorVersion 5
Copy-Item $manifestPath (Join-Path $modDir "$modId.json") -Force
Write-EffectiveModConfig `
    -SourcePath (Join-Path $projectRoot "config.json") `
    -DestinationPath (Join-Path $modDir "config.cfg") `
    -UpdateRepo $UpdateRepo `
    -TelemetryEndpoint $TelemetryEndpoint `
    -TelemetryEndpoints $TelemetryEndpoints `
    -EnableAutoUpdate $EnableAutoUpdate `
    -EnableTelemetry $EnableTelemetry

foreach ($legacyName in @("mod_manifest.json")) {
    $legacyPath = Join-Path $modDir $legacyName
    if (Test-Path -LiteralPath $legacyPath) {
        Remove-Item -LiteralPath $legacyPath -Force
    }
}

if (Test-Path (Join-Path $modDir "_update_runtime")) {
    Remove-Item (Join-Path $modDir "_update_runtime") -Recurse -Force
}

Write-Host "Detected game dir: $resolvedGameDir"
Write-Host "Deployed $modId to $modDir"
