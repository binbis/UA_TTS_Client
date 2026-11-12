# Project structure (code map)

> **Purpose:** where each file and type lives, which contracts exist and which classes implement them.
> **Audience:** developers and AI agents.
> **Source of truth:** the repository tree (`git ls-files`) and the code itself.
> **Verified:** 2026-11-12.

## Tree

```text
UA_TTS_Client/
├── README.md, AGENTS.md              entry points
├── UA_TTS_Client.sln                 solution (classic format)
├── UA_TTS_Client.csproj              the only project: net10.0, Avalonia 12, no ImplicitUsings
├── app.manifest                      Windows application manifest (supportedOS)
├── Program.cs                        Avalonia entry point: BuildAvaloniaApp()
├── App.axaml(.cs)                    Application and composition root (DI, window, lifetime)
├── ViewLocator.cs                    ViewModel → View by naming convention
├── Assets/avalonia-logo.ico          window icon
├── .gitignore
├── Core/                             domain: no UI, no libraries
│   ├── Enums/
│   │   ├── AudioExportFormat.cs      Mp3, Wav
│   │   ├── AudioExportStage.cs       Synthesizing, Concatenating, Converting, Writing
│   │   ├── PlaybackState.cs          Idle, Playing, Paused, Stopped, Error
│   │   ├── ServerStatus.cs           Unknown, Online, Offline, Error
│   │   └── TextChunkingMode.cs       Paragraphs, Sentences, MaxLength
│   ├── Exceptions/
│   │   └── TtsApiException.cs        StatusCode (int?), IsConnectionError
│   ├── Interfaces/
│   │   ├── ITtsApiClient.cs          HTTP contract of the server
│   │   ├── ISettingsService.cs       LoadAsync, SaveAsync
│   │   ├── IAudioPlayerService.cs    playback (BASS)
│   │   ├── IClipboardAutomationService.cs   capture of the selected text
│   │   ├── IGlobalHotkeyService.cs   HotkeyPressed, Start, StopAsync
│   │   ├── ITextChunker.cs           Split
│   │   ├── IFragmentPlaybackService.cs      queue of fragments
│   │   ├── IAudioExportService.cs    ExportAsync
│   │   └── IFileSaveDialogService.cs        save dialog
│   └── Models/
│       ├── AppConfig.cs              settings record (see user/Configuration.md)
│       ├── SpeechQueueOptions.cs     Voice, Speed, BatchSize (1–10), PauseAfterEachBatch
│       ├── TextChunkingOptions.cs    Mode, MaxChunkLength (250)
│       ├── TextFragment.cs           record: Index (from 1), Text
│       ├── FragmentStartedEventArgs.cs
│       ├── BatchCompletedEventArgs.cs
│       ├── AudioExportRequest.cs     Text, Voice, Speed, Chunking, Format, DestinationPath
│       ├── AudioExportProgress.cs    Stage, CompletedFragments, TotalFragments
│       ├── TtsRequestDto.cs          text, voice, speed, volume, translate
│       ├── VerbalizeRequestDto.cs    text
│       ├── TextResponseDto.cs        { "text": ... }
│       ├── ApiErrorResponse.cs       { "error": ... }
│       ├── DictionaryStatus.cs         dictionary Rules, Error?
│       └── ServerHealth.cs           Status, Device?, Voices, Model?, Dictionary?
├── Infrastructure/                   implementations of the Core interfaces
│   ├── Api/TtsApiClient.cs           typed HttpClient; errors become TtsApiException
│   ├── Audio/
│   │   ├── BassAudioPlayerService.cs ManagedBass playback from memory
│   │   ├── AudioExportService.cs     synthesis → concatenation → conversion → atomic write
│   │   └── WavAudioConcatenator.cs   RIFF/fmt/data validation and concatenation
│   ├── Automation/
│   │   ├── SharpHookGlobalHotkeyService.cs       Alt+Q
│   │   └── WaylandClipboardAutomationService.cs  Ctrl+C simulation and clipboard (the name is historical: it also runs on Windows)
│   ├── Playback/FragmentPlaybackService.cs       synthesis one step ahead, batches, pause and stop
│   ├── Storage/JsonSettingsService.cs            config.json, atomic save
│   └── Text/SentenceAwareTextChunker.cs          fragmentation (see architecture/Text_Chunking.md)
├── ViewModels/
│   ├── ViewModelBase.cs              OnUiThread, PluralWord, SymbolsWord
│   ├── MainWindowViewModel.cs        tabs, server status, hotkey flow
│   ├── Components/PlaybackControlViewModel.cs    mini player and queue counters
│   ├── Tabs/
│   │   ├── QuickTtsViewModel.cs      text, capture, synthesis, export
│   │   ├── SettingsViewModel.cs      settings editing and saving
│   │   ├── ChunkingModeOption.cs     record for the chunking ComboBox
│   │   ├── ExportFormatOption.cs     record for the export ComboBox
│   │   ├── ReaderViewModel.cs        placeholder
│   │   └── CharacterVoiceViewModel.cs        placeholder
│   └── Design/DesignData.cs          preview-only fakes for the Rider designer
├── Views/
│   ├── MainWindow.axaml(.cs)         window, status bar, mini player, tabs
│   ├── Services/AvaloniaFileSaveDialogService.cs  system save dialog (UI layer)
│   └── Tabs/
│       ├── QuickTtsView.axaml(.cs)
│       ├── SettingsView.axaml(.cs)
│       ├── ReaderView.axaml(.cs)
│       └── CharacterVoiceView.axaml(.cs)
├── Server_py/                        Python server (see server/Server.md)
│   ├── server.py                     Flask app, all endpoints, StyleTTS2 pipeline
│   ├── verbalizer.py                 numbers, dates, units → Ukrainian words (ukrainian-tn)
│   ├── dictionary.json               pronunciation rules, read at startup
│   ├── voices/*.pt                   31 voice style files
│   ├── requirements.txt
│   └── run_server.sh                 Linux launcher (uses venv/bin/python)
└── Doc/                              documentation (index: Doc/README.md)
```

