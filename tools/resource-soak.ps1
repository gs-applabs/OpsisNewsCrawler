param(
    [ValidateRange(1,240)][int]$Minutes = 3,
    [ValidateSet('Low','Balanced','High')][string]$Tier = 'Low',
    [ValidateSet('Active','Idle')][string]$Scenario = 'Active',
    [Parameter(Mandatory=$true)][string]$Out
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Use this probe on an isolated GitHub Windows runner only.' }
$exe = Get-ChildItem src/OpsisNewsCrawler.App/bin/Release -Filter OpsisNewsCrawler.exe -Recurse | Select-Object -First 1
if (!$exe) { throw 'Build the application first.' }
New-Item -ItemType Directory -Force $Out | Out-Null
$root = Join-Path $env:RUNNER_TEMP ('crawler-soak-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $root | Out-Null
$cfg = Join-Path $root 'config.json'
$stem = if ($Scenario -eq 'Idle') { "$Tier-idle" } else { $Tier }
$csv = Join-Path $Out "$stem.csv"
$settings = @{
    Language='en'; Position='TaskbarIntegrated'; WelcomeDisabled=$true
    CheckForUpdates=$false; HistoryFromYahoo=$false; ArchiveNews=$false
    NewsRefreshSeconds=30; QuoteRefreshSeconds=60; PauseOnOtherWindow=$false
    BarsEnabled=$true; ShowIcons=$true; BreakingEnabled=$true; WeatherAlertsEnabled=$false
    MainContent='News'; MainSlotA='news'; MainSlotB='stocks'
}
if ($Scenario -eq 'Idle') {
    $settings.BarsEnabled = $false
    $settings.SecondBarEnabled = $false
    $settings.MiniWeatherEnabled = $false
    $settings.WeatherInIcon = $false
    $settings.HazardInIcon = $false
    $settings.BreakingEnabled = $false
    $settings.PriceAlerts = @()
}
$settings | ConvertTo-Json | Set-Content -LiteralPath $cfg -Encoding utf8
$env:OPSISNEWSCRAWLER_DATA = $root
$env:OPSISNEWSCRAWLER_FORCE_TIER = $Tier
$seconds = $Minutes * 60
$p = Start-Process -FilePath $exe.FullName -ArgumentList '--config',"`"$cfg`"",'--resource-soak',"`"$csv`"","$seconds" -PassThru
try {
    if (!$p.WaitForExit(($seconds + 90) * 1000)) { $p.Kill(); throw 'Resource probe timed out' }
    if ($p.ExitCode -ne 0) { throw "Resource probe exited $($p.ExitCode)" }
    $rows = @(Import-Csv -LiteralPath $csv)
    if ($rows.Count -lt [math]::Floor($seconds / 5 * .7)) { throw 'Insufficient resource samples' }
    $last = $rows[-1]
    if ([double]$last.elapsed_seconds -lt $seconds) { throw 'Probe stopped early' }
    $steady = @($rows | Where-Object { [double]$_.elapsed_seconds -ge [Math]::Min(60, $seconds / 2) })
    if ($steady.Count -lt 2) { throw 'No steady-state samples' }
    $first = $steady[0]
    $privateDelta = ([long]$last.private_bytes - [long]$first.private_bytes) / 1MB
    $gdiDelta = [int]$last.gdi_handles - [int]$first.gdi_handles
    $cpuMean = ($steady | ForEach-Object { [double]$_.cpu_percent_all_cores } | Measure-Object -Average).Average
    $writeMiB = ([long]$last.file_payload_bytes - [long]$first.file_payload_bytes) / 1MB
    $summary = @(
        "Tier: $Tier; scenario: $Scenario; requested minutes: $Minutes; samples: $($rows.Count)"
        "Steady-state CPU mean (% of all logical cores): $([math]::Round($cpuMean,2))"
        "Private memory change (MiB): $([math]::Round($privateDelta,2))"
        "GDI handle change: $gdiDelta"
        "SafeFile committed payload (MiB): $([math]::Round($writeMiB,3))"
        "RSS bytes: $($last.rss_bytes); rendered frames: $($last.frames)"
        'Logical payload is not physical NAND writes. Live provider availability and desktop compositor differ by runner.'
    )
    $summary | Tee-Object -FilePath (Join-Path $Out "$stem-summary.txt")
    if ($env:GITHUB_STEP_SUMMARY) { $summary | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY }
    # Broad runaway guards, not claimed desktop performance targets.
    if ($privateDelta -gt 256 -or $gdiDelta -gt 200) { throw 'Resource growth exceeds smoke guard' }
    if ($Scenario -eq 'Idle') {
        $unexpected = @($rows | Where-Object {
            [int]$_.active_services -ne 0 -or [int]$_.refresh_timers_active -ne 0 -or
            [int]$_.updates_active -ne 0 -or [int]$_.market_initialized -ne 0 -or
            [int]$_.refresh_jobs -ne 0 -or [long]$_.rss_bytes -ne 0 -or [long]$_.bar_visible -ne 0
        })
        if ($unexpected.Count -gt 0) { throw "Idle demand guard failed in $($unexpected.Count) samples" }
        if ([long]$last.frames -ne [long]$first.frames) { throw 'Hidden ticker continued rendering in idle' }
        'Idle demand guard passed: no optional services, refresh jobs, RSS bytes, visible bar or market history.' | Tee-Object -FilePath (Join-Path $Out "$stem-summary.txt") -Append
    } elseif ([long]$last.frames -le 0) { throw 'Ticker did not render frames' }
} finally {
    if (!$p.HasExited) { $p.Kill(); $p.WaitForExit() }
    $p.Dispose()
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
