param(
    [string]$Configuration = "Release",
    [string]$GameDir
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")

$manifest = Get-ModManifest -ProjectRoot $projectRoot
$modId = [string]$manifest.id
if ([string]::IsNullOrWhiteSpace($modId)) {
    throw "Mod manifest is missing id."
}

$modName = [string]$manifest.name
if ([string]::IsNullOrWhiteSpace($modName)) {
    $modName = $modId
}

$srcDir = Join-Path $projectRoot "src"
$csprojMatches = @(Get-ChildItem -LiteralPath $srcDir -Filter *.csproj -File)
if ($csprojMatches.Count -ne 1) {
    throw "Expected exactly one .csproj file in src/, found $($csprojMatches.Count)."
}

$resolvedGameDir = Resolve-Sts2GameDir -RequestedPath $GameDir
$dotnet = Resolve-DotnetExecutable
$godot = Resolve-GodotExecutable
$buildOut = Join-Path $srcDir "bin\$Configuration"
$dllPath = Join-Path $buildOut "$modId.dll"
$pckPath = Join-Path $buildOut "$modId.pck"

New-Item -ItemType Directory -Force -Path $buildOut | Out-Null

& $dotnet build $csprojMatches[0].FullName -c $Configuration -p:GameDir="$resolvedGameDir" -p:ModAssemblyName="$modId"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed."
}

Update-GodotAssetImports -GodotExecutable $godot -ProjectRoot $projectRoot

& $godot --headless --path $projectRoot --script (Join-Path $projectRoot "scripts\build_pck.gd") -- $pckPath
if ($LASTEXITCODE -ne 0) {
    throw "Godot pck build failed."
}

Set-PckCompatibilityHeader -Path $pckPath -EngineMinorVersion 5
$pckHeader = Assert-PckCompatibilityHeader -Path $pckPath -ExpectedMajor 4 -MaxMinor 5

foreach ($artifactPath in @($dllPath, $pckPath)) {
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Missing build artifact: $artifactPath"
    }
}

Invoke-AuthenticodeCodeSigning -Path $dllPath -Description $modName

Write-Host "Detected game dir: $resolvedGameDir"
Write-Host "Built DLL: $dllPath"
Write-Host ("Verified PCK compatibility header: Godot {0}.{1}" -f $pckHeader.Major, $pckHeader.Minor)
Write-Host "Built PCK: $pckPath"