`Server_py/venv/` is local and is not tracked (ignored by `.gitignore`).

## Contracts and implementations

| Interface (`Core/Interfaces`) | Implementation | Members (summary) |
|---|---|---|
| `ITtsApiClient` | `Infrastructure/Api/TtsApiClient.cs` | `GetVoicesAsync`, `GetHealthAsync`, `VerbalizeAsync`, `SynthesizeAsync`, `ConvertAsync`, `ParseFileAsync` |
| `ISettingsService` | `Infrastructure/Storage/JsonSettingsService.cs` | `LoadAsync`, `SaveAsync`. A missing or unreadable file gives the defaults. |
| `IAudioPlayerService` | `Infrastructure/Audio/BassAudioPlayerService.cs` | `PlayAsync(byte[])`, `Pause`, `Resume`, `Stop`, `SetVolume(double)`, `Volume`, `State`; events `StateChanged`, `TrackEnded` |
| `IClipboardAutomationService` | `Infrastructure/Automation/WaylandClipboardAutomationService.cs` | `CaptureSelectedTextAsync` |
| `IGlobalHotkeyService` | `Infrastructure/Automation/SharpHookGlobalHotkeyService.cs` | event `HotkeyPressed`; `Start()`, `StopAsync()` |
| `ITextChunker` | `Infrastructure/Text/SentenceAwareTextChunker.cs` | `Split(text, options)` |
| `IFragmentPlaybackService` | `Infrastructure/Playback/FragmentPlaybackService.cs` | `StartAsync`, `ContinueAsync`, `Pause`, `Resume`, `Stop`; events `FragmentStarted`, `BatchCompleted`, `QueueCompleted`, `Failed`; counters |
| `IAudioExportService` | `Infrastructure/Audio/AudioExportService.cs` | `ExportAsync(request, progress, ct)` |
| `IFileSaveDialogService` | `Views/Services/AvaloniaFileSaveDialogService.cs` | `PickSavePathAsync`; UI thread only |

Other types: `WavAudioConcatenator` is a static class (no interface). `PreviewTtsApiClient` and other `Preview*` classes in `DesignData.cs` implement the interfaces for the designer only.

## Composition root (`App.axaml.cs`)

1. `LoadStartupConfig()` reads `config.json` synchronously, because the server address is needed for `HttpClient.BaseAddress`. Any error falls back to the defaults.
2. `ConfigureServices`:
   - logging to the console, minimum level Information, `HttpClient` logs at Warning;
   - `AppConfig` registered as a singleton instance;
   - a singleton for every `Core` service listed above;
   - `AddHttpClient<ITtsApiClient, TtsApiClient>` with `BaseAddress` from the config and a 5-minute timeout;
   - singletons for `PlaybackControlViewModel`, `SettingsViewModel`, `QuickTtsViewModel`, `MainWindowViewModel`.
3. `MainWindow` is created with `DataContext = MainWindowViewModel`. On `Opened`, `InitializeAsync()` runs. `ActivateRequested` restores and activates the window.
4. On exit, the ViewModel and the service provider are disposed.

## View ↔ ViewModel

| View | ViewModel | Role |
|---|---|---|
| `Views/MainWindow.axaml` | `MainWindowViewModel` | status bar, tabs, mini player |
| `Views/Tabs/QuickTtsView.axaml` | `QuickTtsViewModel` | text, capture, synthesis, export, queue status |
| `Views/Tabs/SettingsView.axaml` | `SettingsViewModel` | settings |
| `Views/Tabs/ReaderView.axaml` | `ReaderViewModel` | placeholder |
| `Views/Tabs/CharacterVoiceView.axaml` | `CharacterVoiceViewModel` | placeholder |

For the runtime behavior of these classes see `architecture/Flows.md`.
