# Opsis News Crawler 0.28.0 P3 — .NET 11 / Opsis 2.8 compatibility

**Status:** Candidate branch. The previous 0.28.0 .NET 10 installer must not be relabelled as a .NET 11 installer.

## Verified targets

| Component | Target / identity |
|---|---|
| Opsis host | 2.8.0.2510, net11.0-windows10.0.19041.0 |
| Crawler executable | product 0.28.0, net11.0-windows10.0.19041.0 |
| Crawler plugin | product 0.28.0, AssemblyVersion/FileVersion 1.0.0.0 |
| Shared contract | net11.0-windows, AssemblyVersion/FileVersion 1.0.0.0 |
| Opsis.UI 0.6.0 | net11.0-windows, AssemblyVersion 0.1.0.0, FileVersion 0.6.0.0 |

The Crawler libs/Opsis.UI.dll is copied byte-for-byte from the actual Opsis 2.8.0.2510 host (Git blob 4a5b4e0a1e1ec56780a1f581b79f0b272e3b52bd). The host PluginLoadContext shares both the contract and Opsis.UI assemblies with plugins.

## Real host smoke

GitHub workflow `.github/workflows/net11-opsis-280-host.yml` checks out the real Opsis host release branch, validates UI binary parity, builds and tests the Crawler, **stages the exact new shared contract in a disposable CI checkout**, builds the host, and runs `tests/OpsisNewsCrawler.Host28Smoke`. That program uses the actual nested Opsis `PluginHost.PluginLoadContext` via reflection, and verifies the Crawler plugin, 5 widgets, tray menu and navigation.

**Important:** This workflow does not update the published Opsis release/2.8.0.2510 binaries. The installed host and Chronikologio must still be updated to the exact ABI 1.0.0.0 contract DLL in a coordinated rollout, or incompatible plugins disabled.

## Local build

Requirements: Windows 11 x64, .NET 11 SDK/Desktop Runtime and Inno Setup 6/7.

```powershell
dotnet restore OpsisNewsCrawler.sln
dotnet test OpsisNewsCrawler.sln -c Release
dotnet build OpsisNewsCrawler.sln -c Release
pwsh ./tools/release.ps1
```

Installer dependency checks and downloads now target the .NET 11 Desktop Runtime. The application product version remains 0.28.0.

## Release gates still required

1. Windows CI build, all core/plugin tests and Opsis real-host smoke pass.
2. Fresh, update-helper and integrated installer VM tests pass for this new binary.
3. Exact contract DLL and UI6 DLL are staged in every installed Opsis host and dependent plugin.
4. Test navigation, tray, widgets, localization/RTL, High Contrast, DPI, startup and recovery.
5. Test on real hardware for GPU, multi-monitor, DWM/taskbar, UAC, SmartScreen and endurance.

No stable release tag or signed installer is implied by the candidate branch.