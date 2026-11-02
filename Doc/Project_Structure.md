# UA_TTS_Client project structure

> **Purpose:** a code map for an agent and a developer: where things are, how the layers relate, and where to add new things. This is not an architecture history and not a work plan.
>
> **Up to date as of:** 2026-10-22 (after Steps 10–18; iteration 2 completed).
> **Work plan:** `Doc/Current_Work_Plan.md`. **Verified facts and pitfalls:** `AGENTS.md`.
> **Server contract:** `Doc/API.md`.

---

## 1. What it is

`UA_TTS_Client` is an Avalonia 12 / .NET 10 desktop client (MVVM, CommunityToolkit.Mvvm) for a local Python TTS server (`Server_py/`, Flask + StyleTTS2).

Target OS: Linux (EndeavourOS/Arch, Wayland + PipeWire) and Windows 11.

One project (`UA_TTS_Client.csproj`), no separate libraries. Layers are separated by directories and dependency rules, not by projects.

---

## 2. Layers and dependency rules

| Layer | Directory | Contents | May depend on |
|---|---|---|---|
| **Domain** | `Core/` | enums, DTOs, domain exceptions, service interfaces | **nothing** (no Avalonia, HttpClient, ManagedBass, SharpHook) |
| **Infrastructure** | `Infrastructure/` | implementations of `Core` interfaces: HTTP, BASS, hook, clipboard, JSON config | `Core`, external libraries |
| **Presentation (VM)** | `ViewModels/` | MVVM logic, `public partial class`, commands, states | `Core`, `ILogger<T>` |
| **Presentation (View)** | `Views/` | XAML + code-behind, design-time DataContext | `ViewModels` |
| **Composition Root** | `App.axaml.cs` | DI registration, window creation, start/exit | all layers |
| **UI services** | `Views/Services/` (as needed) | services that need Avalonia (e.g. file dialogs) | Avalonia, `Core` |

Rules:

- DI — **constructor injection only**; registration — only in `App.axaml.cs`.
- `Core` knows nothing about the UI or libraries; DTOs are `record`s.
- Transport errors are converted to `TtsApiException` before they reach a ViewModel.
- Audio is played **from memory** (`byte[]`), without temporary `.wav` files.
- Paths use `Path.Combine` + `Environment.SpecialFolder.ApplicationData`.
- Comments and XML documentation are in English. UI, log and error-message strings stay Ukrainian (product language).

---

## 3. Actual tree

