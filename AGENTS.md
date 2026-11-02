# AGENTS.md — UA_TTS_Client

Avalonia/.NET 10 client (MVVM, CommunityToolkit.Mvvm) for the local Python TTS server
(`Server_py/`, Flask + StyleTTS2). Target OSes: Linux EndeavourOS/Arch (Wayland + PipeWire) and Windows 11.

**The client is in working condition.** Steps 0–9 are implemented: `Core/` (contracts), `Infrastructure/`
(API, BASS audio, hook, clipboard, JSON config), ViewModels, Views (XAML), the Composition Root
in `App.axaml.cs`, and documentation. Iteration 2 is **completed**: **Steps 10–18 done**
(documentation, chunking, batch queue, auto-speech after `Alt+Q`, MP4/WAV export,
UI toggles and counters, final check).

The canonical plan is **`Doc/Current_Work_Plan.md`**; the code map is **`Doc/Project_Structure.md`**.

---

## Commands (verified)

```bash
# Client — build (currently 0 warnings / 0 errors)
dotnet build

# Client — run (opens a window; the server must already be running)
dotnet run

# Server — run with the correct cwd (the script changes into Server_py/ itself)
./Server_py/run_server.sh
```

- There is `UA_TTS_Client.sln` (classic format — compatible with all Rider versions; .NET 10 creates `.slnx` by default, so the format is set explicitly). `dotnet build` from the root works.
- There is no lint/format/codegen configuration. There is no CI.
- SDK: .NET 10.0.113 · Python 3.14.8 (`Server_py/venv`) · torch 2.14.1+cu130, **CUDA is available**.
- Starting the server takes ≈45 s (model `patriotyk/styletts2_ukrainian_multispeaker`, ~911 MB, already in the HF cache). The model is loaded on every start — do not be surprised that it takes a while before the first request.

### Explicit `using` directives are required

The `.csproj` has **no** `<ImplicitUsings>` — `System`, `System.IO`, `System.Net.Http`, `System.Threading.Tasks` are not imported automatically. Add `using` to every file.
`BassAudioPlayerService` has a name conflict for `PlaybackState` with `ManagedBass.PlaybackState`, so an alias is used there.

### Run the server ONLY from `Server_py/`

`server.py` uses relative paths (`glob("voices/*.pt")`, `"dictionary.json"`). Running from the repository root → 0 voices and an empty dictionary, with no error in the log.

---

## Server API contract (verified with curl)

Base URL: `http://127.0.0.1:5000` (Flask dev server, CORS `Access-Control-Allow-Origin: *`).

| Method | Path | Body | Success | Error |
|---|---|---|---|---|
| GET | `/api/voices` | — | `["Вероніка Дорош", …]` (31 voices) | — |
| GET | `/api/health` | — | `{"status":"ok","device":"cuda","voices":31,"model":"…"}` | — |
| POST | `/api/verbalize` | `{"text"}` | `{"text"}` | 400 `{"error":"No text"}` |
| POST | `/api/synthesize` | `{"text","voice","speed","volume","translate"}` | `audio/wav`, 24 kHz mono 16-bit PCM | 500 `{"error"}` |
| POST | `/api/convert` | multipart `file`, `format` | `audio/<fmt>` | 400 `{"error":"No file"}` |
| POST | `/api/parse_file` | multipart `file` | `{"text"}` | 400 `{"error":"No file"}` |

- `/api/health` **was added** to `Server_py/server.py` (Step 2). `/api/ping` and `/health` still do not exist → 404.
- `TtsApiClient.GetHealthAsync` falls back to `GET /api/voices` on a 404 from `/api/health` (compatibility with an old server without the patch).
- `/api/convert` and `/api/parse_file` now also return JSON `{"error": …}`. But the client's parser still tolerates plain text, because the server may not have been updated.
- Do not break the CORS headers: a browser extension uses the same server.
- `/api/convert` accepts `format=mp4`; verified on the server venv: pydub/ffmpeg produce an audio-only MP4 (AAC, `ftyp isom`). `format=m4a` **does not work** on this machine — do not use it. The client calls this endpoint for MP4 export (`TtsApiClient.ConvertAsync`, Step 16); for WAV no conversion is needed — the client concatenates fragments itself.
- The full contract with `curl`/JSON examples is in **`Doc/API.md`** (created in Step 9.2).

