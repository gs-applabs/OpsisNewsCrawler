# Fails when the copies of the plugin contract DLL (OpsisNewsCrawler.Plugin.Abstractions.dll) do not all have the same SHA-256.
#   powershell -File tools\check-contract-sha.ps1 [-Extra path1,path2]      (Extra: more copies, e.g. Opsis\libs of a checkout, or the DLL in a release payload)
# Always checked: the canonical copy (tests\...\reference) against contract.sha256, and the copies next to the built plugin / program when they exist.
param([string[]]$Extra = @())
$ErrorActionPreference = "Stop"
$r = Split-Path -Parent $PSScriptRoot
$ref = "$r\tests\OpsisNewsCrawler.Plugin.Tests\reference"
$canonical = "$ref\OpsisNewsCrawler.Plugin.Abstractions.dll"
$want = (Get-Content "$ref\contract.sha256" -Raw).Trim().Split(' ')[0].ToLower()
$list = @($canonical) + @(Get-ChildItem "$r\src\*\bin\*" -Recurse -Filter OpsisNewsCrawler.Plugin.Abstractions.dll -ErrorAction SilentlyContinue |
  Where-Object { -not $_.FullName.Contains("OpsisNewsCrawler.Plugin.Abstractions" + [IO.Path]::DirectorySeparatorChar + "bin") -and -not $_.FullName.Contains("OpsisNewsCrawler.Windows" + [IO.Path]::DirectorySeparatorChar + "bin") } | ForEach-Object FullName)
foreach ($p in @("$r\..\Opsis\libs\OpsisNewsCrawler.Plugin.Abstractions.dll", "$r\Output\stage\plugins\Opsis - Desktop Customization\OpsisNewsCrawler.Plugin.Abstractions.dll") + $Extra) { if (Test-Path $p) { $list += (Resolve-Path $p).Path } }
$bad = 0
foreach ($p in $list) {
  $h = (Get-FileHash $p -Algorithm SHA256).Hash.ToLower()
  $ok = $h -eq $want
  "{0}  {1}  {2}" -f $(if ($ok) { "OK  " } else { "DIFF" }), $h.Substring(0, 16), $p
  if (-not $ok) { $bad++ }
}
if ($bad -gt 0) { throw "$bad copy/copies of the contract DLL differ from the canonical SHA-256 $want" }
"all $($list.Count) copies have the SHA-256 $want"