```text
UA_TTS_Client/
├── AGENTS.md                     ← facts, API, pitfalls
├── README.md                     ← setup and usage
├── UA_TTS_Client.sln
├── UA_TTS_Client.csproj
├── App.axaml / App.axaml.cs      ← Composition Root (DI)
├── Program.cs
├── ViewLocator.cs                ← ViewModel → View
├── Assets/
├── Server_py/                    ← Flask + StyleTTS2 (separate Python project)
├── Doc/
│   ├── Current_Work_Plan.md      ← work plan
│   ├── Project_Structure.md      ← this file
│   ├── API.md                    ← server contract
│   └── Code_Standards_Prompt.md
├── Core/
│   ├── Enums/
│   │   ├── PlaybackState.cs      ← Idle, Playing, Paused, Stopped, Error
│   │   ├── ServerStatus.cs       ← Unknown, Online, Offline, Error
│   │   ├── TextChunkingMode.cs   ← Paragraphs, Sentences, MaxLength
│   │   ├── AudioExportFormat.cs  ← Mp4, Wav
│   │   └── AudioExportStage.cs   ← Synthesizing, Concatenating, Converting, Writing
│   ├── Exceptions/
│   │   └── TtsApiException.cs    ← StatusCode, IsConnectionError
│   ├── Models/
│   │   ├── AppConfig.cs          ← ServerUrl, Voice, Speed, Volume, Hotkey + new fields
│   │   ├── TtsRequestDto.cs      ← text, voice, speed, volume, translate
│   │   ├── VerbalizeRequestDto.cs
│   │   ├── TextResponseDto.cs
│   │   ├── ApiErrorResponse.cs
│   │   ├── ServerHealth.cs
│   │   ├── TextChunkingOptions.cs ← mode + MaxChunkLength = 250
│   │   ├── TextFragment.cs       ← Index + Text
│   │   ├── SpeechQueueOptions.cs ← Voice, Speed, BatchSize (1–10)
│   │   ├── FragmentStartedEventArgs.cs
│   │   ├── BatchCompletedEventArgs.cs
│   │   ├── AudioExportRequest.cs  ← text, voice, speed, format, path
│   │   └── AudioExportProgress.cs ← stage + fragment counter
│   └── Interfaces/
│       ├── ITtsApiClient.cs
│       ├── ISettingsService.cs
│       ├── IAudioPlayerService.cs
│       ├── IClipboardAutomationService.cs
│       ├── IGlobalHotkeyService.cs
│       ├── ITextChunker.cs
│       ├── IFragmentPlaybackService.cs
│       ├── IAudioExportService.cs
│       └── IFileSaveDialogService.cs
├── Infrastructure/
│   ├── Api/
│   │   └── TtsApiClient.cs       ← typed HttpClient
│   ├── Audio/
│   │   ├── BassAudioPlayerService.cs
│   │   ├── AudioExportService.cs     ← synthesis → concatenation → conversion → write
│   │   └── WavAudioConcatenator.cs   ← RIFF: concatenation of PCM chunks
│   ├── Automation/
│   │   ├── SharpHookGlobalHotkeyService.cs
│   │   └── WaylandClipboardAutomationService.cs
│   ├── Text/
│   │   └── SentenceAwareTextChunker.cs ← split into paragraphs/sentences/up to 250
│   ├── Playback/
│   │   └── FragmentPlaybackService.cs  ← batch queue of fragments
│   └── Storage/
│       └── JsonSettingsService.cs
├── ViewModels/
│   ├── ViewModelBase.cs          ← OnUiThread, SymbolsWord, PluralWord
│   ├── MainWindowViewModel.cs    ← tabs, health check, hotkey
│   ├── Components/
│   │   └── PlaybackControlViewModel.cs
│   ├── Tabs/
│   │   ├── QuickTtsViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── ChunkingModeOption.cs ← chunking mode option for a ComboBox
│   │   ├── ExportFormatOption.cs ← export format option for a ComboBox
│   │   ├── ReaderViewModel.cs    ← placeholder
│   │   └── CharacterVoiceViewModel.cs ← placeholder
│   └── Design/
│       └── DesignData.cs         ← fakes for the Rider preview
└── Views/
    ├── MainWindow.axaml(.cs)
    ├── Services/
    │   └── AvaloniaFileSaveDialogService.cs ← system save dialog
    └── Tabs/
        ├── QuickTtsView.axaml(.cs)
        ├── SettingsView.axaml(.cs)
        ├── ReaderView.axaml(.cs)
        └── CharacterVoiceView.axaml(.cs)
```

---

## 4. Key contracts and implementations

| Interface (`Core/Interfaces`) | Implementation (`Infrastructure`) | Purpose |
|---|---|---|
| `ITtsApiClient` | `Api/TtsApiClient.cs` | voices, health, verbalize, synthesize, convert, parse_file |
| `ISettingsService` | `Storage/JsonSettingsService.cs` | `config.json` with atomic write |
| `IAudioPlayerService` | `Audio/BassAudioPlayerService.cs` | BASS: play/pause/resume/stop/volume, `StateChanged` |
| `IClipboardAutomationService` | `Automation/WaylandClipboardAutomationService.cs` | simulated `Ctrl+C` + clipboard reading |
| `IGlobalHotkeyService` | `Automation/SharpHookGlobalHotkeyService.cs` | global `Alt+Q`, `HotkeyPressed` event |
| `ITextChunker` | `Text/SentenceAwareTextChunker.cs` | splitting text into fragments (paragraphs/sentences/250) |
| `IFragmentPlaybackService` | `Playback/FragmentPlaybackService.cs` | synthesis + batch queue + pause/stop |
| `IAudioExportService` | `Audio/AudioExportService.cs` + `WavAudioConcatenator` | exporting the whole text to MP4/WAV |
| `IFileSaveDialogService` | `Views/Services/AvaloniaFileSaveDialogService.cs` | system save dialog (UI thread only) |

