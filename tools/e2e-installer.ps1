# End-to-end test of OpsisNewsCrawler-Setup.exe on a CLEAN, DISPOSABLE Windows machine (a GitHub-hosted runner). It installs, upgrades, starts and uninstalls
# for real, so it REFUSES to run anywhere else (it would change the registry and Program Files of the machine it runs on).
#
#   -Scenario fresh          new setup on a clean machine
#   -Scenario from-per-user  the old per-user release (v0.17.0), started and with "Start with Windows", then the new machine-wide setup
#   -Scenario from-latest    the newest published release (machine-wide) RUNNING, then the new setup (an update; the installer closes the running program)
#   -Scenario update-helper  the newest published release running, then the way the program updates itself: its own helper (--apply-update) waits for it to end, checks the
#                            SHA-256, runs the setup elevated, writes last-update.log and opens the program again
#
# Results: <Out>\report.md (also the job summary), the Inno logs, crawler.log, a screenshot. Exit code 1 on any failed check.
param(
    [Parameter(Mandatory)][ValidateSet('fresh', 'from-per-user', 'from-latest', 'from-candidate', 'update-helper', 'integrated')][string]$Scenario,
    [Parameter(Mandatory)][string]$NewSetup,
    [string]$Repository = 'gs-applabs/OpsisNewsCrawler',
    [string]$PerUserTag = 'v0.17.0',
    [string]$ContractFile = '',
    [string]$Out = (Join-Path ([IO.Path]::GetTempPath()) 'opsis-e2e'),
    [switch]$IAmADisposableMachine
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -and -not $IAmADisposableMachine) {
    throw 'This script installs and uninstalls the program for real. Run it only on a GitHub-hosted runner or a disposable VM (-IAmADisposableMachine). Never on a PC that is in use.'
}
New-Item -ItemType Directory -Force $Out | Out-Null
$AppId = '{B7E3C9A2-5D41-4F68-9A0E-3C1D7F52A8B4}_is1'
$UninstallKey = "Software\Microsoft\Windows\CurrentVersion\Uninstall\$AppId"
$RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run'
$MachineDir = Join-Path $env:ProgramFiles 'Opsis News Crawler'
$UserDir = Join-Path $env:LOCALAPPDATA 'Programs\Opsis News Crawler'
$SettingsDir = Join-Path $env:APPDATA 'OpsisNewsCrawler'
$report = New-Object System.Collections.Generic.List[string]
$failures = 0

