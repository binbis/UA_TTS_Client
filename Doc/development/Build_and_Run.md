# Build, run and environment

> **Purpose:** how to build and run the client and the server, what the project facts are, how changes are verified without a test project, and the environment of the reference machine.
> **Audience:** developers and AI agents.
> **Source of truth:** `UA_TTS_Client.csproj`, `UA_TTS_Client.sln`, `Server_py/run_server.sh`, `.gitignore`.
> **Verified:** 2026-11-03.

## Commands

```bash
# Client
dotnet build           # expected: 0 warnings, 0 errors
dotnet run             # opens the window; the server must be running

# Server (the script changes into Server_py/ itself)
./Server_py/run_server.sh
```

Open `UA_TTS_Client.sln` in Rider to work in the IDE. Installation steps are in `user/Installation.md`.

## Project facts

| Item | Value |
|---|---|
| Target framework | `net10.0`, `<Nullable>enable</Nullable>` |
| Application type | `WinExe` (Avalonia desktop, classic lifetime) |
| UI | Avalonia 12.1.4, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter |
| MVVM | CommunityToolkit.Mvvm 8.4.2 |
| Audio | ManagedBass 4.0.2 (the native `libbass` is required) |
| Hotkey | SharpHook 8.0.0 |
| Clipboard on Windows | TextCopy 6.2.1 |
| DI, HTTP client, logging | Microsoft.Extensions.DependencyInjection, Http and Logging 10.0.13 |
| Implicit usings | **off**: every file declares its `using` directives |
| Solution format | classic `.sln`. .NET 10 creates `.slnx` by default, so the format is set explicitly |
| Application manifest | `app.manifest` (`supportedOS`) |

The project is a single `.csproj`; there are no class libraries. Layers are separated by folders (`architecture/Overview.md`).

There is no CI pipeline, no lint or format configuration, no code generation step and no test project.

## Verification approach

- **Build.** `dotnet build` with 0 warnings and 0 errors is the baseline for every change.
- **Manual scenarios.** `development/Manual_Test_Scenarios.md` lists the scenarios to run after a behavior change. Run the affected scenarios in the running application.
- **Harnesses.** For logic that needs no UI, write a temporary console program or test harness outside the repository, for example under `/tmp/opencode/`, with fake services. Delete it afterwards, or keep it outside the repository. Do not add it to the repository.
- **Hotkey.** The hook ignores simulated events, so `Alt+Q` can be verified only with a physical key press.

## Reference environment

The reference machine is the one on which the verified facts were obtained:

- EndeavourOS (Arch Linux), Wayland session (`XDG_SESSION_TYPE=wayland`), PipeWire.
- `wl-clipboard` (`wl-copy`, `wl-paste`) and `ydotool` are installed. The `ydotool` daemon is enabled, but in the current session its events did not reach windows. SharpHook works.
- The user is in the `input` group, so `/dev/uinput` is accessible.
- `libbass.so` is installed (AUR `libbass`).
- .NET SDK 10.0.113.
- Server virtual environment `Server_py/venv` with Python 3.14.8. The installed torch is `2.14.1+cu130` (CUDA 13.0 is available).
- Client configuration: `~/.config/UA_TTS_Client/config.json`.

## Git rules

- `Server_py/venv/` and the build output (`bin/`, `obj/`) are ignored by `.gitignore`.
- `Server_py/dictionary.json` and `Server_py/voices/` are project data. Change them only on request.
- Do not commit changes that you did not make. Stage files explicitly; do not use `git add -A` when the working tree contains someone else's changes.
