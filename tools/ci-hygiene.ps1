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