---

## ManagedBass 4.0.2 — real API

There are **no** `ManagedBass.Playback` / `ManagedBass.Channel` classes. Only the static `ManagedBass.Bass` exists:

```csharp
Bass.Init(int device, int freq, DeviceInitFlags flags, IntPtr win, IntPtr clsid); // 0 = No sound, -1 = default
Bass.Free();
Bass.CreateStream(byte[] memory, long offset, long length, BassFlags flags); // byte[] overload EXISTS
Bass.ChannelPlay(int handle, bool restart);
Bass.ChannelPause(int handle);        // toggle; attribute PlaybackPaused does NOT exist
Bass.ChannelStop(int handle);
Bass.ChannelSetAttribute(int handle, ChannelAttribute.Volume, float value);
Bass.ChannelSetSync(int handle, SyncFlags.End, 0, SyncProcedure proc, IntPtr user);
Bass.ChannelIsActive(int handle);     // → PlaybackState { Stopped, Playing, Stalled, Paused }
Bass.StreamFree(int handle);
Bass.LastError;                       // → ManagedBass.Errors
Bass.Volume;                          // double, global
```

- Delegate: `SyncProcedure(int syncHandle, int channel, int data, IntPtr user)` — **the first parameter is the sync handle, not the channel**. Compare `channel` with `_streamHandle`. A mistake here silently breaks the automatic transition to `Stopped`.
- `BassFlags` has **no** `Memory` flag — the `byte[]` overload handles it itself, but the array must live for the whole playback (pin via `GCHandle` or keep it in a service field).
- **On Linux `Bass.Init(0, …)` opens the "No sound" device**, not the default one: `Bass.NoSoundDevice == 0`, `Bass.DefaultDevice == -1`. Use `Bass.Init(Bass.DefaultDevice, 44100, …)` — then BASS creates the stream in PipeWire (`pactl list sink-inputs` shows `PipeWire ALSA [app]` on the active sink, including Bluetooth). Verified on this machine: with `0` there is no stream in `pactl` and no sound; with `-1` the sound goes to the headphones.
- BASS version 2.4.18.3; playback from `byte[]` verified — the position moves, `ChannelIsActive` → `Playing`.
- The NuGet package **does not contain** native libraries. The system `/usr/lib/libbass.so` is required. The AUR package is named **`libbass`** (`yay -S libbass`) — **`bass` from the `extra` repository is the game "Beneath A Steel Sky"**, not a library.
- `libbass.so` is installed on this machine.

**How to check the API instead of guessing** (ManagedBass/SharpHook/TextCopy have no proper docs):

```bash
mkdir -p /tmp/apidump && cd /tmp/apidump
# csproj: net10.0 + <PackageReference Include="ManagedBass" Version="4.0.2" />
# Program.cs: Assembly.Load("ManagedBass").GetType("ManagedBass.Bass")
#             .GetMembers(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly)
```

---

## SharpHook 8.0.0 and TextCopy 6.2.1 — verified facts

```csharp
var hook = new TaskPoolGlobalHook(parallelismLevel: 1, UioHookProvider.Instance);
hook.KeyPressed += (_, e) => { /* e.Data.KeyCode, e.IsEventSimulated */ };
var task = hook.RunAsync();          // does not block; completes after Stop()
hook.Stop(); hook.Dispose();

var simulator = SharpHook.Simulation.EventSimulator.Create("app-name", UioHookProvider.Instance);
simulator.Sequence().AddKeyPress(KeyCode.VcLeftControl) /* … */ .Simulate();
```

