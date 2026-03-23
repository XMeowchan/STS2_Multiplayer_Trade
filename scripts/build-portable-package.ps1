param(
    [string]$Configuration = "Release",
    [string]$GameDir,
    [string]$UpdateRepo,
    [string]$TelemetryEndpoint,
    [string[]]$TelemetryEndpoints,
    [string]$EnableAutoUpdate,
    [string]$EnableTelemetry,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")

$payloadArgs = @(
    "-ExecutionPolicy", "Bypass",
    "-File", (Join-Path $PSScriptRoot "build-installer-payload.ps1"),
    "-Configuration", $Configuration
)
if ($GameDir) { $payloadArgs += @("-GameDir", $GameDir) }
if ($PSBoundParameters.ContainsKey("UpdateRepo")) { $payloadArgs += @("-UpdateRepo", $UpdateRepo) }
if ($PSBoundParameters.ContainsKey("TelemetryEndpoint")) { $payloadArgs += @("-TelemetryEndpoint", $TelemetryEndpoint) }
if ($PSBoundParameters.ContainsKey("TelemetryEndpoints")) {
    foreach ($endpoint in $TelemetryEndpoints) { $payloadArgs += @("-TelemetryEndpoints", $endpoint) }
}
if ($PSBoundParameters.ContainsKey("EnableAutoUpdate")) { $payloadArgs += @("-EnableAutoUpdate", $EnableAutoUpdate) }
if ($PSBoundParameters.ContainsKey("EnableTelemetry")) { $payloadArgs += @("-EnableTelemetry", $EnableTelemetry) }
if ($SkipBuild) { $payloadArgs += "-SkipBuild" }

& powershell @payloadArgs
if ($LASTEXITCODE -ne 0) {
    throw "build-installer-payload failed."
}

$manifest = Get-ModManifest -ProjectRoot $projectRoot
$modId = [string]$manifest.id
if ([string]::IsNullOrWhiteSpace($modId)) { throw "Mod manifest is missing id." }

$payloadDir = Join-Path $projectRoot "dist\installer\payload\$modId"
if (-not (Test-Path -LiteralPath $payloadDir)) {
    throw "Portable payload directory not found: $payloadDir"
}

$releaseDir = Join-Path $projectRoot "dist\release"
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$zipPath = Join-Path $releaseDir ("{0}-portable-{1}.zip" -f $modId, $manifest.version)
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path $payloadDir -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Built portable package: $zipPath"
