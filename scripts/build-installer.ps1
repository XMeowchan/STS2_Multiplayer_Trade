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
$appName = [string]$manifest.name
$appPublisher = [string]$manifest.author
if ([string]::IsNullOrWhiteSpace($modId)) { throw "Mod manifest is missing id." }
if ([string]::IsNullOrWhiteSpace($appName)) { $appName = $modId }
if ([string]::IsNullOrWhiteSpace($appPublisher)) { $appPublisher = "Unknown" }

$payloadDir = Join-Path $projectRoot "dist\installer\payload"
$issPath = Join-Path $projectRoot "installer\Sts2MultiplayerTrade.iss"

$isccCandidates = @()
$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($isccCommand) { $isccCandidates += $isccCommand.Source }
$isccCandidates += @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $isccPath) {
    throw "Inno Setup 6 not found. Install ISCC.exe, then rerun .\scripts\build-installer.ps1."
}

& $isccPath "/DAppVersion=$($manifest.version)" "/DAppName=$appName" "/DAppPublisher=$appPublisher" "/DModId=$modId" "/DPayloadDir=$payloadDir" $issPath
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed."
}

$installerPath = Join-Path $projectRoot ("dist\installer\output\{0}-Setup-{1}.exe" -f $modId, $manifest.version)
Invoke-AuthenticodeCodeSigning -Path $installerPath -Description "$appName Setup"
