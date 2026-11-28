# Performance tiers of Opsis News Crawler (0.19.0)

The program adapts itself to the PC: **Low**, **Balanced** or **High**. Settings > General > *Performance*: Automatic (default) / Low / Balanced / High; the card shows what was detected and why this tier is in use.

## What is detected (`Windows/HardwareDetector.cs`, a few Win32 calls, no WMI, no hardware monitor)
| Reading | How |
|---|---|
| logical processors | `Environment.ProcessorCount` |
| physical memory | `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` |
| real graphics adapter | `EnumDisplayDevices`: the primary display's adapter is *not* a software renderer ("Microsoft Basic Render Driver / Display", Remote Display, RDP, Hyper-V Video, VMware SVGA, Parallels, VirtualBox, Citrix) |
| battery / battery saver | `GetSystemPowerStatus` (AC line, "no battery" for desktops, system status flag 1 = battery saver) |

## Which tier (`Core/Performance.cs`, `PerformanceClassifier`)
1. battery saver on → **Low**; no real graphics adapter → **Low**; 2 or fewer processors → **Low**; less than 6 GB → **Low**
2. on battery → **Balanced** (the full effects are for a plugged-in PC); fewer than 8 processors or less than 12 GB → **Balanced**
3. otherwise **High**

The free GitHub runner (2 vCPU, ~7 GB, no GPU) is therefore a *Low* PC — the natural low-spec test environment. `PerformanceTests` simulate the runner, a laptop, a strong desktop, battery and a failing detection.

## What each tier changes (one table: `PerformanceProfile.For`, applied in `TickerHost.ApplyPerformance`)
| | Low | Balanced | High |
|---|---|---|---|
| scroll step | every 33 ms (~30 fps) | 20 ms (50 fps) | 15 ms (66 fps) |
| taskbar polling | 90 ms | 45 ms | 30 ms |
| splash / minimise animations | simple fade | full | full |
| refresh of news, prices, weather, disasters | intervals × 2 | as set | as set |

The scroll speed the user sees never changes (the scroll is time based); the user's own intervals are never made shorter. The notification-area icon of the program is always still (since 0.19.2); it shows the live weather and the highest alert.

## Automatic changes
- Evaluated at the start, when the settings change and **every minute**.
- **Down is immediate** (battery saver, unplugged, slow PC); **up needs two equal readings in a row** (about 2 minutes), so a PC does not flicker between two tiers. Unplugged laptop → Balanced; plugged in again → back to High by itself.
- Order of precedence: test variable `OPSISNEWSCRAWLER_FORCE_TIER=Low|Balanced|High` → the user's setting (Low / Balanced / High) → inside Opsis the tier Opsis reports (`SetHardwareProfile`) while the setting is Automatic → detection. A failing detection never throws: the tier in use is kept (Balanced at the very start).
- Every change is written to `crawler.log` ("Performance tier: Low (gpu); specs: …").

## Not covered
Real graphics cards, several monitors, per-monitor DPI and measurements of CPU / RAM at idle per tier were not measured on a real PC (NOT VERIFIED).

## 0.30.0 long-running resource guards

Favicon cache: maximum 200 source icons, up to 400 derived variants, immediate disposal during eviction under the same lock as painting; at most 4 downloads, no waiting queue, 12-second entire operation deadline and bounded streamed bodies. Ticker canvas owns/disposes animation timer; fade ramps limited to 64 widths. Generated cache JSON retention: 128 files / 64 MiB after pruning, maximum 16 MiB per entry; excludes user files, backups and archives. Cache hashes bounded, no second payload serialization. Active extra categories only, stale category results rejected after stop/restart; secondary RSS provider disposed. Archive headline changes pruned independently of story count. Disk cleanup is best effort; existing files are trimmed on new entries or ten-minute maintenance on saves, not a hard filesystem quota. Actual memory, CPU and write rates must be measured, not inferred from these limits.

Additional lifetime guards: stop cancels in-flight primary RSS, quotes, history backfill, hazard and weather alert operations; stale callbacks cannot publish after restart; retries use session token. Feed states for deleted sources are pruned on existing minute tick, disabled icons do not initiate downloads. Added temp-directory retention tests and eight rounds of icon replacement/painting (1,920 hosts, two variants) without forced garbage collection. GDI counters are supplementary, not a process RAM or physical SSD endurance measurement. Preserve user portfolio, quote history and settings; no global disk cleanup or forced periodic GC.

## Pass 9 — quote write coalescing and measured runtime

QuoteStore dirty flags for closes/candles/backfill avoid serialization and file reads for identical quotes. Providers call SaveIfDue (one-minute interval), the existing minute tick saves pending changes, and Stop/Dispose flushes them. Explicit Save remains synchronous for compatibility. Save holds the store gate through commit so older provider snapshots cannot overwrite newer ones. SafeFile serializes atomic writes and reports successful logical payload bytes, not physical NAND writes. Settings skip identical content. Yahoo history limits attempts (not successes), spaces even failed responses and stops for the day on access/rate-limit rejection. Retention of existing quote history is unchanged. Tests cover 10,000 identical records, minute coalescing, shutdown-style flush and concurrent atomic writes.

New Windows resource soak workflow runs the compiled app for 3 minutes on PRs and supports manual Low/Balanced/High runs up to 240 minutes (runner time limit). Samples every 5 seconds: normalized CPU, private/working-set/managed memory, GDI/USER/total handles, threads, active refreshes, rendered frames, RSS bytes, cache writes/skips, SafeFile logical writes/bytes. CSV saved once at exit, no persistent monitoring in normal use. Live network and Windows desktop conditions may affect the results. Broad smoke gates are not production resource targets; no 8/24h validation yet.

