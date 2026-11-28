# Builds everything the installer packs and compiles it, then checks that nothing stale went in.
#   powershell -ExecutionPolicy Bypass -File tools\release.ps1
# Output: Output\OpsisNewsCrawler-Setup.exe and Output\OpsisNewsCrawler-Setup.exe.sha256
$ErrorActionPreference = "Stop"
$r = Split-Path -Parent $PSScriptRoot
Set-Location $r

Get-Process OpsisNewsCrawler -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$r\*" } | Stop-Process -Force
Start-Sleep -Seconds 1

dotnet build OpsisNewsCrawler.sln -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "build failed" }
# The payload is staged in Output\stage: a copy of the program that is running from bin\ (even an elevated one that cannot be stopped) never blocks a release.
$stage = "$r\Output\stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet publish src\OpsisNewsCrawler.App\OpsisNewsCrawler.App.csproj -c Release --no-build -o $stage --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "publish failed" }
# Prevent an older .NET 10 executable from being included under the 0.28.0 name.
$runtimeConfig = "$stage\OpsisNewsCrawler.runtimeconfig.json"
if (-not (Test-Path $runtimeConfig)) { throw "Missing .NET runtimeconfig: $runtimeConfig" }
$runtimeJson = Get-Content $runtimeConfig -Raw | ConvertFrom-Json
$tfm = [string]$runtimeJson.runtimeOptions.tfm
$frameworks = @($runtimeJson.runtimeOptions.frameworks) + @($runtimeJson.runtimeOptions.framework)
$desktop = $frameworks | Where-Object { $_ -and $_.name -eq 'Microsoft.WindowsDesktop.App' } | Select-Object -First 1
if ($tfm -ne 'net11.0' -or -not $desktop -or -not ([string]$desktop.version).StartsWith('11.0.')) {
  throw "Refusing installer: expected net11.0 + WindowsDesktop 11.0 runtime, got TFM '$tfm' and desktop '$($desktop.version)'."
}
"installer runtime gate: $tfm / WindowsDesktop $($desktop.version)"

# The plugin folder is MIRRORED (stale files removed) from the build output; robocopy needs backslash paths.
$pluginSrc = "$r\src\OpsisNewsCrawler.OpsisPlugin\bin\Release\net11.0-windows10.0.19041.0"
$pluginDst = "$stage\plugins\Opsis - Desktop Customization"
robocopy $pluginSrc $pluginDst /MIR /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "copying the plugin failed (robocopy $LASTEXITCODE)" }

# Guard: the app and every DLL of the plugin must have the same version as the app (this once shipped an old plugin).
$appVersion = (Get-Item "$stage\OpsisNewsCrawler.exe").VersionInfo.ProductVersion.Split('+')[0]
foreach ($name in "OpsisNewsCrawler.OpsisPlugin.dll", "OpsisNewsCrawler.Windows.dll", "OpsisNewsCrawler.Core.dll") {
  $v = (Get-Item "$pluginDst\$name").VersionInfo.ProductVersion.Split('+')[0]
  if ($v -ne $appVersion) { throw "$name is $v but the app is $appVersion - stale plugin payload" }
}
if (Get-ChildItem $pluginDst -Filter "*MotionDesk*" -ErrorAction SilentlyContinue) { throw "old MotionDesk-named files in the plugin payload - delete them" }
"payload version: $appVersion"

# Exact staged payload hashes distinguish same-version candidates (0.30.0).
$payloadHashes = [ordered]@{}
foreach ($name in @('OpsisNewsCrawler.exe','OpsisNewsCrawler.dll','OpsisNewsCrawler.Core.dll','OpsisNewsCrawler.Windows.dll')) {
  $payloadHashes[$name] = (Get-FileHash (Join-Path $stage $name) -Algorithm SHA256).Hash
}
$payloadHashes | ConvertTo-Json | Set-Content -LiteralPath "$r\Output\payload-hashes.json" -Encoding utf8