- `KeyboardHookEventArgs` lives in the namespace **`SharpHook`**, not `SharpHook.Data` (`KeyboardEventData` is in `SharpHook.Data`).
- `GlobalHookBase` has `RunAsync(GlobalHookType = All, bool useBackgroundThread = false)`, `Stop()`, `Dispose()`; events `KeyPressed/KeyReleased/KeyTyped/HookEnabled/HookDisabled`.
- `EventSimulator.Create(applicationName, provider)` on Linux initializes uinput devices; `SimulateKeyStroke(params KeyCode[])` is an extension in `SharpHook.Simulation`.
- Simulated events come back to the hook with `IsEventSimulated == true` — that is how our own `Ctrl+C` is filtered out.
- Verified on this machine: `HookEnabled` fires, and simulated `Ctrl+F13`/`Ctrl+C` are visible in the handler. **Alt+Q was physically verified by the user** (the hook fires); the trigger is now on release of both keys, otherwise Ctrl+C goes out as Ctrl+Alt+C.
- The `Alt+Q` hotkey fires **after both keys are released**. If `Ctrl+C` is simulated before the physical Alt is released, the active window receives `Ctrl+Alt+C` and nothing is copied.
- **On Linux the buffer is read only via `wl-paste`** (`wl-clipboard`); X11 is not supported, so the obsolete X11 fallback was removed (Step 11). On Windows `TextCopy` (Win32 Clipboard) is used; on Linux `TextCopy` is not called.
- After `Ctrl+C` the service polls the clipboard for up to 900 ms and returns an empty string if the content has not changed (so an old text is not picked up).
- Verified: `wl-paste --no-newline` sees a 1312-byte Wayland buffer. The full chain "selection → Ctrl+C → wl-paste" was verified on `zenity --entry`: exactly the typed marker was captured.
- **Terminals copy via `Ctrl+Shift+C`, not `Ctrl+C`**: simulating `Ctrl+C` in a terminal sends SIGINT. Capturing a selection from a terminal is not supported — there the text must be copied manually and pasted into the Quick TTS field (`Ctrl+V`).
- `ClipboardService.GetTextAsync(CancellationToken)` and `SetTextAsync` are static methods.

---

## Fragmentation, queue and `Alt+Q` (Steps 13–15) — verified facts

- `IAudioPlayerService.TrackEnded` fires **only at the natural end of a track**; `Stop()` and stream replacement do not raise it. The fragment queue waits precisely for this event.
- `FragmentPlaybackService.StartAsync` returns as soon as **the first fragment has started playing**, not after the whole batch. The rest of the batch plays in the background.
- Between batches: `BatchCompleted` + `IsBatchPending == true`; the next batch is started by `ContinueAsync`. `QueueCompleted` is raised when the last fragment has played.
- `SpeechQueueOptions.PauseAfterEachBatch` (default `true`): when `false`, the service starts the next batch itself — `BatchCompleted`/`IsBatchPending` do not occur, the "Next batch" button is not shown, and `QueueCompleted` with the summary is still raised.
- Synthesis requests are **sequential** (one fragment ahead) so as not to overload the GPU.
- A pause issued right after `FragmentStarted` is applied only after `BassAudioPlayerService.PlayAsync` has started — verified by the harness (otherwise the pause was lost).
- `SentenceAwareTextChunker`: three modes (`Paragraphs`, `Sentences`, `MaxLength`); abbreviations and initials do not break sentences; an over-long sentence is cut at a space or comma; empty text → **0 fragments**, not an exception.
- `MainWindowViewModel.HandleHotkeyAsync` performs **capture → activation → synthesis**. Activating the window before capture would break `Ctrl+C` (it would go to the app itself).
- A repeated `Alt+Q` during capture is ignored (`_hotkeyCaptureInProgress`); `QuickTts.CaptureForHotkeyAsync` does not start additionally if `IsBusy`.
- The «Захопити виділене» (Capture selection) button does **not** activate the window and does **not** start synthesis.
- `ActivateRequested` → `App.axaml.cs` → `Window.Activate()`. On Wayland the compositor may ignore the request (best effort); capture and speech work without activation.
- The hook ignores **simulated** events (`IsEventSimulated`), so `Alt+Q` cannot be tested by automation — only by a physical key press. The logic was verified with a harness with fake services (25/25 PASS).

