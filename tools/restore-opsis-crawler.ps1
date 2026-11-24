# Opsis News Crawler P0: explicit, integrity-gated rollback for an integrated plugin.
# Usage from elevated PowerShell: .\tools\restore-opsis-crawler.ps1 -Mode Inspect|Restore
#   -HostFolder 'C:\Program Files\Opsis - Desktop Customization'
#   -PluginFolder 'C:\Program Files\Opsis - Desktop Customization\plugins\Opsis - Desktop Customization'
# Defaults to the latest completed backup under %ProgramData%\Opsis\CrawlerRollback.
[CmdletBinding(SupportsShouldProcess=$true, ConfirmImpact='High')]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Inspect','Restore')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$HostFolder,
    [Parameter(Mandatory=$true)][string]$PluginFolder,
    [string]$BackupRoot = (Join-Path $env:ProgramData 'Opsis\CrawlerRollback')
)
$ErrorActionPreference = 'Stop'
function Full([string]$path) { [IO.Path]::GetFullPath($path) }
function EnsureRealDirectory([string]$path) {
    if (!(Test-Path -LiteralPath $path -PathType Container)) { throw "Missing folder: $path" }
    if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing junction/symlink directory: $path"
    }
}
function FindHostDll([string]$name) {
    foreach($root in @($HostFolder,(Join-Path $HostFolder 'lib'),(Join-Path $HostFolder 'libs'))) {
        $file = Join-Path $root $name
        if(Test-Path -LiteralPath $file -PathType Leaf) { return $file }
    }
    throw "Host library not found: $name"
}
$HostFolder = Full $HostFolder
$PluginFolder = Full $PluginFolder
$BackupRoot = Full $BackupRoot
# The integrated Inno installer always installs THIS plugin into the leaf
# 'Opsis - Desktop Customization', whether the parent is protected Program
# Files, per-user AppData, or an explicitly selected PLUGINROOT.
# A typo must never rename/delete another Opsis plugin or arbitrary folder.
$expectedLeaf = 'Opsis - Desktop Customization'
$actualLeaf = Split-Path $PluginFolder.TrimEnd([char[]]@('\', '/')) -Leaf
if ($PluginFolder -eq $HostFolder -or
    ![string]::Equals($actualLeaf, $expectedLeaf, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Invalid Crawler plugin destination: $PluginFolder (expected leaf $expectedLeaf)"
}
EnsureRealDirectory (Split-Path $PluginFolder -Parent)
EnsureRealDirectory $HostFolder
EnsureRealDirectory $BackupRoot
$pending = Join-Path $BackupRoot 'pending'
$previous = Join-Path $BackupRoot 'previous'
if ((Test-Path -LiteralPath $pending -PathType Container) -and
    !(Test-Path -LiteralPath (Join-Path $pending '.backup-complete') -PathType Leaf)) {
    throw 'An unfinished rollback backup exists. Do not restore a partially copied plugin.'
}
$snapshot = if(Test-Path -LiteralPath $pending -PathType Container){$pending}else{$previous}
EnsureRealDirectory $snapshot
$marker = Join-Path $snapshot '.backup-complete'
if (!(Test-Path -LiteralPath $marker -PathType Leaf)) { throw 'No complete plugin backup marker.' }
$contents = @(Get-ChildItem -LiteralPath $snapshot -Force -Recurse)
if ($contents.Count -gt 300 -or ($contents | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint })) {
    throw 'Backup has too many items or contains links.'
}
$total = ($contents | Where-Object { -not $_.PSIsContainer } | Measure-Object -Property Length -Sum).Sum
if ($total -gt 300MB) { throw 'Unusually large backup: refusing to restore.' }
$contract = FindHostDll 'OpsisNewsCrawler.Plugin.Abstractions.dll'
$ui = FindHostDll 'Opsis.UI.dll'
$backupContract = Join-Path $snapshot 'OpsisNewsCrawler.Plugin.Abstractions.dll'
$backupUi = Join-Path $snapshot 'Opsis.UI.dll'
$backupDll = Join-Path $snapshot 'OpsisNewsCrawler.OpsisPlugin.dll'
foreach($required in @($backupContract,$backupUi,$backupDll)) {
    if (!(Test-Path -LiteralPath $required -PathType Leaf)) { throw "Backup is missing $required" }
}
function Ver([string]$file){ [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $file).Path).Version.ToString() }
if ((Ver $contract) -ne '1.0.0.0' -or (Ver $backupContract) -ne '1.0.0.0' -or
    (Ver $ui) -ne '0.1.0.0' -or (Ver $backupUi) -ne '0.1.0.0') {
    throw 'Backup ABI/UI version is incompatible with this Opsis host.'
}
foreach($pair in @(@($contract,$backupContract),@($ui,$backupUi))) {
    if ((Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $pair[1] -Algorithm SHA256).Hash) {
        throw 'Backup shared DLL is not byte-identical to the Opsis host: coordinated host rollback is required.'
    }
}
Write-Host "P0 completed rollback snapshot: $snapshot"
Write-Host "Compatible with current host contract/UI; files $($contents.Count), payload bytes $total"
if($Mode -eq 'Inspect'){return}
# The host uses the same mutex name in Program.cs and the Inno installer.
# Check both, since the process may be launched with a different exe name.
try {
    $runningHostMutex = [Threading.Mutex]::OpenExisting('Local\Opsis.SingleInstance')
    $runningHostMutex.Dispose()
    throw 'Opsis host mutex is active. Close Opsis before restoring a plugin.'
}
catch [Threading.WaitHandleCannotBeOpenedException] { }
if (Get-Process -Name Opsis -ErrorAction SilentlyContinue) {
    throw 'Close Opsis completely before attempting a plugin rollback.'
}
if (!(Test-Path -LiteralPath $PluginFolder -PathType Container)) { throw "Missing plugin installation: $PluginFolder" }
EnsureRealDirectory $PluginFolder
$parent = Split-Path $PluginFolder -Parent
$leaf = Split-Path $PluginFolder -Leaf
$stage = Join-Path $parent ($leaf + '.p0-restore-staging')
$failed = Join-Path $parent ($leaf + '.p0-replaced')
foreach($path in @($stage,$failed)){
    if(Test-Path -LiteralPath $path){throw "Previous unfinished rollback artifact: $path"}
}
if (!$PSCmdlet.ShouldProcess($PluginFolder, "Restore snapshot $snapshot (Opsis must be stopped)")){return}
Copy-Item -LiteralPath $snapshot -Destination $stage -Recurse -Force
if(!(Test-Path -LiteralPath (Join-Path $stage 'OpsisNewsCrawler.OpsisPlugin.dll'))) {
    throw 'Could not stage the backed-up plugin.'
}
# Verify copied backup's exact DLL bytes before touching the running version.
foreach($name in @('OpsisNewsCrawler.OpsisPlugin.dll','Opsis.UI.dll','OpsisNewsCrawler.Plugin.Abstractions.dll')){
    if((Get-FileHash -LiteralPath (Join-Path $stage $name) -Algorithm SHA256).Hash -ne
       (Get-FileHash -LiteralPath (Join-Path $snapshot $name) -Algorithm SHA256).Hash) {
        throw "Staged rollback file is corrupted: $name"
    }
}
# Marker is a backup-only recovery guard and does not belong inside the
# deployed plugin's code directory.
Remove-Item -LiteralPath (Join-Path $stage '.backup-complete') -Force
$moved = $false
try{
    Move-Item -LiteralPath $PluginFolder -Destination $failed -ErrorAction Stop
    $moved = $true
    Move-Item -LiteralPath $stage -Destination $PluginFolder -ErrorAction Stop
} catch {
    if($moved -and !(Test-Path -LiteralPath $PluginFolder) -and (Test-Path -LiteralPath $failed)) {
        Move-Item -LiteralPath $failed -Destination $PluginFolder -ErrorAction Stop
    }
    throw
}
# Only the superseded *plugin code folder* is deleted. All user settings and
# host recovery snapshots remain in place.
Remove-Item -LiteralPath $failed -Recurse -Force
Write-Host 'PASS: Crawler plugin restored. Reopen Opsis to load the previous working plugin.'
