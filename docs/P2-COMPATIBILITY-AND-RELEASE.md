# P2 0.28.0 deployment sequence and compatibility checklist

1. Build/tests: Core, Windows, plugin, localization/snapshot audit; confirm 0.28.0 versions.
2. Use isolated Windows runners to produce setup and portable ZIP and run fresh/update-helper installer e2e. Check SHA-256 and archive source from the exact commit.
3. Staged compatibility gate: in the Opsis host build replace the **single host copy** of `OpsisNewsCrawler.Plugin.Abstractions.dll` with the updated P1/P2 build, then rebuild Opsis plus the existing Chronikologio and Crawler plugins. Bump contract assembly identity to 1.0.0.0 in a coordinated host-first deployment; older host builds will not load the new contract. This file's assembly name/version by itself is NOT sufficient to guarantee compatibility: additional P1 members must exist in the exact binary.
4. On the Chronikologio integration branch, build `OPSIS_P1_BRIDGE` only after the updated contract DLL has been staged. Verify the existing `san-simera` feed, the rich feed and event bus. Do not deploy the feature flag to older hosts.
5. Validate plugin loading, disabled plugins and standalone-instance guard, 100/150/200% DPI, all monitor/taskbar modes, keyboard and screen reader navigation, 24–72 hour CPU/RAM/GDI/SSD soak, and install/uninstall upgrade paths in actual Windows sessions.
6. Never replace an installed plugin in place while Opsis is running. Preserve user config and store and retain rollback artifact before official release.
7. An Authenticode signature is not produced without a valid signing certificate. SHA-256 hashes detect file changes but do not establish publisher authenticity by themselves.

## Scope notes
- PortfolioTransaction ledger is optional data stored in config; it does not rewrite existing Position-based portfolio totals or migrate old cost bases silently. This avoids losing user financial data.
- Topic watchlist region matching is textual and must not be described as a geolocation service.
- A story cluster is a headline grouping, not fact verification.
- Percent-change alerts depend on the upstream price source providing `ChangePercent`; absence of this field produces no alert.
- New core timeline objects are separate from Windows/plugin contract DTOs, avoiding cyclic project dependencies.
- The installer source and Crawler P2 PR depend on P1; the P1 PR depends on P0. Merge in order with updated checks at every step.