# P0: the plugin must ship the single host-canonical ABI DLL, not a separately compiled same-version assembly.
$canonicalContract = "$r\libs\OpsisNewsCrawler.Plugin.Abstractions.dll"
$shippedContract = "$pluginDst\OpsisNewsCrawler.Plugin.Abstractions.dll"
if (-not (Test-Path $canonicalContract)) { throw 'Missing canonical Opsis 2.8 ABI DLL' }
if ((Get-FileHash $canonicalContract -Algorithm SHA256).Hash -ne (Get-FileHash $shippedContract -Algorithm SHA256).Hash) {
  throw 'Plugin payload has a different ABI contract DLL from the pinned Opsis 2.8 source. Do not package.'
}
# Guard: the plugin contract keeps the assembly version of the copy that Opsis has loaded (otherwise the plugin cannot bind to it).
$contract = [Reflection.AssemblyName]::GetAssemblyName("$pluginDst\OpsisNewsCrawler.Plugin.Abstractions.dll").Version
# The Opsis project folder is a sibling of this one (currently still named MotionDeskStudio on disk; "Opsis" is tried first).
$opsisCopy = @("$r\..\Opsis\lib", "$r\..\Opsis\libs", "$r\..\MotionDeskStudio\libs") | ForEach-Object { Join-Path $_ "OpsisNewsCrawler.Plugin.Abstractions.dll" } | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($opsisCopy) {
  $opsisVersion = [Reflection.AssemblyName]::GetAssemblyName($opsisCopy).Version
  if ($contract -ne $opsisVersion) { throw "plugin contract is $contract but the copy in Opsis\libs is $opsisVersion" }
  $contractHash = (Get-FileHash "$pluginDst\OpsisNewsCrawler.Plugin.Abstractions.dll" -Algorithm SHA256).Hash
  $opsisHash = (Get-FileHash $opsisCopy -Algorithm SHA256).Hash
  if ($contractHash -ne $opsisHash) {
    throw 'Opsis and Crawler have different contract binaries even though their AssemblyVersion matches. Use a coordinated Opsis 2.8 + Crawler + Chronikologio rollout.'
  }
}
"contract version: $contract"
# Opsis 2.8.x shares Opsis.UI from its default AssemblyLoadContext. The plugin
# must ship the exact DLL to avoid an older UI implementation at runtime.
$ui = "$pluginDst\Opsis.UI.dll"
if (-not (Test-Path $ui)) { throw "Missing Opsis.UI.dll in plugin payload" }
$uiAssembly = [Reflection.AssemblyName]::GetAssemblyName($ui).Version.ToString()
$uiVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($ui).FileVersion
if ($uiAssembly -ne '0.1.0.0' -or $uiVersion -ne '0.6.0.0') {
  throw "Opsis.UI mismatch: AssemblyVersion=$uiAssembly, FileVersion=$uiVersion (expected 0.1.0.0 / 0.6.0.0)"
}
$opsisUi = @("$r\..\Opsis\lib\Opsis.UI.dll", "$r\..\Opsis\libs\Opsis.UI.dll") | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($opsisUi) {
  $expectedHash = (Get-FileHash $opsisUi -Algorithm SHA256).Hash
  $actualHash = (Get-FileHash $ui -Algorithm SHA256).Hash
  if ($expectedHash -ne $actualHash) { throw 'Opsis.UI.dll differs from the sibling Opsis 2.8 host - stage a matching DLL before packaging.' }
}
"Opsis.UI: $uiAssembly / file $uiVersion"

# Any installed Inno Setup (5, 6, 7...): the newest real install folder wins (the ISCC.exe on the PATH is, on a CI runner, a shim of an older version); the PATH is the last resort.
$iscc = @("$env:LOCALAPPDATA\Programs", $env:ProgramFiles, ${env:ProgramFiles(x86)}) |
  Where-Object { $_ -and (Test-Path $_) } |
  ForEach-Object { Get-ChildItem $_ -Directory -Filter "Inno Setup*" -ErrorAction SilentlyContinue } |
  Sort-Object Name -Descending |
  ForEach-Object { Join-Path $_.FullName "ISCC.exe" } |
  Where-Object { Test-Path $_ } |
  Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
if (-not $iscc) { throw "Inno Setup (ISCC.exe) not found - install it from https://jrsoftware.org/isdl.php" }
"Inno Setup: $iscc"
& $iscc "/DBinDir=Output\stage" "$r\OpsisNewsCrawler.iss" | Select-String "Error|Successful"
if ($LASTEXITCODE -ne 0) { throw "installer compile failed" }

