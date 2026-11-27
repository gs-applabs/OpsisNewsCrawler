# Crawler 0.30.0 / OPSIS 3.0 RC quality gate

Baseline: Crawler main 811be98a11cdb0436ff2ecf54846cabcf06deea4 (project version 0.30.0); OPSIS unified RC c68f2c086575f7c6f8cba0cf5a70e78ad9dca0b2.

Both shared DLL Git blob hashes match the host RC: Opsis.UI 4a5b4e0a1e1ec56780a1f581b79f0b272e3b52bd; plugin abstractions 7beb06349daef31ae4c80f2a47a32c7477f1e50d. Host still calls the optional SetOpsisP0Environment and host settings interfaces. This is static compatibility evidence, not an actual host launch.

Fixes: audit entry points return real exit status; scroll audit records exceptions and refuses empty checks; CI verifies scroll report; family feed re-registers after external removal, updates metadata-only changes, rejects non-integral schema; tray Settings falls back to standalone settings when navigation is unsubscribed.

Remaining acceptance: Windows CI results, installer E2E on exact commit, every functional button (including dialogs and network outcomes), live installed OPSIS 3 host lifecycle, permissions/trust/reload, safe mode transition, shared weather/DeskStrip ownership, mixed standalone/host and sleep/resume, DPI and multi-monitor, CPU/GDI/SSD soak. Do not equate unit tests or offscreen layout audits with complete user-facing E2E. No merge/release before review.

Installer E2E now triggers on pull requests to main/master, checks scroll on each tested performance tier, and treats crash/timeout/nonzero exit as a failure. OPSIS R28 build at the pinned host commit failed with CS0234 in MainWindow.Studio.cs:97 (`Appearance.Button` resolves to MotionDesk.Appearance rather than the WinForms enum); host launch remains blocked at that commit. No host files modified in this PR.