function Check([bool]$ok, [string]$what) {
    $mark = if ($ok) { 'PASS' } else { 'FAIL' }
    if (-not $ok) { $script:failures++ }
    $line = "- **$mark** $what"
    $script:report.Add($line)
    Write-Host "$mark  $what"
}
function Stop-OnFailure([string]$why) {
    $script:report.Add("- **BLOCKED** $why")
    $script:report | Set-Content (Join-Path $Out 'report.md')
    if ($env:GITHUB_STEP_SUMMARY) { $script:report | Add-Content $env:GITHUB_STEP_SUMMARY }
    throw $why
}
function Note([string]$text) { $script:report.Add("- _NOT VERIFIED_ $text"); Write-Host "NOT VERIFIED  $text" }
function Run-Setup([string]$exe, [string[]]$arguments, [string]$logName) {
    $log = Join-Path $Out $logName
    $p = Start-Process -FilePath $exe -ArgumentList ($arguments + "/LOG=`"$log`"") -Wait -PassThru
    return $p.ExitCode
}
function Reg-Value([string]$hive, [string]$path, [string]$name) {
    try { (Get-ItemProperty -Path "${hive}:\$path" -Name $name -ErrorAction Stop).$name } catch { $null }
}
function Reg-Key([string]$hive, [string]$path) { Test-Path "${hive}:\$path" }
function Programs-Copies { @($MachineDir, $UserDir) | Where-Object { Test-Path $_ } | ForEach-Object { Get-ChildItem $_ -Filter 'OpsisNewsCrawler.exe' -Recurse -ErrorAction SilentlyContinue } }
function Screenshot([string]$name) {
    try {
        Add-Type -AssemblyName System.Windows.Forms, System.Drawing
        $b = [Windows.Forms.SystemInformation]::VirtualScreen
        $bmp = New-Object Drawing.Bitmap $b.Width, $b.Height
        [Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Left, $b.Top, 0, 0, $bmp.Size)
        $bmp.Save((Join-Path $Out $name)); $bmp.Dispose()
    } catch { Write-Host "screenshot not possible: $($_.Exception.Message)" }
}
function Expected-Version {
    $v = (Get-Item $NewSetup).VersionInfo.ProductVersion
    if ($v) { return $v.Split('+')[0].Trim() }   # the setup's own version resource is padded with spaces
    return $null
}

$report.Add("# Installer end-to-end: $Scenario on $([Environment]::OSVersion.VersionString), $([Environment]::ProcessorCount) logical processors")
$report.Add('')

# ---- ABI-gated integrated plugin installation in a disposable Windows runner ------------------
# Only install a real plugin after a matching host contract has been staged. This simulates
# the host install directory; real Opsis DLL loading is separately tested in the Opsis CI.
if ($Scenario -eq 'integrated') {
    if (-not $ContractFile -or -not (Test-Path $ContractFile)) {
        throw 'Integrated scenario requires -ContractFile pointing to the P3 DLL built by CI.'
    }
    $hostLibs = Join-Path $env:LOCALAPPDATA 'Programs\Opsis - Desktop Customization\libs'
    $hostContract = Join-Path $hostLibs 'OpsisNewsCrawler.Plugin.Abstractions.dll'
    $hostUi = Join-Path $hostLibs 'Opsis.UI.dll'
    $uiSource = Join-Path (Split-Path -Parent $PSScriptRoot) 'libs\Opsis.UI.dll'
    if (-not (Test-Path $uiSource)) { throw 'Missing UI6 payload used by the P0 preflight.' }
    $pluginRoot = Join-Path $Out 'isolated-opsis-plugins'
    $pluginDir = Join-Path $pluginRoot 'Opsis - Desktop Customization'
    New-Item -ItemType Directory -Force $hostLibs, $pluginRoot | Out-Null
    Remove-Item $hostContract -Force -ErrorAction SilentlyContinue
    $setupArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/MODE=integrated',
              "/PLUGINROOT=`"$pluginRoot`"", '/DEPS=none')
    $denied = Run-Setup $NewSetup $setupArgs 'integrated-denied.log'
    Check ($denied -ne 0) 'installer refuses integrated plugin deployment without host ABI 1.0.0.0'
    Check (-not (Test-Path (Join-Path $pluginDir 'OpsisNewsCrawler.OpsisPlugin.dll'))) 'refused install did not write plugin DLL'
    Copy-Item $ContractFile $hostContract -Force
    Copy-Item $uiSource $hostUi -Force
    # The Opsis 2.8 host holds Local\Opsis.SingleInstance while running.
    # Integrated setup must not write DLLs before it has exited.
    $hostMutex = [Threading.Mutex]::new($false, 'Local\Opsis.SingleInstance')
    try {
        $runningHost = Run-Setup $NewSetup $setupArgs 'integrated-host-running-denied.log'
        Check ($runningHost -ne 0) 'integrated setup refuses live Opsis host mutex'
        Check (-not (Test-Path (Join-Path $pluginDir 'OpsisNewsCrawler.OpsisPlugin.dll'))) 'live host rejection changed no Crawler files'
    } finally {
        $hostMutex.Dispose()
    }
    # A DLL may claim the right FileVersion while its bytes differ.
    # The integrated installer must reject that case before copying anything.
    $modified = [IO.File]::ReadAllBytes($hostUi)
    $modified[$modified.Length - 1] = $modified[$modified.Length - 1] -bxor 1
    [IO.File]::WriteAllBytes($hostUi, $modified)
    $mismatch = Run-Setup $NewSetup $setupArgs 'integrated-ui-hash-denied.log'
    Check ($mismatch -ne 0) 'integrated installer rejects mismatched Opsis.UI SHA256'
    Check (-not (Test-Path (Join-Path $pluginDir 'OpsisNewsCrawler.OpsisPlugin.dll'))) 'UI6 SHA mismatch did not deploy the plugin'
    Copy-Item $uiSource $hostUi -Force
    # A separately installed Chronikologio with the wrong ABI bytes must not
    # be damaged by attempting an integrated Crawler update.
    $chronDir = Join-Path $env:APPDATA 'Opsis\plugins\Chronikologio'
    New-Item -ItemType Directory -Force $chronDir | Out-Null
    Set-Content (Join-Path $chronDir 'Chronikologio.OpsisPlugin.dll') 'CI-only stub plugin' -Encoding utf8
    $chronContract = Join-Path $chronDir 'OpsisNewsCrawler.Plugin.Abstractions.dll'
    Copy-Item $hostContract $chronContract -Force
    $modified = [IO.File]::ReadAllBytes($chronContract)
    $modified[$modified.Length - 1] = $modified[$modified.Length - 1] -bxor 1
    [IO.File]::WriteAllBytes($chronContract, $modified)
    $chronDenied = Run-Setup $NewSetup $setupArgs 'integrated-chron-abi-denied.log'
    Check ($chronDenied -ne 0) 'integrated setup refuses the wrong Chronikologio shared ABI'
    Check (-not (Test-Path (Join-Path $pluginDir 'OpsisNewsCrawler.OpsisPlugin.dll'))) 'incompatible Chronikologio did not cause Crawler replacement'
    Remove-Item $chronDir -Recurse -Force

    $hostVersion = [Reflection.AssemblyName]::GetAssemblyName($hostContract).Version.ToString()
    Check ($hostVersion -eq '1.0.0.0') "staged host ABI identity is $hostVersion"
    # Existing installed plugin is replaced; P0 must back it up *before* copying
    # anything. This is a data-only fixture under the runner's isolated root.
    New-Item -ItemType Directory -Force $pluginDir | Out-Null
    $oldPlugin = Join-Path $pluginDir 'OpsisNewsCrawler.OpsisPlugin.dll'
    Set-Content -LiteralPath $oldPlugin -Value 'pre-p0-plugin-fixture' -Encoding utf8
    Copy-Item $hostContract (Join-Path $pluginDir 'OpsisNewsCrawler.Plugin.Abstractions.dll') -Force
    Copy-Item $hostUi (Join-Path $pluginDir 'Opsis.UI.dll') -Force
    $rollbackRoot = Join-Path $env:ProgramData 'Opsis\CrawlerRollback'
    if (Test-Path (Join-Path $rollbackRoot 'pending')) {
        throw 'Disposable runner should not have an unfinished rollback snapshot'
    }
    $installed = Run-Setup $NewSetup $setupArgs 'integrated-installed.log'
    $previous = Join-Path $rollbackRoot 'previous'
    Check (Test-Path (Join-Path $previous '.backup-complete')) 'integrated upgrade has a completed rollback snapshot'
    Check ((Test-Path (Join-Path $previous 'OpsisNewsCrawler.OpsisPlugin.dll')) -and
           ((Get-Content (Join-Path $previous 'OpsisNewsCrawler.OpsisPlugin.dll') -Raw) -match 'pre-p0-plugin-fixture')) 'old plugin bytes are preserved for rollback'
    Check ($installed -eq 0) "integrated installation succeeds against host ABI 1.0.0.0 (exit $installed)"
    $dll = Join-Path $pluginDir 'OpsisNewsCrawler.OpsisPlugin.dll'
    Check (Test-Path $dll) 'integrated setup installs the real Crawler plugin in Opsis plugin root'
    if (Test-Path $dll) {
        $abi = [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
        $file = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
        Check ($abi -eq '1.0.0.0' -and $file -eq '1.0.0.0') "plugin DLL AssemblyVersion/FileVersion are $abi/$file"
    }
    Check (-not (Test-Path (Join-Path $pluginDir 'OpsisNewsCrawler.exe'))) 'integrated mode does not create a standalone executable'
    $uninstaller = Join-Path $pluginDir 'unins000.exe'
    Check (Test-Path $uninstaller) 'integrated installer writes the uninstaller'
    if (Test-Path $uninstaller) {
        $removed = Run-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'integrated-uninstall.log'
        Check ($removed -eq 0) "integrated uninstaller exits successfully ($removed)"
        Check (-not (Test-Path $dll)) 'uninstall removes the plugin DLL'
    }
    $report | Set-Content (Join-Path $Out 'report.md')
    if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
    if ($failures -gt 0) { exit 1 }
    exit 0
}

# ---- 1. the old version ----------------------------------------------------------------------------------------------------------------
$oldIsPerUser = $false
if ($Scenario -eq 'from-candidate') {
    # Install and run this exact candidate, then reinstall it with no force option.
    $tag = 'current candidate'
    $code = Run-Setup $NewSetup @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/MODE=separate','/DEPS=none') 'candidate-first-install.log'
    if ($code -ne 0) { Stop-OnFailure "Candidate initial install failed ($code)" }
    New-Item -ItemType Directory -Force $SettingsDir | Out-Null
    Set-Content (Join-Path $SettingsDir 'config.json') '{"Language":"de","ScrollSpeed":80,"CheckForUpdates":false,"WelcomeDisabled":true}' -Encoding utf8
    $oldExe = Join-Path $MachineDir 'OpsisNewsCrawler.exe'
    $oldRunning = Start-Process -FilePath $oldExe -PassThru
    Start-Sleep -Seconds 8
    if ($oldRunning.HasExited) { Stop-OnFailure 'Candidate did not remain running before reinstall' }
} elseif ($Scenario -ne 'fresh') {
    $tag = if ($Scenario -eq 'from-per-user') { $PerUserTag } else { (& gh release view --repo $Repository --json tagName --jq .tagName) }
    $oldDir = Join-Path $Out 'old'
    New-Item -ItemType Directory -Force $oldDir | Out-Null
    & gh release download $tag --repo $Repository --pattern 'OpsisNewsCrawler-Setup.exe*' --dir $oldDir --clobber 2>&1 | Out-Host
    $oldSetup = Join-Path $oldDir 'OpsisNewsCrawler-Setup.exe'
    if (-not (Test-Path $oldSetup)) {
        $report.Add("- skipped: release $tag has no OpsisNewsCrawler-Setup.exe (nothing to upgrade from)")
        Write-Host "::notice::release $tag has no setup asset: scenario $Scenario skipped"
        $report | Set-Content (Join-Path $Out 'report.md'); if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
        exit 0
    }
    $sumFile = "$oldSetup.sha256"
    if (Test-Path $sumFile) {
        $expected = ((Get-Content $sumFile -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
        Check ((Get-FileHash $oldSetup -Algorithm SHA256).Hash.ToLowerInvariant() -eq $expected) "the downloaded $tag setup matches its published SHA-256"
    }
    # the user's own settings must survive every upgrade
    New-Item -ItemType Directory -Force $SettingsDir | Out-Null
    Set-Content (Join-Path $SettingsDir 'config.json') '{"Language":"de","ScrollSpeed":80}' -Encoding utf8

    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/MODE=separate', '/DEPS=none')
    if ($Scenario -eq 'from-per-user') { $arguments += '/TASKS="startup"' }
    $code = Run-Setup $oldSetup $arguments 'old-setup.log'
    Check ($code -eq 0) "the old setup ($tag) installed (exit code $code)"
    $oldIsPerUser = Test-Path (Join-Path $UserDir 'OpsisNewsCrawler.exe')
    Check ((Programs-Copies).Count -ge 1) "the old version is installed ($(if ($oldIsPerUser) { 'per user' } else { 'machine-wide' }))"
    if ($Scenario -eq 'from-per-user') {
        Check $oldIsPerUser "$tag installs per user (%LocalAppData%\Programs)"
        Check ($null -ne (Reg-Value 'HKCU' $RunKey 'Opsis News Crawler')) 'the old per-user copy has its start-up entry'
        # the old program is RUNNING while the new setup removes it (the case that left two copies in 0.18.5)
        $old = Start-Process -FilePath (Join-Path $UserDir 'OpsisNewsCrawler.exe') -ArgumentList '--config', (Join-Path $Out 'old-running.json') -PassThru
        Start-Sleep -Seconds 6
        Check (-not $old.HasExited) 'the old program is running before the upgrade'
    }
    if ($Scenario -in 'from-latest', 'update-helper') {
        # the real update: the old (machine-wide) program is RUNNING while the new version is installed
        $oldExe = Join-Path $MachineDir 'OpsisNewsCrawler.exe'
        $oldRunning = Start-Process -FilePath $oldExe -ArgumentList '--config', (Join-Path $Out 'old-running.json') -PassThru
        Start-Sleep -Seconds 8
        Check (-not $oldRunning.HasExited) "the old program ($tag) is running before the update"
    }
}

# ---- 2. the new setup ------------------------------------------------------------------------------------------------------------------
$newArgs = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/MODE=separate', '/BAR=above', '/DEPS=none')
if ($Scenario -eq 'from-per-user') { $newArgs += '/TASKS="startup"','/CLOSEAPPLICATIONS','/FORCECLOSEAPPLICATIONS' }
# Explicit forced recovery is permitted for the legacy release only. Candidate
# reinstalls MUST close gracefully, never hide a regression behind forced exit.
if ($Scenario -eq 'from-latest') { $newArgs += '/CLOSEAPPLICATIONS','/FORCECLOSEAPPLICATIONS' }
if ($Scenario -eq 'from-candidate') { $newArgs += '/CLOSEAPPLICATIONS' }
if ($Scenario -eq 'update-helper') {
    $oldVersion = [version](($oldExeInfo = (Get-Item $oldExe).VersionInfo.ProductVersion).Split('+')[0])
    if ($oldVersion -lt [version]'0.20.3') {
        Note "the published $tag has no update helper (needs 0.20.3 or newer): scenario update-helper skipped"
        $report | Set-Content (Join-Path $Out 'report.md'); if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
        Stop-Process -Id $oldRunning.Id -Force -ErrorAction SilentlyContinue
        exit 0
    }
    # what the program does when the user chooses "Install": start the helper, then quit. The helper is started from a copy (as the program does), here by the program's own switch.
    $sha = (Get-FileHash $NewSetup -Algorithm SHA256).Hash.ToLowerInvariant()
    $helperLog = Join-Path $Out 'last-update.log'
    Remove-Item $helperLog -ErrorAction SilentlyContinue
    $helperDir = Join-Path $Out 'helper-copy'
    Remove-Item $helperDir -Recurse -Force -ErrorAction SilentlyContinue
    robocopy (Split-Path $oldExe) $helperDir /E /XD plugins /NFL /NDL /NJH /NJS | Out-Null
    $helper = Start-Process -FilePath (Join-Path $helperDir 'OpsisNewsCrawler.exe') -ArgumentList '--apply-update', $oldRunning.Id, "`"$NewSetup`"", $sha, "`"$helperLog`"", '0.0.0', 'standalone', "`"$oldExe`"" -PassThru
    Start-Sleep -Seconds 1
    Stop-Process -Id $oldRunning.Id -Force
    $finished = $helper.WaitForExit(240000)
    Check $finished 'the update helper finished'
    Check ($finished -and $helper.ExitCode -eq 0) "the update helper succeeded (exit code $(if ($finished) { $helper.ExitCode } else { 'none' }))"
    if (!$finished -or $helper.ExitCode -ne 0) { Stop-OnFailure 'Update helper failed; no further product audit' }
    $logLine = if (Test-Path $helperLog) { (Get-Content $helperLog | Select-Object -Last 1) } else { '' }
    Check ($logLine -like '*|ok|0|*') "last-update.log says the update worked: $logLine"
    Start-Sleep -Seconds 8
    $reopened = @(Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$MachineDir*" })
    Check ($reopened.Count -ge 1) 'the helper opened the updated program again (from Program Files)'
    $reopened | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Note 'the UAC prompt of the elevation (the runner is an administrator without prompts)'
} else {
    $code = Run-Setup $NewSetup $newArgs 'new-setup.log'
    Check ($code -eq 0) "the new setup finished (exit code $code)"
    if ($code -ne 0) { Stop-OnFailure "Setup failed ($code); do not audit the old installed binaries" }
    if ($Scenario -in 'from-latest','from-candidate') {
        Start-Sleep -Seconds 1
        $closed = $oldRunning.HasExited -or -not (Get-Process -Id $oldRunning.Id -ErrorAction SilentlyContinue)
        Check $closed 'the installer closed the running old program'
        if (!$closed) { Stop-OnFailure 'Old process still running; no further product audit' }
    }
}
Start-Sleep -Seconds 2

$manifest = Join-Path (Split-Path -Parent $NewSetup) 'payload-hashes.json'
if (!(Test-Path $manifest)) { Stop-OnFailure 'Exact candidate payload hash manifest missing' }
$expectedPayload = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
foreach ($entry in $expectedPayload.PSObject.Properties) {
    $installed = Join-Path $MachineDir $entry.Name
    $match = (Test-Path -LiteralPath $installed) -and ((Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -eq $entry.Value)
    Check $match "installed $($entry.Name) matches staged candidate SHA-256"
    if (!$match) { Stop-OnFailure 'Installed binary is not the candidate under test' }
}
$version = Expected-Version
Check (Test-Path (Join-Path $MachineDir 'OpsisNewsCrawler.exe')) 'the program is in Program Files'
if ($version) { Check (((Get-Item (Join-Path $MachineDir 'OpsisNewsCrawler.exe')).VersionInfo.ProductVersion) -like "$version*") "the installed program is version $version" }
Check (Reg-Key 'HKLM' $UninstallKey) 'the machine-wide uninstall entry exists (HKLM)'
Check (-not (Reg-Key 'HKCU' $UninstallKey)) 'no per-user uninstall entry is left (HKCU)'
Check (-not (Test-Path $UserDir)) 'the old per-user folder is gone'
Check (@(Programs-Copies).Count -eq 1) "exactly one copy of OpsisNewsCrawler.exe is installed (found $(@(Programs-Copies).Count))"
Check ($null -eq (Reg-Value 'HKLM' $RunKey 'Opsis News Crawler')) 'no machine-wide start-up value (HKLM Run)'
$stray = @(Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$UserDir*" })
Check ($stray.Count -eq 0) 'no program of the old per-user folder is still running'
$menu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Opsis News Crawler'
Check ((Test-Path $menu) -and @(Get-ChildItem $menu -Filter '*.lnk' -ErrorAction SilentlyContinue).Count -ge 1) 'the all-users Start menu shortcut exists'
Check (-not (Test-Path (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Opsis News Crawler'))) 'no per-user Start menu folder is left'
if ($Scenario -ne 'fresh') {
    $config = Get-Content (Join-Path $SettingsDir 'config.json') -Raw -ErrorAction SilentlyContinue
    Check ($config -like '*"Language":"de"*' -or $config -like '*"Language": "de"*') 'the settings (language) survived the upgrade'
}
if ($Scenario -eq 'fresh') {
    Check (Test-Path (Join-Path $SettingsDir 'first-run.json')) 'a clean installation carries the choices of the wizard (first-run.json)'
}

# ---- 3. start it ------------------------------------------------------------------------------------------------------------------------
$exe = Join-Path $MachineDir 'OpsisNewsCrawler.exe'
$smoke = Start-Process -FilePath $exe -PassThru        # a normal start: tray icon, bars, the welcome guide of a first run
Start-Sleep -Seconds 12
Check (-not $smoke.HasExited) 'the program starts and keeps running'
Screenshot 'running.png'
if ($Scenario -eq 'from-per-user') {
    $run = Reg-Value 'HKCU' $RunKey 'Opsis News Crawler'
    Check ($run -and $run -like "*Program Files*") "the wish 'start with Windows' was carried over: the per-user Run value now points into Program Files ($run)"
}
$runValue = Reg-Value 'HKCU' $RunKey 'Opsis News Crawler'
if ($runValue) { Check ($runValue -like "*Program Files*") 'the start-up value points into Program Files' }
$log = Join-Path $env:APPDATA 'OpsisNewsCrawler\crawler.log'
$tierLine = $null
for ($i = 0; $i -lt 30 -and -not $tierLine; $i++) {   # the line is written once the first evaluation has run; a slow runner needs longer than a fixed sleep
    if (Test-Path $log) { $tierLine = (Select-String -Path $log -Pattern 'Performance tier:' | Select-Object -Last 1).Line }
    if (-not $tierLine) { Start-Sleep -Seconds 2 }
}
Check ($tierLine -like '*Low*') "the runner (2 vCPU, no GPU) is detected as a Low PC: $tierLine"
Stop-Process -Id $smoke.Id -Force -ErrorAction SilentlyContinue
Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$MachineDir*" } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# the audits run with a throw-away configuration (isolated registry), once per forced tier
foreach ($tier in @('', 'Low', 'High')) {
    $env:OPSISNEWSCRAWLER_FORCE_TIER = $tier
    foreach ($kind in 'audit-settings', 'audit-layout', 'audit-scroll') {
        $cfg = Join-Path $Out "$kind-$tier.json"
        $rep = Join-Path $Out "$kind-$tier.txt"
        Set-Content $cfg '{"Language":"en","Position":"Top"}' -Encoding utf8
        $p = Start-Process -FilePath $exe -ArgumentList "--$kind", "`"$rep`"", '--config', "`"$cfg`"" -PassThru
        if (-not $p.WaitForExit(600000)) { $p.Kill(); $p.WaitForExit(); throw "Timeout: $kind tier=$tier" }
        # Both report and process must be clean; a crash after a report is still a failure.
        $text = if (Test-Path $rep) { Get-Content $rep -Raw } else { '' }
        $clean = if ($kind -eq 'audit-settings') { $text -match '0 change nothing, 0 not shown again' -and $text -match 'keyboard checks failed: 0' -and $text -notmatch '(?m)^ERROR' }
                 elseif ($kind -eq 'audit-layout') { $text -match 'nothing sticks out' }
                 else { $text -match '(?m)^Scroll audit: [1-9][0-9]* pages that scroll, [1-9][0-9]* checks, 0 moved, 0 errors\s*$' -and $text -notmatch '(?m)^(MOVED|ERROR)' }
        $clean = $clean -and ($p.ExitCode -eq 0)
        if (-not $clean) {
            Write-Host "::group::DIAGNOSTIC $kind tier=$tier exit=$($p.ExitCode)"
            Write-Host "Report exists: $(Test-Path $rep); characters: $($text.Length)"
            if ($text) { Write-Host ($text.Substring(0, [Math]::Min(6000, $text.Length))) }
            else { Write-Host 'Report file is absent or empty: possible command-line/config/installed runtime issue.' }
            if (Test-Path $log) {
                Write-Host 'Recent process log lines:'
                Get-Content $log -Tail 35 | Out-Host
            }
            Write-Host "::endgroup::"
        }
        Check $clean "$kind with tier '$(if ($tier) { $tier } else { 'detected' })' reports nothing wrong (exit code $($p.ExitCode))"
    }
}
$env:OPSISNEWSCRAWLER_FORCE_TIER = ''
if (Test-Path $log) {
    $forced = @(Select-String -Path $log -Pattern 'Performance tier: (Low|High) \(forced\)').Count
    Check ($forced -ge 2) 'a forced Low and a forced High tier were both applied (crawler.log)'
    Copy-Item $log (Join-Path $Out 'crawler.log') -ErrorAction SilentlyContinue
}

# ---- 4. uninstall -----------------------------------------------------------------------------------------------------------------------
$uninstaller = Join-Path $MachineDir 'unins000.exe'
Check (Test-Path $uninstaller) 'the uninstaller is there'
$code = Run-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART') 'uninstall.log'
for ($i = 0; $i -lt 60 -and (Test-Path $MachineDir); $i++) { Start-Sleep -Seconds 1 }       # the uninstaller hands over to a helper and returns early
Check (-not (Test-Path (Join-Path $MachineDir 'OpsisNewsCrawler.exe'))) 'the program file is gone'
Check (-not (Test-Path $MachineDir) -or @(Get-ChildItem $MachineDir -Recurse -File -ErrorAction SilentlyContinue).Count -eq 0) 'no files are left in the Program Files folder'
Check (-not (Reg-Key 'HKLM' $UninstallKey)) 'the uninstall entry is gone (HKLM)'
Check (-not (Test-Path $menu)) 'the Start menu folder is gone'
Check ($null -eq (Reg-Value 'HKLM' $RunKey 'Opsis News Crawler') -and $null -eq (Reg-Value 'HKCU' $RunKey 'Opsis News Crawler')) 'no start-up value is left (HKLM / HKCU Run)'
Check (@(Programs-Copies).Count -eq 0) 'no OpsisNewsCrawler.exe is left anywhere in the program folders'
Check (Test-Path $SettingsDir) 'the settings in %AppData% are kept by a silent uninstall (by design)'

# ---- limits -----------------------------------------------------------------------------------------------------------------------------
Note 'the UAC prompt (the runner is an administrator without prompts), SmartScreen, a real graphics card, several monitors and per-monitor DPI cannot be tested on a runner'
$report.Add('')
$report.Add("**Result: $(if ($failures -eq 0) { 'all checks passed' } else { "$failures check(s) FAILED" })**")
$report | Set-Content (Join-Path $Out 'report.md')
if ($env:GITHUB_STEP_SUMMARY) { $report | Add-Content $env:GITHUB_STEP_SUMMARY }
if ($failures -gt 0) { exit 1 }
exit 0   # without this the exit code of the last native command (taskkill, the uninstaller) leaks out as the result of the script