`ITtsApiClient` is registered as a typed `HttpClient` with `BaseAddress` from the config and a 5-minute timeout.
`IClipboardAutomationService` on Linux reads the clipboard only through `wl-paste`; on Windows it uses `TextCopy`. X11 is not supported.

---

## 5. Composition Root (`App.axaml.cs`)

Order:

1. `LoadStartupConfig()` — **synchronously** reads `config.json` before the container is built (the address is needed for `HttpClient.BaseAddress`).
2. `ConfigureServices`:
   - `AddLogging` (console, filter for noisy `HttpClient` logs);
   - `AppConfig` as a singleton;
   - `ISettingsService`, `IAudioPlayerService`, `IClipboardAutomationService`, `IGlobalHotkeyService`, `ITextChunker`, `IFragmentPlaybackService`, `IAudioExportService`, `IFileSaveDialogService`;
   - `AddHttpClient<ITtsApiClient, TtsApiClient>()`;
   - `PlaybackControlViewModel`, `SettingsViewModel`, `QuickTtsViewModel`, `MainWindowViewModel` — singletons.
3. `MainWindow` is created with `DataContext = MainWindowViewModel`.
4. `window.Opened` → `MainWindowViewModel.InitializeAsync()`.
5. `desktop.Exit` → `Dispose()` of the ViewModel and `ServiceProvider`.

A change of URL in the settings takes effect **after restart** (this is written in the UI).

---

## 6. Execution flows

### 6.1 Startup

`Program` → `App.OnFrameworkInitializationCompleted` → DI → `MainWindow` → `Opened` → `MainWindowViewModel.InitializeAsync`:

1. subscribe to `IGlobalHotkeyService.HotkeyPressed` and call `Start()`;
2. `SettingsViewModel.InitializeAsync()` → `GET /api/voices`;
3. `RefreshServerStatusAsync()` → `GET /api/health` (fallback to `/api/voices` on 404);
4. background `PeriodicTimer` — health check every 15 s.

### 6.2 `Alt+Q` → auto-speech

1. `SharpHookGlobalHotkeyService` catches `Alt+Q` after **both** keys are released (so the simulated `Ctrl+C` does not become `Ctrl+Alt+C`).
2. `HotkeyPressed` event → `MainWindowViewModel.OnHotkeyPressed` → UI thread → `HandleHotkeyAsync()`.
3. **Capture (always first):** `QuickTts.CaptureForHotkeyAsync` → `IClipboardAutomationService.CaptureSelectedTextAsync`:
   - remembers the current clipboard;
   - simulates `Ctrl+C` via SharpHook `EventSimulator`;
   - polls the clipboard for up to 900 ms until it changes;
   - on Linux reads `wl-paste --no-newline`, on Windows — `TextCopy`;
   - returns an empty string if the clipboard did not change.
4. If nothing was captured — exit: the window is not activated and synthesis does not start.
5. If `Settings.ActivateOnHotkey`: `SelectedTabIndex = 0` (Quick TTS) and the `ActivateRequested` event → `App.axaml.cs` calls `Window.Activate()` (on Wayland the compositor may ignore it — best effort).
6. If `Settings.AutoSynthesizeOnHotkey`: `QuickTts.SynthesizeCurrentTextAsync` → fragment queue (see 6.3).
7. A repeated `Alt+Q` during capture is ignored (`_hotkeyCaptureInProgress`).

> The order of steps is critical: activating the window before capture would send `Ctrl+C` to the app itself, so the empty text would be copied instead of the selection.

### 6.3 Synthesis and playback (current state)