$h = (Get-FileHash "$r\Output\OpsisNewsCrawler-Setup.exe" -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText("$r\Output\OpsisNewsCrawler-Setup.exe.sha256", "$h  OpsisNewsCrawler-Setup.exe`n")
"setup: $r\Output\OpsisNewsCrawler-Setup.exe  sha256 $h"

# Portable zip: the same files as the installer's program folder without the Opsis plugin copy (needs the .NET 11 Desktop Runtime).
$ver = (Get-Item "$stage\OpsisNewsCrawler.exe").VersionInfo.ProductVersion.Split('+')[0]
$portable = "$r\Output\OpsisNewsCrawler-$ver-portable.zip"
if (Test-Path $portable) { Remove-Item $portable -Force }
$tmp = "$r\Output\portable-stage\OpsisNewsCrawler-$ver"
if (Test-Path "$r\Output\portable-stage") { Remove-Item "$r\Output\portable-stage" -Recurse -Force }
robocopy $stage $tmp /E /XD plugins /NFL /NDL /NJH /NJS | Out-Null
Compress-Archive -Path "$r\Output\portable-stage\OpsisNewsCrawler-$ver" -DestinationPath $portable
Remove-Item "$r\Output\portable-stage" -Recurse -Force
$ph = (Get-FileHash $portable -Algorithm SHA256).Hash.ToLower()
[IO.File]::WriteAllText("$portable.sha256", "$ph  OpsisNewsCrawler-$ver-portable.zip`n")

# Everything to publish, in one folder (release\<version>-assets): installer, portable zip, their checksums, release notes (el + en).
$assets = "$r\release\$ver-assets"
if (Test-Path $assets) { Remove-Item $assets -Recurse -Force }
New-Item -ItemType Directory -Force $assets | Out-Null
Copy-Item "$r\Output\OpsisNewsCrawler-Setup.exe", "$r\Output\OpsisNewsCrawler-Setup.exe.sha256", $portable, "$portable.sha256" $assets
# Ship the explicit rollback helper; it never runs silently during setup.
Copy-Item "$r\tools\restore-opsis-crawler.ps1" "$assets\OpsisNewsCrawler-$ver-rollback.ps1"
$rollbackHash = (Get-FileHash "$assets\OpsisNewsCrawler-$ver-rollback.ps1" -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText("$assets\OpsisNewsCrawler-$ver-rollback.ps1.sha256", "$rollbackHash  OpsisNewsCrawler-$ver-rollback.ps1" + [Environment]::NewLine)

# Additional unambiguous name for this .NET 11 candidate. Preserve canonical E2E filename.
$net11Setup = "$assets\OpsisNewsCrawler-$ver-NET11-Setup.exe"
Copy-Item "$assets\OpsisNewsCrawler-Setup.exe" $net11Setup
[IO.File]::WriteAllText("$net11Setup.sha256", "$h  OpsisNewsCrawler-$ver-NET11-Setup.exe`n")
$notes = "$r\docs\releases\RELEASE-$ver.md"
if (Test-Path $notes) { Copy-Item $notes "$assets\RELEASE-NOTES.md" } else { "note: $notes is missing - write the release notes before publishing" }
if (Test-Path "$assets\RELEASE-NOTES.md") {
  Add-Content "$assets\RELEASE-NOTES.md" ("`r`n## SHA-256`r`n`r`n```````r`n$h  OpsisNewsCrawler-Setup.exe`r`n$ph  OpsisNewsCrawler-$ver-portable.zip`r`n```````r`n")
}
"assets: $assets"

# Only after successful package creation: keep the newest generated release artifacts.
# The cleanup helper has strict path and name checks; no source/backups or GitHub releases are touched.
& "$PSScriptRoot\prune-generated-releases.ps1" -Root $r -KeepVersion $ver -Apply

# Best effort: bring bin\ (what the Run-...bat starts) up to date too; files in use are skipped.
robocopy $stage "$r\bin" /MIR /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { "note: bin\ could not be refreshed completely (program running?) - the installer is fine" }
$global:LASTEXITCODE = 0
