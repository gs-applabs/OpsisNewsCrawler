# Opsis shared binaries — .NET 11 / 2.8.0.2510

Only the current runtime contract and UI binaries are tracked here; historical versions are available through Git.

- `Opsis.UI.dll`: **the exact** host copy from `gs-applabs/Opsis` `release/2.8.0.2510/lib/Opsis.UI.dll`; AssemblyVersion `0.1.0.0`, FileVersion `0.6.0.0`; Git blob `4a5b4e0a1e1ec56780a1f581b79f0b272e3b52bd`.
- `OpsisNewsCrawler.Plugin.Abstractions.dll`: **the exact** shared host ABI from `gs-applabs/Opsis` `release/2.8.0.2510/lib/OpsisNewsCrawler.Plugin.Abstractions.dll`; AssemblyVersion/FileVersion `1.0.0.0`; Git blob `7beb06349daef31ae4c80f2a47a32c7477f1e50d`.
- Text files `*.version.txt` record provenance.

**Important:** do not compile and publish the Crawler source mirror's contract DLL separately or replace only one host/plugin copy. Every dependent application, including Chronikologio, must be built and deployed against the same binary. The installer and CI reject an ABI/UI6 SHA mismatch. To upgrade either binary, pin the matching new host release and run cross-app regression and installation tests first.

The remaining flat controls originate from `gs-applabs/Opsis.UI`; the app shares only the published Windows-compatible DLL with the host. The standalone Crawler has its own palette adapter, while hosted mode never overrides the process-wide Opsis theme.
