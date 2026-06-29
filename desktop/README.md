# qlipq — C# / WinUI 3 desktop app

This is the native Windows rewrite of the qlipq desktop app, migrated from Tauri
(Rust backend + React/TypeScript webview) to **C# with WinUI 3** and
**LibVLCSharp** for video preview. It lives alongside — and is built independently
of — the Vite+ JS monorepo (it is **not** a pnpm workspace member).

## Architecture

The original split is preserved: **the app builds ffmpeg/ffprobe argument strings;
the host only spawns processes.** The two pure libraries are direct C# ports of the
shared TS packages and are covered by ported parity tests.

| Project                                              | Target                       | Role                                                                                                                                                                                 |
| ---------------------------------------------------- | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `src/Qlipq.Core`                                     | `net9.0`                     | Domain model + pure logic — config (+ lenient JSON), edit spec, OBS filename parsing, rename templating, INI/OBS detection, datetime. Port of `@qcksys/qlipq-core`.                  |
| `src/Qlipq.Ffmpeg`                                   | `net9.0`                     | The single source of truth for ffmpeg/ffprobe **arg building & parsing** — `BuildExportArgs`, `ParseFfprobe`, `ParseProgress`, `EstimateExportSize`. Port of `@qcksys/qlipq-ffmpeg`. |
| `src/Qlipq.Host`                                     | `net9.0`                     | Process runner, config/edits stores, folder scan, `FileSystemWatcher`, OBS/NVIDIA detection, rename/delete — the ported Tauri commands (no UI).                                      |
| `src/Qlipq.App`                                      | `net9.0-windows10.0.19041.0` | WinUI 3 UI (MVVM via CommunityToolkit.Mvvm), LibVLCSharp preview, MSIX-packaged.                                                                                                     |
| `tests/Qlipq.Core.Tests`, `tests/Qlipq.Ffmpeg.Tests` | `net9.0`                     | xUnit ports of the TS test suites (exact ffmpeg-arg parity).                                                                                                                         |

The TS packages remain in the JS monorepo as the **parity oracle** (their tests and
`@qcksys/qlipq-ffmpeg`'s real-ffmpeg `integration.test.ts` cross-check the C# ports).

## Build & run

Requires the **.NET 10 SDK** and (for the WinUI app) the Windows App SDK build
tooling — Visual Studio 2022 17.10+ with the _Windows App SDK_ / _.NET desktop_
workloads, or `dotnet` on a Windows machine with the Windows 10/11 SDK.

```bash
# From desktop/
dotnet test tests/Qlipq.Core.Tests/Qlipq.Core.Tests.csproj      # 41 tests
dotnet test tests/Qlipq.Ffmpeg.Tests/Qlipq.Ffmpeg.Tests.csproj  # 32 tests

dotnet build src/Qlipq.App/Qlipq.App.csproj -p:Platform=x64     # build the app
```

Run/debug the app from Visual Studio (set `Qlipq.App` as startup, platform x64),
or:

```bash
dotnet run --project src/Qlipq.App/Qlipq.App.csproj -p:Platform=x64
```

ffmpeg/ffprobe must be on `PATH` (or set explicit paths in **Settings → FFmpeg**) —
the app shells out to them, exactly as the Tauri version did. LibVLC's native
binaries are restored via the `VideoLAN.LibVLC.Windows` package.

## Packaging (MSIX)

The app is configured for MSIX (`WindowsPackageType=MSIX`, `Package.appxmanifest`).
Produce a signed package from Visual Studio (**Project → Package and Publish**) or:

```bash
dotnet publish src/Qlipq.App/Qlipq.App.csproj -c Release -p:Platform=x64 ^
  -p:GenerateAppxPackageOnBuild=true
```

Update the `Identity`/`Publisher` in `Package.appxmanifest` to match your
code-signing certificate before distributing.

## Data compatibility

Config and per-clip edits live in the **same** location and format as the Tauri
app — `~/.com.qcksys.qlipq/config.json` and `edits.json` (camelCase, with the
`$schema` reference) — and a one-time migration copies them from the old Roaming
AppData folder, so existing users' settings and edits carry over unchanged.

## Notes

- **Preview proxies were removed.** LibVLC plays MKV/HEVC natively, so the old
  proxy-build/cache machinery (and the _Settings → Preview cache_ section) is gone.
  Export accuracy comes from ffmpeg `-ss`/`-t`; the preview is an advisory guide.
- The Tauri app under `apps/app` is **superseded** by this project and can be
  removed once this app is validated on a real machine.
