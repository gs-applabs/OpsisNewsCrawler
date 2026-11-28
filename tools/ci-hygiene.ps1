# Repository hygiene gate: one current release note, one progress history, no stale tracked installers.
# Read-only. Run from any directory with: pwsh ./tools/ci-hygiene.ps1
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Get-Content (Join-Path $repo 'src/OpsisNewsCrawler.App/OpsisNewsCrawler.App.csproj') -Raw
if ($project -notmatch '<Version>([0-9]+\.[0-9]+\.[0-9]+)</Version>') { throw 'App project has no product version.' }
$version = $Matches[1]
$releaseDir = Join-Path $repo 'docs/releases'
$notes = @(Get-ChildItem $releaseDir -Filter 'RELEASE-*.md' -File)
if ($notes.Count -ne 1 -or $notes[0].Name -ne "RELEASE-$version.md") {
    throw "Only docs/releases/RELEASE-$version.md may remain. Found: $(($notes.Name -join ', '))"
}
$oldProgress = @(
 'docs/TASKS.md', 'docs/HANDOFF.md', 'docs/PLAN-INTAKE.md',
 'docs/INSTRUCTIONS-REVIEW.md', 'docs/REVIEW-2026-10.md',
 'docs/P0-IMPLEMENTATION-2026-11-23.md', 'docs/P2-COMPATIBILITY-AND-RELEASE.md',
 'docs/NET11-OPSIS-280-COMPAT.md', 'docs/P0-OPSIS-28-HOST-INTEGRATION.md'
)
foreach ($path in $oldProgress) {
    if (Test-Path (Join-Path $repo $path)) { throw "Old progress file reintroduced: $path. Add progress to CHANGELOG.md instead." }
}
$history = Get-Content (Join-Path $repo 'CHANGELOG.md') -Raw
if (-not $history.Contains('Consolidated historical progress and release-note archive') -or
    -not $history.Contains("## $version")) { throw 'Missing canonical CHANGELOG.md history.' }
$guide = Get-Content (Join-Path $repo 'CLAUDE.md') -Raw
foreach ($path in @('docs/TASKS.md','docs/HANDOFF.md')) {
    if ($guide.Contains($path)) { throw "Stale CLAUDE.md reference: $path" }
}
$contract = Join-Path $repo 'libs/OpsisNewsCrawler.Plugin.Abstractions.dll'
$ui = Join-Path $repo 'libs/Opsis.UI.dll'
if (([Reflection.AssemblyName]::GetAssemblyName($contract).Version.ToString()) -ne '1.0.0.0') { throw 'Wrong canonical shared ABI DLL.' }
if (([Reflection.AssemblyName]::GetAssemblyName($ui).Version.ToString()) -ne '0.1.0.0') { throw 'Wrong UI6 DLL.' }
$sourceFiles = @(git -C $repo ls-files)
$badAssets = @($sourceFiles | Where-Object {
    ($_ -match '^(?:Output|release|bin|obj)/') -or
    ($_ -match '\.(?:exe|msi|msix|zip|7z)$' -and $_ -notmatch '^tests/(fixtures|assets)/')
})
if ($badAssets.Count -gt 0) { throw "Generated or obsolete binaries must not be Git-tracked: $($badAssets -join ', ')" }
Write-Host "PASS: one $version release note, canonical history, no old progress docs or tracked installers, pinned contract/UI6."

# Source-overlap inventory: read-only lexical evidence, not semantic clone detection.
# Exclude generated/build output by using the tracked tree. Do not automatically delete or
# fail on shared patterns: compatibility adapters and tests can legitimately look alike.
$cs = @($sourceFiles | Where-Object { $_ -match '^(src|tests)/.*\.cs$' })
$hashes = @(foreach ($path in $cs) {
    [pscustomobject]@{ Path = $path; Hash = (Get-FileHash -LiteralPath (Join-Path $repo $path) -Algorithm SHA256).Hash }
})
$identical = @($hashes | Group-Object Hash | Where-Object Count -gt 1)
$windows = [System.Collections.Generic.Dictionary[string,System.Collections.Generic.List[string]]]::new([StringComparer]::Ordinal)
foreach ($path in $cs) {
    $significant = [System.Collections.Generic.List[object]]::new()
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines((Join-Path $repo $path))) {
        $lineNumber++
        $trim = $line.Trim()
        if (!$trim -or $trim.StartsWith('//') -or $trim -match '^[{};]+$') { continue }
        $significant.Add([pscustomobject]@{ Text = $trim; Line = $lineNumber })
    }
    for ($i = 0; $i + 11 -lt $significant.Count; $i++) {
        $key = ($significant.GetRange($i,12) | ForEach-Object Text) -join "`n"
        if ($key.Length -lt 250) { continue }
        if (!$windows.ContainsKey($key)) { $windows[$key] = [System.Collections.Generic.List[string]]::new() }
        $windows[$key].Add(('{0}:{1}' -f $path,$significant[$i].Line))
    }
}
$overlaps = @($windows.GetEnumerator() | Where-Object { $_.Value.Count -gt 1 } |
    Sort-Object -Property @{Expression={$_.Value.Count};Descending=$true}, @{Expression={$_.Value[0]};Descending=$false})
$report = [System.Collections.Generic.List[string]]::new()
$report.Add("Git checkout: $(git -C $repo rev-parse HEAD)")
$report.Add("Tracked C# files: $($cs.Count); exact whole-file duplicate groups: $($identical.Count)")
foreach ($group in $identical) { $report.Add("Exact: $($group.Group.Path -join ' | ')") }
$report.Add("Repeated 12-significant-line windows: $($overlaps.Count); showing first 200 groups (overlapping windows may describe the same clone).")
foreach ($group in ($overlaps | Select-Object -First 200)) { $report.Add("Review: $($group.Value -join ' | ')") }
$report.Add('Lexical scan only: may miss renamed/reordered implementations and may flag legitimate adapters or tests. Review ownership ledger and call sites before adding or removing functionality.')
if ($env:RUNNER_TEMP) {
    $dir = Join-Path $env:RUNNER_TEMP 'opsis-ci-audit'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $report | Set-Content -LiteralPath (Join-Path $dir 'source-overlap.txt') -Encoding utf8
}
$report | Select-Object -First 3 | ForEach-Object { Write-Host $_ }
