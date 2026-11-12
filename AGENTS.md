# AGENTS.md — UA_TTS_Client

> Entry point for AI agents. Read this file, then the documents linked below. The detailed facts live in `Doc/`; this file keeps the rules, the commands and a short list of pitfalls.

## Project

Avalonia 12 / .NET 10 desktop client (MVVM, CommunityToolkit.Mvvm) for a local Python TTS server (StyleTTS2, Flask) in `Server_py/`. Ukrainian speech with 31 voices, a global `Alt+Q` hotkey, batch playback and MP3/WAV export.

Targets: Linux (EndeavourOS/Arch, Wayland + PipeWire), tested; Windows 11, not yet run.

## Documentation map

| Need | Document |
|---|---|
| Where a file or type lives; contracts | `Doc/architecture/Project_Structure.md` |
| Startup, hotkey, playback queue, export: order of operations | `Doc/architecture/Flows.md` |
| Architecture, threads, platforms, design choices | `Doc/architecture/Overview.md` |
| Text chunking algorithm | `Doc/architecture/Text_Chunking.md` |
| Server endpoints: requests, responses, errors | `Doc/server/API.md` |
| Server pipeline, dictionary, voices | `Doc/server/Server.md` |
| ManagedBass, SharpHook, TextCopy, wl-paste: real APIs | `Doc/reference/Native_Libraries.md` |
| Avalonia, CommunityToolkit.Mvvm, DI rules | `Doc/reference/UI_and_DI.md` |
| All pitfalls | `Doc/reference/Pitfalls.md` |
| Build, run, reference environment | `Doc/development/Build_and_Run.md` |
| Recipes: new service, setting, tab, endpoint, format | `Doc/development/Extension_Points.md` |
| Manual verification checklist | `Doc/development/Manual_Test_Scenarios.md` |
| Coding standards (C#, Avalonia, MVVM, DI, documentation) | `Doc/Code_Standards_Prompt.md` |
| Backlog and out-of-scope decisions | `Doc/Roadmap.md` |
| Documentation index and conventions | `Doc/README.md` |

## Commands

```bash
dotnet build                  # client; expected 0 warnings, 0 errors
dotnet run                    # client; the server must already be running
./Server_py/run_server.sh     # server; the script changes into Server_py/ itself
```

There is no lint, format, code generation or CI configuration, and no test project (a decision; see `Doc/Roadmap.md`).

## Rules that must not be broken

- Run the server only from `Server_py/`. `server.py` uses relative paths (`voices/*.pt`, `dictionary.json`).
- The project has no `ImplicitUsings`: add the `using` directives to every file.
- ViewModels are `public partial class`. Every View and UserControl XAML declares `x:DataType`.
- Use `#nullable enable`, guard clauses, `ILogger<T>` and `CancellationToken` in async code. DI is constructor-only; services are registered only in `App.axaml.cs`.
- Play audio from memory (`byte[]`). Do not write temporary WAV files for playback.
- Synthesis requests are sequential, one fragment at a time, never in parallel (GPU).
- Keep the `Alt+Q` order: capture, then activate, then synthesize. Activating the window before the capture makes `Ctrl+C` go to this application.
- Do not simulate `Ctrl+C` while the physical Alt key is held. The hotkey fires after both keys are released.
- Comments and XML documentation are in English. UI strings, log messages and error messages are in Ukrainian.
- No test project. Verify with the manual scenarios and with temporary harnesses outside the repository.
- Do not commit changes that you did not make. `Server_py/dictionary.json` is the user's working file.

## Pitfalls (short list)

1. `Bass.Init(0, …)` is the "No sound" device on Linux. Use `Bass.DefaultDevice`.
2. The first argument of the BASS `SyncProcedure` is the sync handle. Compare `channel` with the stream handle.
3. `/api/convert` does not accept `m4a` on the reference machine. Use `mp3` or `wav`.
4. Terminals copy with `Ctrl+Shift+C`; `Ctrl+C` sends SIGINT there. Capture from terminals is not supported.
5. `TrackEnded` fires only at the natural end of a track, never on `Stop()`.
6. `server.py` CORS headers must stay as they are: a browser extension uses the same server.
7. Full list with symptoms: `Doc/reference/Pitfalls.md`.

## Environment (reference machine)

Wayland session and PipeWire. The user is in the `input` group, and `libbass` is installed. SharpHook `EventSimulator` is the only keyboard simulation path. Details: `Doc/development/Build_and_Run.md`.

## Documentation policy

- Update the owner document in the same change as the behavior change. The owner of each topic is listed in `Doc/README.md`.
- Do not add step numbers, iteration names or change history to reference documents.
- Do not create plan files in the repository. Open items go to `Doc/Roadmap.md`.
- Refresh the `Verified` date of a document after you check it against the code.
