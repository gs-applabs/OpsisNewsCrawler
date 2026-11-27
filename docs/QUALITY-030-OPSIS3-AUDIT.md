# Crawler 0.30.0 / OPSIS 3.0 RC quality gate

Baseline: Crawler main 811be98a11cdb0436ff2ecf54846cabcf06deea4 (project version 0.30.0); OPSIS unified RC c68f2c086575f7c6f8cba0cf5a70e78ad9dca0b2.

Both shared DLL Git blob hashes match the host RC: Opsis.UI 4a5b4e0a1e1ec56780a1f581b79f0b272e3b52bd; plugin abstractions 7beb06349daef31ae4c80f2a47a32c7477f1e50d. Host still calls the optional SetOpsisP0Environment and host settings interfaces. This is static compatibility evidence, not an actual host launch.

Fixes: audit entry points return real exit status; scroll audit records exceptions and refuses empty checks; CI verifies scroll report; family feed re-registers after external removal, updates metadata-only changes, rejects non-integral schema; tray Settings falls back to standalone settings when navigation is unsubscribed.

Remaining acceptance: Windows CI results, installer E2E on exact commit, every functional button (including dialogs and network outcomes), live installed OPSIS 3 host lifecycle, permissions/trust/reload, safe mode transition, shared weather/DeskStrip ownership, mixed standalone/host and sleep/resume, DPI and multi-monitor, CPU/GDI/SSD soak. Do not equate unit tests or offscreen layout audits with complete user-facing E2E. No merge/release before review.

Installer E2E now triggers on pull requests to main/master, checks scroll on each tested performance tier, and treats crash/timeout/nonzero exit as a failure. OPSIS R28 build at the pinned host commit failed with CS0234 in MainWindow.Studio.cs:97 (`Appearance.Button` resolves to MotionDesk.Appearance rather than the WinForms enum); host launch remains blocked at that commit. No host files modified in this PR.

## Current OPSIS 3.0 roadmap and source baseline — R54

Source: https://github.com/gs-applabs/Opsis/tree/fc17d81020e0200eb038c72807192c2ce494ad5f (R54 commit; PR #18 description still says R45). Authoritative roadmap: https://github.com/gs-applabs/Opsis/blob/fc17d81020e0200eb038c72807192c2ce494ad5f/docs/SOURCE-CONSOLIDATION.md . R53 reconciled 48/48 recorded source overlaps, not all release gates. Shared DLL blob identities remain identical to Crawler baseline.

Crawler acceptance aligned to MP2/MP3/MP4/MP6/MP7/MP9 and R48 discoverability: explicit user preference and deterministic compatible healthy provider, no duplicate news polling or notifications, bounded cancellation and stop before fallback, offline cache, preserve widget placement/preferences, show active provider/permissions/disabled reason, compatible shared UI tokens and accessibility, searchable feature catalog with contextual help and truthful version history, test cold and hosted launch, standalone versus hosted ownership, Safe Mode, DeskStrip, sleep/resume, crash/disable/re-enable/uninstall, version mismatch and restart without leaked timers or shared settings writes. These are acceptance requirements, not claimed implementations. Preserve plugin DLL identity 1.0.0.0; do not activate unverified SDK2 capabilities or fork common UI libraries.

Host compatibility workflow at R54 defaults to Crawler 0.28.0 (legacy branch), not current Crawler 0.30.0. OPSIS PR #19 now proposes defaulting to current Crawler main, with exact commit override for release certification. For this Crawler PR, select its actual reviewed head explicitly when running host compatibility. Latest R54 Windows audit failed; real host launch remains unverified. No host merge or production release implied.