1. `QuickTts.SynthesizeCommand` takes `Text` and splits it with `ITextChunker` according to `Settings.ChunkingMode`.
2. `PlaybackControlViewModel.StartQueueAsync` → `FragmentPlaybackService.StartAsync`:
   - fragments are synthesized sequentially (`ITtsApiClient.SynthesizeAsync` → `POST /api/synthesize`);
   - one fragment is synthesized ahead while the current one plays;
   - batches of `Settings.BatchSize` (1–10); between batches the service waits for `ContinueAsync` if `Settings.PauseAfterEachBatch` is enabled (otherwise the batches play back to back).
3. `BassAudioPlayerService.PlayAsync`:
   - `Bass.Init(Bass.DefaultDevice, …)`;
   - `Bass.CreateStream(byte[], …)`;
   - `Bass.ChannelSetSync(End)` → `StateChanged = Stopped` + `TrackEnded`;
   - `Bass.ChannelPlay`.
4. `TrackEnded` completes the wait for the current fragment; `FragmentStarted`, `BatchCompleted` and `QueueCompleted` update the counters via `OnUiThread`.
5. Pause/resume/stop of the queue are methods of `IFragmentPlaybackService`; volume is set via `IAudioPlayerService.SetVolume`.

### 6.4 Audio export (MP4 / WAV)

1. `QuickTts.ExportCommand`: the format comes from `Settings.SelectedExportFormat`, then `IFileSaveDialogService.PickSavePathAsync` (`SuggestedFileName = UA_TTS_yyyy-MM-dd_HH-mm`). Cancelling the dialog → exit without changes.
2. `PlaybackControlViewModel.StopCommand` — playback and the queue are stopped (R18).
3. `AudioExportService.ExportAsync`:
   - `ITextChunker.Split` → empty → `ArgumentException`;
   - sequential `ITtsApiClient.SynthesizeAsync` for each fragment (`Volume = 1.0`, `Translate = false`), progress `Synthesizing (i, N)`;
   - `WavAudioConcatenator.Concat` — validation of RIFF/fmt/data (24 kHz, mono, 16-bit) and concatenation of PCM chunks with a new header, progress `Concatenating`;
   - `Mp4` → `ITtsApiClient.ConvertAsync(wav, "mp4")` (`POST /api/convert`), progress `Converting`; `Wav` — no conversion;
   - atomic write: temporary file in the same directory → `File.Move(overwrite: true)`, progress `Writing`; on cancel or error the temporary file is deleted.
4. `QuickTtsViewModel` shows `ExportProgressText` / `ExportProgressPercent`; during export `CanCapture`, `CanSynthesize` and `CanExport` are disabled, so the text cannot change.
5. Errors: the server text (`TtsApiException`) or an `IOException` is shown in `ErrorMessage`; MP4 depends on `ffmpeg` on the server (the alternative is WAV).

### 6.5 Settings

- `SettingsViewModel` is the source of truth for voice, speed, volume, chunking mode, batch size, and export format.
- The ComboBox works with option records (`ChunkingModeOption`, `ExportFormatOption`) without magic indexes; the logic values are `ChunkingMode` / `ExportFormat` (fallback to defaults if the config contains an unknown value).
- `NumericUpDown` «Фрагментів у пакеті» (Fragments per batch) (1–10, step 1); speed and volume sliders with a step of 0.1 (`IsSnapToTickEnabled`, format `0.0`).
- Checkbox «Пауза після кожного пакета» (Pause after each batch) (`PauseAfterEachBatch`, default ✅) — when disabled, `FragmentPlaybackService` starts the next batch itself. The default chunking mode is «Абзаци» (Paragraphs).
- The `AutoSynthesizeOnHotkey` / `ActivateOnHotkey` checkboxes are read by `MainWindowViewModel` directly during `Alt+Q` — changes take effect immediately; `SaveCommand` writes them to disk.
- `Volume` is synchronized with `PlaybackControlViewModel` in both directions (loop protection via `_syncingVolume`).
- `SaveCommand` validates the URL and writes `AppConfig` via `ISettingsService`.
- `JsonSettingsService`: `~/.config/UA_TTS_Client/config.json` (Linux) / `%APPDATA%\UA_TTS_Client\config.json` (Windows); temporary file + `File.Move`.

