# P0: Opsis 2.8 host integration — tranche 1

**Base:** Crawler 0.28.0 P3 .NET 11 with the exact Opsis.UI 0.6.0 DLL used by Opsis 2.8.0.2510. No changes to public plugin contract 1.0.0.0.

## Implemented in this tranche
- A01/A10: in-memory versioned host context (language, theme, hardware tier), no location/credentials, independent standalone fallback.
- A02: idempotent Start/Stop, no duplicate delayed auto-update checks after Stop, blocked refresh while stopped, mutex release on failed Start.
- A03: Opsis owns global Opsis.UI theme in hosted mode; standalone retains its own theme tokens and repaint notification.
- A01: preserve language pushed by Opsis through TickerHost construction; avoid creating unused ticker windows when hardware tier or tray status are queried.
- Q01: correct .NET 10 vs .NET 11 BCL identity in public contract compatibility test.

## Explicitly remaining P0
- A04: no shared-weather callback exists in ABI 1.0.0.0: needs an optional host capability with actual provenance and consent.
- A05/A06: Taskbar/DeskStrip ownership and safe-mode compatibility hold require host-side handshake.
- A07/A08: richer performance/permissions policy and user consent.
- A09/Q02/Q03: coordinated installation, restore, rollback and Chronikologio binary consistency.
- A11/A12: read-only observability and event bus backpressure beyond existing bounded event snapshots.
- Windows CI, GPU, multi-monitor/DPI and installer checks must pass before release.

This is a first P0 implementation, NOT a declaration that all P0 is complete.

## Read-only compatibility preflight (Q01)

Before activating an existing Opsis 2.8.x installation, run:

```powershell
pwsh ./tools/verify-opsis-host-p0.ps1 -HostFolder 'C:\Program Files\Opsis' -PluginFolder 'C:\Program Files\Opsis\plugins\OpsisNewsCrawler' -ChronikologioFolder 'C:\Program Files\Opsis\plugins\Chronikologio'
```

Adjust paths to the actual installation. The Chronikologio parameter is optional. The script performs **only read operations** and rejects mismatched contract or UI6 DLL hashes, the wrong ABI or unsupported Opsis major version. It is not a substitute for VM installer/rollback testing.
## P0 installer fail-closed gate (Q02 / A09)

- Integrated installation now looks for the Opsis host assemblies under root, `lib/` or `libs/`.
- It extracts the candidate Crawler contract and UI6 DLL to the Inno temporary directory **before modifying the existing install**, and uses Inno Setup's `GetSHA256OfFile` to require exact binary parity with the Opsis host.
- In the absence of host contract ABI 1.0.0.0 or a matching UI6 0.6.0 DLL, integrated setup fails with a bilingual explanation. Separate install remains available.
- The disposable integrated E2E test now stages the exact UI6 binary and confirms that a tampered UI6 copy is rejected before any plugin is deployed.
- This does not by itself implement full atomic multi-app upgrade/rollback; a coordinated update of Opsis and Chronikologio remains necessary.

**Never overwrite the host's shared DLL from a plugin installer without a coordinated host+other-plugins release.**