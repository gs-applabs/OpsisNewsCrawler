# Remove ONLY superseded generated release artifacts after a successful build.
# Without -Apply, prints a preview and changes nothing. Never modifies source files,
# Git history, published releases, app settings or recovery backups.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Root,
    [Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$KeepVersion,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$base = [IO.Path]::GetFullPath($Root)
if (!(Test-Path -LiteralPath $base -PathType Container)) { throw "Missing project root: $base" }
$release = Join-Path $base 'release'
$output = Join-Path $base 'Output'
function SafeDirectory([string]$path) {
    if (!(Test-Path -LiteralPath $path -PathType Container)) { return $false }
    if ((Get-Item -LiteralPath $path -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to traverse reparse-point directory: $path"
    }
    return $true
}
$items = [System.Collections.Generic.List[System.IO.FileSystemInfo]]::new()
if (SafeDirectory $release) {
    foreach ($d in Get-ChildItem -LiteralPath $release -Directory) {
        if ($d.Name -match '^(?<v>\d+\.\d+\.\d+)-assets$' -and $Matches['v'] -ne $KeepVersion) {
            if ($d.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing to remove linked directory: $($d.FullName)" }
            $items.Add($d)
        }
    }
}
if (SafeDirectory $output) {
    foreach ($f in Get-ChildItem -LiteralPath $output -File) {
        if ($f.Name -match '^OpsisNewsCrawler-(?<v>\d+\.\d+\.\d+)-(portable\.zip|NET11-Setup\.exe)(\.sha256)?$' -and $Matches['v'] -ne $KeepVersion) {
            if ($f.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Refusing to remove linked file: $($f.FullName)" }
            $items.Add($f)
        }
    }
}
foreach ($item in $items) {
    if ($Apply) {
        Remove-Item -LiteralPath $item.FullName -Recurse -Force
        Write-Host "Deleted old generated artifact: $($item.Name)"
    } else {
        Write-Host "Would delete: $($item.Name)"
    }
}
$removed = if ($Apply) { $items.Count } else { 0 }
Write-Host "Release retention: keep $KeepVersion; removed $removed generated item(s); preview mode=$(-not $Apply)."