---

## Audio export (Step 16) — verified facts

- `/api/convert` accepts a multipart `file` (WAV) + a `format` field. Verified on a live server: `format=mp4` gives an audio-only MP4 (`ftyp isom`, AAC); `format=m4a` **does not work** on this machine — do not use it. `ffmpeg` on the server is provided by `static-ffmpeg`.
- `WavAudioConcatenator` — a pure synchronous class: it validates RIFF/fmt/data (PCM 24 kHz, mono, 16-bit), concatenates `data` chunks under a new header, and ignores extra chunks; a mismatched format → `InvalidDataException`.
- `AudioExportService` synthesizes fragments **sequentially**, keeps the WAV in memory (~48 KB/s), and writes atomically: a temporary file in the same directory → `File.Move(overwrite)`. Cancel or error creates no target file and removes the temporary one.
- MP4 export **depends on the server**; WAV does not (no conversion needed).
- `IFileSaveDialogService` is the only UI service outside `Infrastructure` (`Views/Services/AvaloniaFileSaveDialogService`): `Infrastructure` must not depend on Avalonia. The dialog is invoked only from the UI thread; cancelling changes nothing.
- During export `CanCapture`/`CanSynthesize`/`CanExport` are disabled, and `Alt+Q` capture is ignored (`IsExporting`), so the text cannot change in the middle of the operation.
- Verified with a harness (49 checks: headers, data order, format errors, progress stages, cancellation without files, overwriting, ViewModel flow) and a live server (WAV 4.64 s / MP4 4.65 s, both decoded by pydub).

---

## This machine's environment

- Wayland (`XDG_SESSION_TYPE=wayland`) + PipeWire; `wl-copy`, `wl-paste`, `ydotool` are installed.
  `wl-clipboard` is enough for the clipboard; X11 utilities are not needed.
- The `ydotool` daemon is **enabled and active** (check with `systemctl --user is-active ydotool`).
  But in this session `ydotool key/type` exits with code 0 and the events do not reach windows (the journal shows `unable to find device pointer:ydotoold virtual device`). For UI automation do not rely on `ydotool`; SharpHook `EventSimulator` works.
- The user is in the `input` group → `/dev/uinput` is accessible for keyboard simulation.
- The NuGet cache is warm and the network is available; `dotnet build` succeeds without downloads.
- User config: `~/.config/UA_TTS_Client/config.json`. It already contains all fields from Steps 12 and 17; enums are stored as strings (`"chunkingMode": "Paragraphs"`, `"exportFormat": "Mp4"`).

---

## Code conventions

The authoritative source: **`Doc/Code_Standards_Prompt.md`** (the code standards section). In brief:

- `file-scoped namespaces`; `public partial class` for **all** ViewModels (otherwise `[ObservableProperty]`/`[RelayCommand]` are generated incorrectly).
- `x:DataType="vm:XxxViewModel"` in **every** XAML file (compiled bindings).
- `#nullable enable`; guard clauses; `ILogger<T>`; no `NullReferenceException`s.
- Asynchrony with `CancellationToken`; DI **only through constructors**; registration in `App.axaml.cs`.
- Typed HttpClient: `AddHttpClient<ITtsApiClient, TtsApiClient>()` with a timeout of **at least 120 s**.
- Paths only through `Path.Combine` + `Environment.SpecialFolder.ApplicationData` (`~/.config/UA_TTS_Client` / `%APPDATA%\UA_TTS_Client`). Linux is case-sensitive.
- Audio is played **from memory** (`byte[]`/`MemoryStream`), without writing temporary `.wav` files.
- Wayland: after simulating `Ctrl+C`, always `await Task.Delay(80..120)` before reading the buffer.
- **XML documentation and comments — English only.** `<inheritdoc />` for interface implementations. UI strings, log and exception messages, voice names, and dictionary data stay Ukrainian: they are product/runtime text, not comments.
- DTOs are `record`s; use primary constructors, collection expressions, and `required` wherever possible.
- Autotests are **not created** in iteration 2 (the user's decision). Verification — manual scenarios and temporary harnesses outside the repository.

---

## UI and DI (Steps 6–8) — verified facts

- Avalonia 12: `TextBox.Watermark` is **obsolete** → `PlaceholderText`; scrolling in a `TextBox` is set with the attached property `ScrollViewer.VerticalScrollBarVisibility="Auto"`.
- The Rider analyzer wrongly complains about `Design.DataContext="{x:Static …}"` («expected type is AttachedProperty<object?>»). Therefore the design-time DataContext is set in code-behind: `if (Design.IsDesignMode) DataContext = DesignData.MainWindow;` — without `Design.DataContext` in XAML. `DesignData` (`ViewModels/Design/DesignData.cs`) contains fake interface implementations for previews only.
- Conditional classes: `Classes.online="{Binding IsServerOnline}"` + style `Ellipse.status-dot.online`. Styles from `Window.Styles` apply to all child UserControls.
- `IsVisible` for rows is done via `{Binding X, Converter={x:Static conv:StringConverters.IsNotNullOrEmpty}}`.
- ViewModels are created on the UI thread; `ViewModelBase.OnUiThread` is needed for BASS events that come from a background thread.
- The `BaseAddress` of `HttpClient` is fixed at startup, so a URL change in the settings applies **after restart** (this is stated in the UI).
- `MainWindowViewModel` checks the server every 15 s via `PeriodicTimer`; the «Перевірити» (Check) button calls the same `RefreshServerStatusCommand`.
- ComboBoxes with settings lists work through option records (`ChunkingModeOption`, `ExportFormatOption`) — no magic indexes. `SelectedItem` binds to a nullable property, and the logic reads `ChunkingMode`/`ExportFormat` with fallback to defaults (the default chunking mode is `Paragraphs`, «Абзаци»).
- `NumericUpDown.Value` is `decimal?` (Avalonia 12.1.4); binding to an `int` property works (verified), and `FormatString="0"` shows whole numbers.
- Speed/volume sliders: `TickFrequency="0.1"` + `IsSnapToTickEnabled="True"`; the value is shown as `StringFormat='{}{0:0.0}'`.
- Quick TTS queue statuses — `Playback.QueueStatusText` («Пакет 1 з 3 завершено…» (Batch 1 of 3 completed…), «Готово: N пакетів, M фрагментів» (Done: N batches, M fragments)); position — `FragmentPositionText`, preview — `CurrentFragmentText` (up to 60 characters, one line). A queue error raises `Playback.QueueFailed` → `ErrorMessage` of Quick TTS (the subscription is removed in `Dispose`).
- Ukrainian number declension — `ViewModelBase.PluralWord(count, one, few, many)` (for symbols — `SymbolsWord`).

---

## Pitfalls (already hit, or nearly so)

1. **`ViewLocator` is fixed** (Step 8): it replaces `.ViewModels` → `.Views` and `ViewModel` → `View`, and for `MainWindowViewModel` it additionally tries `Views.MainWindow`. The main window is created explicitly in `App.axaml.cs`, so the locator only handles tab content (`ContentControl`).
2. **`App.axaml.cs` is the Composition Root.** The config is read synchronously before the container is built (the address is needed for `HttpClient.BaseAddress`); then services and ViewModels are registered. `desktop.Exit` disposes `MainWindowViewModel` and the `ServiceProvider`.
3. **Do not "fix" a `partial` property in a Toolkit model.** The form `[ObservableProperty] public partial string Foo { get; set; }` is **correct** for CommunityToolkit.Mvvm 8.x: the generator creates the partial property on the C# 13 `field` (`get => field;`), and `PropertyChanged` works. The template `Greeting` with "Welcome to Avalonia!" has already been removed from the code — the rule stands.
4. **`/api/health` now exists** (added to `server.py`), but `TtsApiClient` still has a fallback to `GET /api/voices` for compatibility with an old server. Do not remove this fallback.
5. **Server errors are historically inconsistent** (JSON vs plain text `No file`) — the parser `TtsApiClient.ReadErrorMessageAsync` tolerates both formats. Do not narrow it to JSON only.
6. **BASS `SyncProcedure`**: the first argument is the sync handle, the second is the channel. A mistake here gives no error in the log; auto-stop simply does not fire.
7. **Linux is case-sensitive**: the namespace in XAML (`x:Class`, `x:DataType`) must match the text in `.cs` exactly.
8. **`.gitignore`**: `Server_py/venv/` is ignored by the root rule (`.gitignore:49`) and by the built-in `venv/.gitignore` (`*`) — both are committed.
9. **`Bass.Init(0, …)` is the "No sound" device**, not the default device. Always use `Bass.DefaultDevice` (-1); otherwise synthesis passes (the server returns 200), but there is no sound at all. Check with `pactl list sink-inputs`.
10. **Do not simulate `Ctrl+C` while the physical Alt is held**: the active window receives `Ctrl+Alt+C` and copies nothing. The `Alt+Q` hook fires on release of both keys; do not switch the trigger back to a key press.

---

## `Doc/` and `README.md` — what each file is

State after **Step 10** (iteration 2): the historical `ua_tts_client_arch.md` was deleted and replaced by the up-to-date code map `Project_Structure.md`.

| File | State |
|---|---|
| `README.md` (root) | **Up to date**: running the server and client, "How to use", requirements, limitations |
| `Doc/Project_Structure.md` | **Up to date**: layers, actual tree, contracts, DI, threads, extension points |
| `Doc/Current_Work_Plan.md` | **Canonical plan for iteration 2**: steps, statuses, journal. Steps 10–18 ✅, iteration 2 completed |
| `Doc/API.md` | **Up to date**: endpoint contract with `curl`/JSON examples |
| `Doc/Code_Standards_Prompt.md` | Goals and code standards. Worth reading in full |
| ~~`Doc/ua_tts_client_arch.md`~~ | **Deleted** (Step 10): replaced by `Project_Structure.md` |
| ~~`Doc/Запит на архітектуру.md`~~ | **Deleted by the user** as historical |
| ~~`Doc/implementation_roadmap.md`~~ | **Deleted** (Step 9.2): referred to non-existent `/api/ping` and `/health` |
| ~~`Doc/ua_tts_client_architecture(remove).md`~~ | **Deleted** (Step 9.2): a conflicting duplicate |
| ~~`Doc/Readme.md`~~ | **Moved** to `README.md` in the root (Step 9.2) |

---

## Git state

Iteration 2 commits (`git log` shows the current history): `8181092 docs: актуалізувати AGENTS.md після завершення ітерації 2`, `93656ca docs: фінальна документація ітерації 2` (Step 18). Earlier: `14fdf31 feat(ui): перемикачі налаштувань, лічильники черги та слайдери` (Step 17), `076cec5 feat(export)` (Step 16), `fdb4106 docs` (Step 15), `41d955d feat(hotkey)`, `ae319e1 docs`, `5f35b80 feat(playback)`, `957fbbe feat(text)`, `c8fba32 feat(settings)`.

Iteration 2 is completed.

---

## Work plan

The canonical plan is **`Doc/Current_Work_Plan.md`**. Do not duplicate its contents in this file: it contains the step numbering, the current state, the contracts and the execution journal. The iteration 2 plan is **closed** (all steps ✅) — for new tasks create an iteration 3 plan (archive this file or add a new one).

Iteration 2 is **completed**: **Steps 10–18 done**, manual scenarios confirmed by the user. Step 9.1 (test project) is **cancelled** — autotests are not needed at this stage.

The actual state of the history and requirements is in `Doc/` and `README.md`. **Executable code and these two files take priority over the other documents.**
