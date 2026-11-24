# Read-only compatibility preflight for Opsis 2.8.x and the Crawler 0.28.0 / .NET 11 plugin.
# Usage: pwsh ./tools/verify-opsis-host-p0.ps1 -HostFolder 'C:\Program Files\Opsis' -PluginFolder 'C:\...\Opsis - Desktop Customization'
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$HostFolder,
  [Parameter(Mandatory=$true)][string]$PluginFolder,
  [string]$ChronikologioFolder
)
$ErrorActionPreference = 'Stop'
function ManagedVersion([string]$path) {
  if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing binary: $path" }
  return [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $path).Path).Version.ToString()
}
function Sha([string]$path) {
  if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing binary: $path" }
  return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}
$hostExe = Join-Path $HostFolder 'Opsis.exe'
if (!(Test-Path -LiteralPath $hostExe -PathType Leaf)) { throw "Missing Opsis host executable: $hostExe" }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $hostExe).Path).ProductVersion
if (!$version -or !$version.StartsWith('2.8.')) { throw "Expected Opsis 2.8.x; found $version" }
$hostContract = Join-Path $HostFolder 'OpsisNewsCrawler.Plugin.Abstractions.dll'
$hostUi = Join-Path $HostFolder 'Opsis.UI.dll'
$pluginDll = Join-Path $PluginFolder 'OpsisNewsCrawler.OpsisPlugin.dll'
$pluginContract = Join-Path $PluginFolder 'OpsisNewsCrawler.Plugin.Abstractions.dll'
$pluginUi = Join-Path $PluginFolder 'Opsis.UI.dll'
if ((ManagedVersion $hostContract) -ne '1.0.0.0' -or (ManagedVersion $pluginContract) -ne '1.0.0.0' -or (ManagedVersion $pluginDll) -ne '1.0.0.0') {
  throw 'Opsis / Crawler plugin contract ABI mismatch: expected 1.0.0.0.'
}
if ((Sha $hostContract) -ne (Sha $pluginContract)) {
  throw 'The host and Crawler contract binaries have different hashes; do not activate this plugin.'
}
if ((ManagedVersion $hostUi) -ne '0.1.0.0' -or (ManagedVersion $pluginUi) -ne '0.1.0.0') {
  throw 'Opsis.UI AssemblyVersion mismatch (expected 0.1.0.0).'
}
if ((Sha $hostUi) -ne (Sha $pluginUi)) {
  throw 'The host and Crawler Opsis.UI 0.6.0 binaries have different hashes.'
}
$uiFileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $hostUi).Path).FileVersion
if ($uiFileVersion -ne '0.6.0.0') { throw "Wrong Opsis.UI file version: $uiFileVersion" }
if ($ChronikologioFolder) {
  $chronContract = Join-Path $ChronikologioFolder 'OpsisNewsCrawler.Plugin.Abstractions.dll'
  if ((ManagedVersion $chronContract) -ne '1.0.0.0' -or (Sha $hostContract) -ne (Sha $chronContract)) {
    throw 'Chronikologio shared contract is not byte-identical with Opsis/Crawler.'
  }
}
# No registry reads or writes, no installation, no process start or system changes.
[pscustomobject]@{
  Outcome = 'PASS'
  OpsisHost = $version
  PluginContractAssembly = '1.0.0.0'
  OpsisUIFile = $uiFileVersion
  ContractSha256 = (Sha $hostContract)
  UiSha256 = (Sha $hostUi)
  ChronikologioChecked = [bool]$ChronikologioFolder
} | Format-List
