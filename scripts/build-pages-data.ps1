param(
    [string]$OutputDir = $(Join-Path (Split-Path -Parent $PSScriptRoot) "dist\pages"),
    [string]$TelemetryStatsUrl = $(if ([string]::IsNullOrWhiteSpace($env:TELEMETRY_STATS_URL)) { "https://telemetry.example.com/v1/stats.json?days=365" } else { $env:TELEMETRY_STATS_URL })
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot "Sts2InstallHelpers.ps1")
$manifest = Get-ModManifest -ProjectRoot $projectRoot

if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

& (Join-Path $PSScriptRoot "build-usage-curve.ps1") -OutputDir $OutputDir -StatsUrl $TelemetryStatsUrl

$usageStatsPath = Join-Path $OutputDir "usage-stats.json"
$usageStats = if (Test-Path -LiteralPath $usageStatsPath) {
    Get-Content -LiteralPath $usageStatsPath -Raw | ConvertFrom-Json
} else {
    $null
}

$generatedAt = if ($null -ne $usageStats -and $usageStats.generated_at) { [string]$usageStats.generated_at } else { [DateTimeOffset]::UtcNow.ToString("o") }
$assetVersion = [Uri]::EscapeDataString($generatedAt)
$svgHref = "users-history.svg?v=$assetVersion"
$jsonHref = "usage-stats.json?v=$assetVersion"

$html = @"
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>$($manifest.name) Usage</title>
  <style>
    body { margin: 0; font-family: 'Segoe UI', 'Microsoft YaHei UI', sans-serif; background: #11161d; color: #eef2f7; }
    main { max-width: 980px; margin: 0 auto; padding: 32px 20px 56px; }
    .panel { background: rgba(25, 31, 40, 0.96); border: 1px solid rgba(120, 150, 190, 0.35); border-radius: 18px; padding: 20px; box-shadow: 0 16px 48px rgba(0,0,0,0.28); }
    h1 { margin: 0 0 12px; font-size: 32px; }
    p { color: #c7d3df; line-height: 1.7; }
    a { color: #8ec5ff; }
    img { width: 100%; display: block; border-radius: 14px; border: 1px solid rgba(120, 150, 190, 0.25); background: #0c1117; }
  </style>
</head>
<body>
  <main>
    <section class="panel">
      <h1>$($manifest.name)</h1>
      <p>公开页面仅展示匿名安装/活跃趋势。运行时不依赖此页面。</p>
      <p><a href="$jsonHref">Telemetry JSON</a></p>
      <img src="$svgHref" alt="Usage curve">
    </section>
  </main>
</body>
</html>
"@

Set-Content -LiteralPath (Join-Path $OutputDir "index.html") -Value $html -Encoding UTF8