## Pass 10 — demand-driven activation

The existing host/scheduler is the sole timer owner. Demand depends on actually enabled/unsnoozed bars, connected monitors, visible mini bar, visible news/market/weather widgets, portfolio/analysis and enabled price alerts, plus explicitly selected icon data and weather alerts. Disabled network services stop their timer and cancel their linked session lease. News category-only consumers use the extra provider without loading unrelated active category. Update timer is only started when enabled. QuoteService/history is lazy; cached RSS/weather are loaded at first demand; standalone SettingsForm is lazy and opens normally on first use/first run. Retain the slow host maintenance tick for hardware, family discovery and pending persistence. No second scheduler. Runtime startup and toggle behavior must pass Windows CI; snapshots and audit paths remain explicit.

Pass 10 follow-up: automatic update delay/retry/download uses cancellable lease; manual update still works. Widget-only quote/weather results can reach the UI without a visible main ticker HWND. Closing the host closes the market analysis window. Regression test starts a host with bars and updates disabled, asserts all refresh timers inactive and no market store, toggles automatic update on/off before the scheduled request and checks clean shutdown. Previous 4245187 resource soak passed (Low, 3 minutes, 36 samples): mean CPU 0.43% across logical cores, private memory +3.05 MiB, GDI delta 0, SafeFile +0.053 MiB during steady window. That is not a long-duration result and does not measure new demand changes.

## Pass 11 — activation regression fixes

6e10069 Windows plugin contract run failed RuntimeDemandTests because legacy AppConfig.Normalize forcibly enabled automatic updates for versionless settings. Changed migration to preserve explicit opt-out; missing key retains enabled default. The existing RefreshScheduler now offers RunLatest for configuration/activation transitions (one pending callback per key, ordinary timer ticks still never queue). All host periodic and explicit refresh paths share this scheduler and current demand lease. News category demand changes cancel the prior lease; only latest pending work runs. Hidden started bars skip layout rebuilds; hidden canvases skip favicon requests. Added 1,000 activation requests regression (one pending execution, no overlap). Current CI required.

## Pass 12 — shared providers and bounded cross-app event dispatch

Use one host RSS provider for main and secondary categories; recently validated feeds can be reused for 30 seconds, preserving category-specific publisher names and conditional GET outside that window. Manual provider callers retain the original behavior. Main/extra RSS and market events use the existing bounded dispatcher; initialize only on first actual publication. Dispatcher cancellation no longer races disposal of its token. Host weather budget is 12 seconds total; fallback requests only missing cities, merge requested results with host priority and retain prior readings with a partial-failure notice. A successful shared observation is not duplicated to disk. Weather retry is single-flight. Added deterministic tests for one download across two categories and partial coverage. Host integration PR #18 still requires validation against the current Crawler branch (its default workflow currently points to the older 0.28.0 branch).

Pass 15: the existing Windows resource soak workflow can run 30-minute Low measurements when PR label `extended-soak` is set; normal PR smoke remains 3 minutes, and manual dispatch still supports up to 240 minutes. No duplicate workflow or production monitoring timer. A label-triggered run is only requested for the dedicated label. Do not equate a 30-minute run with an 8/24h certification.

## Pass 17 — separate idle demand gate

The 30-minute Low run at 388b471 passed: 360 samples, mean CPU 0.64% across logical cores, private memory +12.21 MiB, GDI delta 0, SafeFile +0.074 MiB logical payload. Pass 16 at 655b8b5 passed its separate 3-minute soak and all installer jobs. Add a two-minute isolated idle scenario with optional consumers explicitly disabled. Extend the existing CI-only probe (no production timer) to sample active refresh services, refresh timers, update state, lazy market initialization and ticker visibility; reject optional activity, RSS bytes or steady-window frame growth. Idle and active reports use distinct filenames. New Windows evidence required; this is not an 8/24h certification.

Pass 18: the Breaking widget is an explicit news consumer rather than an accidental dependency on visible ticker bars. It shares the existing news scheduler and assessment, no second timer or provider, and has a bounded snapshot. Widget-only activation has no bar/audio side effects. Existing widget font now has explicit disposal ownership. Runtime regression and final soak pending.

Pass 19: coalesced, cancellable sleep/resume refresh and demand-scoped delayed retries; canceled generations cannot block replacement retries. History opt-out cancels in-flight optional backfill without stopping quote display. No new periodic production timers. Windows validation pending.

Pass 20: source edits cancel stale network generation, cache and selected user data follow the existing explicit probe directory; bounded local reads protect cache and family feed from file growth after stat. Production defaults unchanged; existing offline cache preserved. New tests pending Windows validation.

Pass 17 final measurement (46105509202): active Low 30 min, 360 samples, CPU 0.44% all cores, private +74.31 MiB, GDI delta 0, SafeFile +0.401 MiB; idle Low 2 min, 24 samples, CPU 0.01%, private +0.07 MiB, GDI delta 0, no logical writes and no optional service/RSS/render demand. Active private growth passed the broad 256 MiB smoke cap but is a P0 investigation; compare identical configs and memory/handle trends, not just endpoint deltas. Exact subsequent candidate still unverified.

Release handoff: Pass 23 build and real OPSIS host smoke passed, not final resource certification. Prior Pass 21 run 46108370603 still active at handoff; review actual final candidate active/idle soak, CPU/private bytes/GDI/logical writes and sustained memory trend. Keep Pass 17 +74.31 MiB private growth as investigation baseline, not a leak-free claim. Exact run links and gates in CHANGELOG.
