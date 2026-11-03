# Pitfalls

> **Purpose:** the mistakes that were already made or are easy to make in this project, each with its symptom and the rule that prevents it.
> **Audience:** developers and AI agents.
> **Source of truth:** the code and the verified runs described in the owner documents linked below.
> **Verified:** 2026-11-03.

## Server

1. **The server is started from the repository root.** Symptom: 0 voices and an empty dictionary, with no error. Rule: start it from `Server_py/`. The launcher `Server_py/run_server.sh` does this itself. (`server/Server.md`)
2. **`dictionary.json` has a syntax error.** Symptom: `[!] Помилка словника` in the server console and no dictionary rules at all. Rule: validate the JSON after every edit, and restart the server after any change. (`server/Server.md`)
3. **Parts are cut at 500 tokens silently.** Symptom: the end of a very long part is missing from the audio. Rule: do not assume that the full text was spoken. (`server/Server.md`)
4. **`/api/convert` accepts WAV input only, and `m4a` does not work.** Rule: request `mp4` or `wav`. Never `m4a` on the reference machine. (`server/API.md`)
5. **An older server has no `/api/health`.** Symptom: 404. Rule: keep the client fallback to `GET /api/voices`. (`server/API.md`)
6. **CORS headers.** Rule: do not remove or change the headers set in `server.py`. A browser extension uses the same server. (`server/API.md`)
7. **Error bodies come in several formats.** Rule: the client reads JSON `{"error"}` and plain text, and rejects HTML. Do not narrow that to JSON only. (`server/API.md`)
8. **`torch` is not pinned in `requirements.txt`.** Symptom: the server runs on CPU when CUDA was expected. Rule: install the CUDA build of torch first, especially on Windows. (`user/Installation.md`)

## Audio (BASS)

9. **`Bass.Init(0, …)` is the "No sound" device.** Symptom: synthesis succeeds, but there is no sound and no PipeWire stream. Rule: use `Bass.DefaultDevice` (-1). (`reference/Native_Libraries.md`)
10. **The `SyncProcedure` arguments.** The first argument is the sync handle, the second is the channel. Symptom: `TrackEnded` never fires, and the queue stalls. Rule: compare `channel` with the stream handle. (`reference/Native_Libraries.md`)
11. **The lifetime of the audio buffer.** `CreateStream(byte[])` does not copy the data. Symptom: silence or garbage. Rule: keep the array referenced until playback ends. (`reference/Native_Libraries.md`)
12. **`TrackEnded` fires only at the natural end.** `Stop()` does not raise it. Rule: do not use it to detect cancellation. (`architecture/Flows.md`)
13. **A pause requested right after a fragment starts.** Symptom: the pause is lost. Rule: apply it after `PlayAsync` has started; the queue does this already. (`architecture/Flows.md`)

## Hotkey and clipboard

14. **The window is activated before the capture.** Symptom: nothing is copied, because `Ctrl+C` goes to this application. Rule: capture first, then activate, then speak. (`architecture/Flows.md`)
15. **`Ctrl+C` is sent while the physical Alt is held.** Symptom: the application receives `Ctrl+Alt+C`. Rule: the hotkey fires after both keys are released, and the capture releases Alt before it sends `Ctrl+C`. (`reference/Native_Libraries.md`)
16. **Terminals.** `Ctrl+C` in a terminal sends SIGINT. Rule: capture from terminals is not supported; the user copies with `Ctrl+Shift+C` and pastes into the field. (`user/Troubleshooting.md`)
17. **X11 is not supported.** Without `WAYLAND_DISPLAY` the capture returns empty text and logs a warning. Rule: do not add an X11 fallback back. (`reference/Native_Libraries.md`)
18. **Old text returned as a capture.** Rule: the capture returns text only when the clipboard has changed since the baseline. Keep this behavior. (`reference/Native_Libraries.md`)
19. **The `ydotool` fallback needs its daemon.** Symptom: the fallback logs a warning and fails. Rule: SharpHook is the primary path; on the reference machine `ydotool` did not deliver events. (`reference/Native_Libraries.md`)
20. **`EventSimulator.Create` is expensive.** Rule: create it lazily, as the code does. (`reference/Native_Libraries.md`)

## UI and code

21. **`[ObservableProperty]` on a `partial` property.** The form `public partial string Foo { get; set; }` is correct. Do not "fix" it. (`reference/UI_and_DI.md`)
22. **Every ViewModel is `public partial class`.** Without `partial` the generators produce nothing. (`reference/UI_and_DI.md`)
23. **`ImplicitUsings` are off.** Symptom: `Task`, `File`, `Path` or `Console` cannot be found. Rule: add the `using` directives. (`development/Build_and_Run.md`)
24. **`Design.DataContext="{x:Static …}"` in XAML** is reported by Rider's analyzer. Rule: set the design-time DataContext in code-behind. (`reference/UI_and_DI.md`)
25. **`TextBox.Watermark` is obsolete.** Use `PlaceholderText`. (`reference/UI_and_DI.md`)
26. **Names are case-sensitive on Linux.** `x:Class`, `x:DataType` and file names must match exactly. (`reference/UI_and_DI.md`)
27. **`ViewLocator` naming.** A View must be named `<Name>View` in the matching `Views` namespace. `MainWindow` is the special case. (`reference/UI_and_DI.md`)
28. **Every View and UserControl XAML needs `x:DataType`.** Symptom: the compiled-binding check is lost. Rule: declare `x:DataType="vm:<Name>ViewModel"`. (`reference/UI_and_DI.md`)

## Configuration and repository

29. **Enums in `config.json` are strings.** Write `"Paragraphs"`, not a number. (`user/Configuration.md`)
30. **A broken `config.json`.** The client starts with the defaults and logs a warning. The next save overwrites the broken file. (`user/Configuration.md`)
31. **`Server_py/venv/` is not tracked.** It is ignored by the rule at `.gitignore` line 49. Do not commit it. (`development/Build_and_Run.md`)
32. **User data files.** `Server_py/dictionary.json` and `Server_py/voices/` are project data. Do not change or commit them without a request. (`development/Build_and_Run.md`)
