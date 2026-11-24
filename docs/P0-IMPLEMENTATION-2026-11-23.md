# P0 implementation batch — 2026-11-23

Branch: `p0/stability-feed-contract-ui-audit-20261123`

This batch implements source-level changes for all **15 original P0** items plus an early, optional diagnostic slice of **P1 item #20**, *without* claiming that every acceptance criterion or release QA gate is complete. Version 0.24.1 and the plugin contract assembly identity 0.8.0.0 are intentionally unchanged until a coordinated Opsis host/plugin rollout.

| Original item | Area | Code status | Remaining release gate |
|---|---|---|---|
| #1 | RSS source health | Added a visible health/diagnostics dialog with last OK/try, failures, retry due, error | Visual review in all supported languages and dark/light themes |
| #2 | Retry policy | Exponential bounded backoff and HTTP 429/503 Retry-After | Broader real-feed network fault testing |
| #3 | Concurrency | At most 6 global and 2 per RSS host; deduplicated URLs | Load test with many distinct real feeds |
| #4 | Network limits | Stream-read bound of 5 MB, including chunked/unknown length | Test gzip/redirect policy separately |
| #9 | Parser fuzzing | Random malformed XML and oversized payload regression tests | Extended structured XML fuzz corpus |
| #11 | Refresh scheduler | Central single-flight scheduler for timer-driven news, quotes, weather, hazards, alerts | Consolidate remaining non-timer/manual refresh paths |
| #12 | SSD writes | Avoid identical RSS/weather payload cache rewrites; quote history is write-on-change | System-level SSD trace for extended idle + market-open sessions |
| #14 | Idle | Settings preview and market source status timers stop when hidden | Audit all other forms and third-party WebView2 processes |
| #15 | GDI | Repeated ticker frame/dispose smoke test + live GDI counter | 24–72h sustained real desktop soak not yet run |
| #17 | Shutdown | Cached cancellation token survives CTS disposal; stop cancels/halts periodic timers and scheduler | Extended stop/start and slow I/O race tests |
| #18 | TickerHost | Split news, plugin, market, weather, hazard logic into partial modules | Full service interfaces/DI refactor is future work |
| #19 | SettingsPanel | Split markets, appearance, history, breaking, weather, read-later, sources into partial modules | Refactor shared field dependencies into independent panels |
| P1 #20 (brought forward) | Diagnostics | CPU, process memory, GDI, active refreshes, cache writes/skips, RSS health | Per-provider network bytes and FPS telemetry |
| #42 | Rich Feed metadata | Preserves Kind, ImageId, CountryCode, LanguageCode, HistoricalYear, DetailId, Priority in NewsItem.PluginMetadata | Display/click consumers must opt in to the new metadata |
| #43 | Async plugin feed | AsyncRichFeedSnapshot lets plugin producers refresh in background and expose immutable synchronous GetItems | Updated contract DLL needed in Opsis/Chronikologio before their plugins use the new type |
| #49 | Contract tests | Frozen 0.8.0.0 assembly identity; baseline public surface + async compatibility regression tests | Cross-repo host/Chronikologio integration test with identical packaged DLL |

## Test procedure

- GitHub Windows build and test workflow: `dotnet test` for Core/Plugin plus packaging checks.
- Added `P0RssSafetyTests`, `P0RefreshSchedulerTests`, `P0SsdWritesTests`, `P0GdiSoakTests`, `P0AsyncFeedCompatibilityTests`; extended `RichFeedTests`.
- No CI run can establish a genuine multi-day soak in this session. The shorter GDI regression does not replace it.

## Deployment safety / Opsis.UI 6

Do **not** copy the changed `OpsisNewsCrawler.Plugin.Abstractions.dll` directly into Opsis without updating the matching plugin copy and running the host+Chronikologio integration tests. Both old and new DLLs still have `AssemblyVersion=0.8.0.0`, but the new `AsyncRichFeedSnapshot` type exists only in the updated DLL. Older plugin usage remains valid because the established APIs and constructors have not been changed.

Opsis.UI 6 is a **different** DLL (`Opsis.UI.dll`, assembly identity 0.1.0.0). This P0 batch deliberately keeps the existing bundled UI dependency and reuses existing shared flat controls rather than silently upgrading the host or requiring new UI6 members.

The diagnostics screen is a first-pass theme-aware UI; before release, match it to the latest shared Opsis.UI controls and verify keyboard access, RTL, multi-monitor DPI, high contrast and all translations.

## Release decision

This is a **candidate branch**, not a completed application release. Do not tag/bump the version or merge into `main` until Windows CI is green, critical network/cancellation regressions are reviewed, visual checks are done and Opsis host compatibility is validated.
