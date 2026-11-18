# Review of CLAUDE-CODE-INSTRUCTIONS.md for the Crawler (18/11/2026)

Which items of the coordinator's instruction file apply to this repository, and their status. "Done" means in the code and covered by tests where noted; nothing here was re-checked on a real Windows desktop unless stated.

| Item | Subject | Status | Notes |
|---|---|---|---|
| A, 7b | CI `permissions: contents: read` | Done | `.github/workflows/windows-ci.yml`. Green GitHub run: not verified (nothing pushed). |
| E1, E2 | Taskbar integration (flicker, placement) | Done, differently | Solved at the cause (helper window, 0.22.x/0.24.0); `IShellState` and `SetWinEventHook` were not adopted. Measured on a real taskbar only for tray overflow; Notification Center, Start, Search, Widgets, second taskbar, auto-hide, several monitors and 150% DPI are NOT VERIFIED. |
| E3 | "On this day" story flow (`HistoryPlacement`) | Not done | Needs a design first. |
| E4 | Live channels | Not done | Needs a channel list and a design first. |
| E5 | Unified search | Not done | |
| E6 | Motion | Done, differently | 0.22.x/0.24.0. |
| E7 | Contrast | Done, differently | `ContrastTextTests`. |
| E8 | Source health, `Retry-After`, data age, accessibility, DPI matrix, `CopyScalars`, presets, SHA-pinned Actions | Not done | 2, 3, 5, 6, 7, 8, 9, 10, 11 outstanding. |
| E9 | Housekeeping | See E8 | |
| ST1 | Final sigma in `NewsKeywordFilter`, reserved names in `DownloadPolicy.IsSafeFileName`, `WindowsPath` | Not done | Prepared on the remote branch `origin/claude/hardening`; not merged. |
| I1.1 | Yahoo wording in HANDOFF | Done (HANDOFF) | The exception line in `CLAUDE.md` is still missing. |
| I1.2 | Leaflet SRI | Done | `integrity` + `crossorigin` on Leaflet 1.9.4 css/js in `WeatherMapForm` and `HazardDetailForm` (hashes verified against unpkg). Bundling Leaflet locally and restricting WebView2 navigation: not done. |
| I6 | Yahoo stays as opt-in (user decision) | Partly | Yahoo is kept, not removed. Consent dialog, circuit breaker and bring-your-own-key provider: not done. |
| Rename (TASKS-MASTER 39) | giorstergiopoulos-design to gs-applabs | Done | Trusted-URL checks accept both names; CHANGELOG and old release notes keep the old name; consistency test in `ReleaseHygieneTests`. |