### 6.6 Tabs

`MainWindow.axaml` contains a `TabControl` with four `TabItem`s; the content is a `ContentControl` with a ViewModel. `ViewLocator` maps `ViewModels.*` → `Views.*`, and for `MainWindowViewModel` it additionally tries `Views.MainWindow`. The main window is created explicitly in `App.axaml.cs`.

`QuickTtsView` shows the queue state: the position (`Playback.FragmentPositionText`), a preview of the current fragment (`CurrentFragmentText`, up to 60 characters), the status of batch or whole-queue completion (`QueueStatusText`), and the «Наступний пакет» (Next batch) button when `IsBatchPending` is set. A queue error reaches `ErrorMessage` through the `Playback.QueueFailed` event.

---

## 7. View ↔ ViewModel

| View | ViewModel | Note |
|---|---|---|
| `Views/MainWindow.axaml` | `MainWindowViewModel` | server status, tabs, mini player (queue position, volume 0.1) |
| `Views/Tabs/QuickTtsView.axaml` | `QuickTtsViewModel` | text, capture, synthesis, queue counters, audio export |
| `Views/Tabs/SettingsView.axaml` | `SettingsViewModel` | URL, voice, speed, volume, chunking, batch, export format, hotkey |
| `Views/Tabs/ReaderView.axaml` | `ReaderViewModel` | placeholder |
| `Views/Tabs/CharacterVoiceView.axaml` | `CharacterVoiceViewModel` | placeholder |

Every XAML file must declare `x:DataType`. The design-time DataContext is set in code-behind via `if (Design.IsDesignMode) DataContext = DesignData...` (not via `Design.DataContext`). `DesignData` contains fake interface implementations; production code does not use them.

---

## 8. Extension points

### New service

1. Interface — `Core/Interfaces/I...cs`.
2. Models and enums — `Core/Models` or `Core/Enums`.
3. Implementation — the matching directory in `Infrastructure/`.
4. Registration — `App.axaml.cs`.
5. Preview fake — `ViewModels/Design/DesignData.cs`, if a ViewModel needs the service.

### New setting

1. Field in `Core/Models/AppConfig.cs` with a default value (old configs must still load).
2. Property in `SettingsViewModel` + `ApplyConfig` + `SaveAsync`.
3. Field in `SettingsView.axaml`.
4. If needed for the preview — value in `DesignData.PreviewConfig`.

### New tab

1. `ViewModels/Tabs/XxxViewModel.cs` (`public partial class`).
2. `Views/Tabs/XxxView.axaml(.cs)` with `x:DataType`.
3. ViewModel registration in DI.
4. `TabItem` in `MainWindow.axaml`.
5. If needed — `DesignData`.

### New API endpoint

1. Method in `ITtsApiClient`.
2. Implementation in `TtsApiClient` (use `SendAsync` so that errors are uniform).
3. Description in `Doc/API.md`.
4. Fake in `DesignData`.

---

## 9. Platform differences

| Aspect | Linux (Wayland + PipeWire) | Windows 11 |
|---|---|---|
| Clipboard | `wl-paste` (`wl-clipboard`); X11 not supported | `TextCopy` (Win32 Clipboard) |
| Keyboard simulation | SharpHook `EventSimulator` (uinput); fallback `ydotool` | SharpHook `EventSimulator` |
| Audio | `libbass.so` (AUR `libbass`), `Bass.DefaultDevice` → PipeWire | `bass.dll` next to the binary |
| Config | `~/.config/UA_TTS_Client/config.json` | `%APPDATA%\UA_TTS_Client\config.json` |
| Verified | yes, the working machine | build; runtime not verified |

Do not remove the Windows-specific paths: `TextCopy` is needed precisely for Windows.

---

## 10. Where to look next

| Question | File |
|---|---|
| What to do next and in what order | `Doc/Current_Work_Plan.md` |
| Exact APIs, pitfalls, environment | `AGENTS.md` |
| Server contract | `Doc/API.md` |
| Setup and usage | `README.md` |
| Code standards | `Doc/Code_Standards_Prompt.md` |
