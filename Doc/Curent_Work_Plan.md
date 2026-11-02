# UA_TTS_Client work plan — iteration 2

> **Status:** completed. Updated 2026-10-22. Iteration 2: **Steps 10–18 done** (all ✅).
> **Source of requirements:** the user's notes after manual use of the working version (`Notes_Userstory.md` was deleted in Step 16; the requirements were moved into this plan).
> **Baseline:** working version after Steps 0–9; autotests are deliberately **not** created.
> **Read before starting:**
> 1. `AGENTS.md` — verified facts, exact APIs, pitfalls;
> 2. `Doc/Project_Structure.md` — actual code map and extension points (created in Step 10).
>
> This file answers *"what, and in what order"*. `AGENTS.md` answers *"how exactly"*.

---

## 1. State at the start

- Steps 0–9 done; the app starts, synthesis and playback work.
- **Steps 10–18 done** (documentation, X11 fallback removal, config, chunking, batch queue, auto-speech after `Alt+Q`, MP4/WAV export, UI toggles and counters, final check) — see the journal.
- Step 9.1 (test project) is **closed as obsolete** — autotests are not needed at this stage.
- UI/UX needs identified by the user:
  1. after `Alt+Q` the text is captured, but speech has to be started manually;
  2. all text goes to the server in one request — chunking is needed;
  3. settings for the chunking mode and auto-start are needed;
  4. Quick TTS has no fragment counter;
  5. pause/resume must work with fragments;
  6. after `Alt+Q` the window should activate / switch to Quick TTS (optional);
  7. the speed and volume sliders must have a step of `0.1` and a proper number format;
  8. saving the whole spoken text to a `.mp4` file (default) or `.wav` is needed; the format choice goes into settings.
- X11 is no longer supported: on Wayland `wl-clipboard` is enough for the clipboard. `TextCopy` stays in the project **only for Windows 11**.

---

## 2. Recorded decisions

| # | Decision | Rationale |
|---|---|---|
| R7 | **Chunking is done on the client**; the server is not changed | The server accepts one `text`; the client splits the text itself and makes sequential requests |
| R8 | **Batch mode:** text is split into fragments, fragments into batches of N (1–10, default 5). Playback stops after a batch; the rest is started manually | The user's choice: "in batches of N", no automatic continuation |
| R9 | **Auto-speech after `Alt+Q` is enabled by default**; window activation is **disabled by default** | A quick scenario "select → hear it", without unexpected focus stealing |
| R10 | **Default chunking mode is "up to 250 characters"** (smart sentence packing). Modes: paragraphs / sentences / up to 250 | A compromise between TTS quality and speed; everything is configurable |
| R11 | **The obsolete X11 fallback is removed from the code and documentation.** Linux — only `wl-paste`; Windows — `TextCopy` | X11 is no longer a target platform; an extra dependency and confusion |
| R12 | **`Doc/ua_tts_client_arch.md` is deleted**, replaced by `Doc/Project_Structure.md` | A code map is needed for the agent, not a historical architecture document |
| R13 | **No test project is created** | The user's decision; verification — manual scenarios + temporary harnesses outside the repository |
| R14 | **Reader and Character Voice are out of this iteration** | Not part of the current needs |
| R15 | **One checkbox "Activate window after Alt+Q"** controls both the window activation and the switch to the Quick TTS tab | Activation without switching to the needed tab makes no sense |
| R16 | **Export of the whole text** from the Quick TTS field: the «Зберегти аудіо…» (Save audio…) button, formats **MP4 (default)** and **WAV**; MP4 is produced by the server's `/api/convert` | The server already has pydub + ffmpeg; the client does not need a new native dependency |
| R17 | **MP4 is audio without video** (AAC in an MP4 container), not a video clip | The requirement is to keep the sound; no video track is needed |
| R18 | During export the current playback **is stopped**; export runs as a separate sequential synthesis | Avoid competing for the GPU and confusion with the queue |

---

## 3. Target tree (new and changed files)

```text
Core/
├── Enums/
│   ├── TextChunkingMode.cs              ← new: Paragraphs | Sentences | MaxLength
│   └── AudioExportFormat.cs             ← new: Mp4 | Wav
├── Models/
│   ├── AppConfig.cs                     ← new fields (see §6.1)
│   ├── TextChunkingOptions.cs           ← new: mode + MaxChunkLength = 250
│   ├── TextFragment.cs                  ← new: record (Index 1-based, Text)
│   ├── SpeechQueueOptions.cs            ← new: Voice, Speed, BatchSize
│   ├── AudioExportRequest.cs            ← new: text, voice, speed, chunking, format, path
│   └── AudioExportProgress.cs           ← new: stage + completed/total fragments
├── Interfaces/
│   ├── ITextChunker.cs                  ← new
│   ├── IFragmentPlaybackService.cs      ← new
│   ├── IAudioExportService.cs           ← new
│   ├── IFileSaveDialogService.cs        ← new
│   └── ITtsApiClient.cs                 ← + ConvertAsync (MP4/WAV via the server)

Infrastructure/
├── Text/
│   └── SentenceAwareTextChunker.cs      ← new: pure implementation of ITextChunker
├── Playback/
│   └── FragmentPlaybackService.cs       ← new: synthesis + queue + batches
├── Audio/
│   ├── BassAudioPlayerService.cs        ← + TrackEnded event (natural end)
│   ├── WavAudioConcatenator.cs          ← new: in-memory WAV concatenation
│   └── AudioExportService.cs            ← new: synthesis of all fragments → WAV/MP4
├── Automation/
│   └── WaylandClipboardAutomationService.cs ← X11 fallback removed; TextCopy only on Windows
├── Api/
│   └── TtsApiClient.cs                  ← + ConvertAsync (multipart /api/convert)
└── Storage/
    └── JsonSettingsService.cs           ← JsonStringEnumConverter

ViewModels/
├── MainWindowViewModel.cs               ← SelectedTabIndex, ActivateRequested, new hotkey flow
├── Components/
│   └── PlaybackControlViewModel.cs      ← queue, counters, batch pause/resume
├── Tabs/
│   ├── QuickTtsViewModel.cs             ← chunking, auto-synthesis, export, statuses
│   └── SettingsViewModel.cs             ← new settings, export format, step 0.1
└── Design/
    └── DesignData.cs                    ← fakes for ITextChunker / IFragmentPlaybackService /
                                             IAudioExportService / IFileSaveDialogService

Views/
├── MainWindow.axaml(.cs)                ← SelectedIndex, fragment counter
├── Services/
│   └── AvaloniaFileSaveDialogService.cs ← new: SaveFilePickerAsync (UI layer)
└── Tabs/
    ├── QuickTtsView.axaml               ← counter, «Наступний пакет», «Зберегти аудіо…»
    └── SettingsView.axaml               ← checkboxes, ComboBox, NumericUpDown, sliders

App.axaml.cs                             ← registration of new services, ActivateRequested
Doc/
├── Project_Structure.md                 ← new (replaces ua_tts_client_arch.md)
├── Curent_Work_Plan.md                  ← this plan
└── ua_tts_client_arch.md                ← deleted (replaced by Project_Structure.md)
```

---

## 4. Steps

### Step 10 — Documentation hygiene ✅

**Goal:** remove the historical document and give the agent an up-to-date code map.

| # | Action | Status |
|---|---|---|
| 10.1 | Create `Doc/Project_Structure.md`: layers, actual tree, key types, DI, flows, extension points, platform differences | ✅ |
| 10.2 | Delete `Doc/ua_tts_client_arch.md` | ✅ |
| 10.3 | Rewrite this plan for iteration 2 | ✅ |
| 10.4 | Update the references in `AGENTS.md`, `README.md`, `Doc/API.md` | ✅ |

`Project_Structure.md` does not duplicate `AGENTS.md`: there are facts and pitfalls there, here — **where things are and where to add new ones**.

---

### Step 11 — Remove the obsolete X11 fallback, keep `TextCopy` for Windows ✅

**Goal:** the Linux clipboard path uses only `wl-paste`; on Windows — `TextCopy`.

**Changes in `WaylandClipboardAutomationService.cs` (done):**

1. `ReadClipboardAsync`:
   - Linux: if `WaylandSession` — `wl-paste --no-newline`; if `wl-paste` is unavailable → empty string + `LogWarning`.
   - Linux without Wayland: empty string + `LogWarning` (X11 is not supported).
   - Windows (and other OS): `TextCopy.ClipboardService.GetTextAsync`.
2. The X11 branch and mentions of it were removed from the XML documentation.
3. `TextCopy` is **not called on Linux**.
4. Simulated `Ctrl+C` and the 900 ms clipboard polling were not changed.

**Verification:**
- `dotnet build` — 0 warnings / 0 errors (verified 2026-10-22).
- No mentions of the obsolete package remain in the code or user documentation.
- Run with a `PATH` that has no obsolete package but has `wl-paste` → `Alt+Q` captures the text (check in a session with a live window).
- The Windows path compiles; runtime on Windows is not checked (no environment) — record this in the report.

**Documentation:** `README.md` — requirement `sudo pacman -S wl-clipboard`; `AGENTS.md` — "TextCopy Windows only".

---

### Step 12 — Config and new settings ✅

**Goal:** add the fields that control the new behavior, with backward compatibility.

**Done:** `AppConfig` extended (`AutoSynthesizeOnHotkey`, `ActivateOnHotkey`, `ChunkingMode`, `BatchSize`, `ExportFormat`); `JsonSettingsService` stores enums as strings; `SettingsViewModel` has new properties, clamps `BatchSize` (1–10) and rounds speed/volume to `0.1`.

#### 12.1 New `AppConfig` fields

```csharp
public bool AutoSynthesizeOnHotkey { get; init; } = true;
public bool ActivateOnHotkey { get; init; } = false;
public TextChunkingMode ChunkingMode { get; init; } = TextChunkingMode.Paragraphs;
public int BatchSize { get; init; } = 5;              // 1..10
public AudioExportFormat ExportFormat { get; init; } = AudioExportFormat.Mp4;
```

- The default chunking mode was changed from `MaxLength` to `Paragraphs` ("Абзаци") in Step 17; `PauseAfterEachBatch` was added at the same time (see §6.1).

- Old `config.json` files without these fields are read with default values.
- `JsonSettingsService`: add `JsonStringEnumConverter` (the chunking mode and export format are stored as strings, not numbers).
- `SettingsViewModel`: new `[ObservableProperty]` (`AutoSynthesizeOnHotkey`, `ActivateOnHotkey`, `SelectedChunkingMode`, `BatchSize`, `SelectedExportFormat`) + `BatchSize` validation (clamp 1–10) and `Math.Round(Speed/Volume, 1)`.
- `SettingsViewModel.SaveAsync` saves the new fields.
- `SettingsViewModel.ApplyConfig` rounds speed/volume to `0.1`.
- New settings take effect **immediately** after a change (not only after «Зберегти» (Save)); «Зберегти» only writes to disk.

**Verification:** an old config loads; a new one is saved; after restart the values persist.

---

### Step 13 — Text chunking ✅

**Goal:** split the text into logical fragments without cutting sentences in the middle.

**Done:** `ITextChunker` + `SentenceAwareTextChunker`; three modes; handling of abbreviations and initials; over-long sentences are cut at the last space or comma; empty text → 0 fragments. Verified with a temporary harness outside the repository (14/14 PASS).

**New types:**

```csharp
public enum TextChunkingMode { Paragraphs, Sentences, MaxLength }

public sealed record TextChunkingOptions
{
    public const int DefaultMaxChunkLength = 250;
    public TextChunkingMode Mode { get; init; } = TextChunkingMode.MaxLength;
    public int MaxChunkLength { get; init; } = DefaultMaxChunkLength;
}

public sealed record TextFragment(int Index, string Text); // Index starts at 1

public interface ITextChunker
{
    IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options);
}
```

**`SentenceAwareTextChunker` algorithm:**

1. Normalization: `\r\n` → `\n`; redundant spaces and tabs inside a paragraph are collapsed; empty lines are kept as paragraph boundaries.
2. Splitting into sentences: after `.`, `!`, `?`, `…`, if followed by a space or the end and the next character is an uppercase letter, a quote, or a digit.
3. A minimal list of abbreviations after which we do **not** cut: `т.д.` (etc.), `т.п.` (etc.), `і т.ін.` (and so on), `р.` (year), `м.` (city), `вул.` (street), `ім.` (named after), `проф.` (professor), `акад.` (academician), and initials (`І. І.`). If in doubt — better not to cut.
4. Modes:
   - `Paragraphs` — paragraphs; a paragraph longer than `MaxChunkLength` is additionally split by sentences.
   - `Sentences` — one sentence = one fragment; an over-long sentence is cut at the last space or comma before the limit.
   - `MaxLength` (default) — consecutive sentences are packed up to `250` characters; a sentence is not cut if it fits; if one sentence is longer than 250, it is cut at the last space or comma, and as a last resort — hard-cut.
5. Empty or whitespace-only fragments are discarded; very short ones (< 20 characters) are merged with a neighbor where possible.
6. `Index` is assigned after filtering, starting at 1.

**Verification:** a temporary harness outside the repository (`/tmp/opencode/...`) on 5–6 texts:
- several paragraphs;
- a long sentence over 250 characters;
- text without punctuation;
- abbreviations `т.д.` and initials;
- a list with line breaks;
- empty or whitespace-only text → 0 fragments.

Expected: no fragment exceeds 250 characters unless there is nowhere to split; a sentence is not cut in the middle without need.

**DI:** `services.AddSingleton<ITextChunker, SentenceAwareTextChunker>();`

---

### Step 14 — Batch playback queue ✅

**Goal:** synthesize and play fragments sequentially, in batches of N, with a pause between batches.

**Done:** `IAudioPlayerService.TrackEnded`; `IFragmentPlaybackService` + `FragmentPlaybackService` (prefetch of one fragment, batches, pause/resume, stop, counters); `PlaybackControlViewModel` integrated; `QuickTtsViewModel` now splits the text and starts the queue. Verified on a live server: 5 fragments / 3 batches, pause/resume, stop — ALL PASSED.

#### 14.1 `IAudioPlayerService`: natural completion event

```csharp
/// <summary>Raised after the stream ends naturally (not after Stop()).</summary>
event EventHandler? TrackEnded;
```

- `BassAudioPlayerService.OnStreamEnded`, after checking `channel == _streamHandle`, sets `Stopped` and raises `TrackEnded`.
- The event does not fire on `Stop()` or on stream replacement.
- Handlers do not block the BASS thread: the queue completes a `TaskCompletionSource` created with `RunContinuationsAsynchronously`.

#### 14.2 `IFragmentPlaybackService`

```csharp
public interface IFragmentPlaybackService
{
    event EventHandler<FragmentStartedEventArgs>? FragmentStarted;
    event EventHandler<BatchCompletedEventArgs>? BatchCompleted;
    event EventHandler? QueueCompleted;
    event EventHandler<Exception>? Failed;

    bool HasQueue { get; }
    bool IsBatchPending { get; }
    int CurrentFragment { get; }
    int TotalFragments { get; }
    int CurrentBatch { get; }
    int TotalBatches { get; }

    Task StartAsync(
        IReadOnlyList<TextFragment> fragments,
        SpeechQueueOptions options,
        CancellationToken cancellationToken = default);

    Task ContinueAsync(CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
    void Stop();
}
```

`SpeechQueueOptions`: `Voice`, `Speed`, `BatchSize` (clamp 1–10).

`FragmentStartedEventArgs`: the fragment, the batch number, the total number of batches.
`BatchCompletedEventArgs`: the completed batch, the total number of batches, whether there is a next one.

#### 14.3 `FragmentPlaybackService` (Infrastructure/Playback)

- Dependencies: `ITtsApiClient`, `IAudioPlayerService`, `ILogger`.
- `StartAsync`:
  - stops the previous queue and audio;
  - creates an internal `CancellationTokenSource` linked to the external one;
  - starts a background pipeline and **returns as soon as the first fragment has started playing** (so the UI is not "busy" the whole time);
  - if the first fragment fails — throws `TtsApiException` outward.
- Pipeline of one batch:
  1. synthesize fragment `i`;
  2. while it plays, pre-synthesize `i+1` (one step ahead — "seamless imitation");
  3. `PlayAsync(wav)` → waits for `TrackEnded` → next;
  4. stops after `BatchSize` fragments or at the end of the text.
- Between batches:
  - `IsBatchPending = true`, `BatchCompleted`;
  - the audio is already `Stopped`, the queue does not advance;
  - `ContinueAsync` starts the next batch from the same place.
- `Pause`/`Resume`:
  - `Pause` → `_audio.Pause()` and the pause flag; the pipeline does not start a new fragment;
  - `Resume` → `_audio.Resume()` and clears the flag;
  - if the pause happened between fragments — a new fragment does not start until `Resume`.
- `Stop`:
  - cancels the CTS, stops the audio, resets `IsBatchPending` and the counters;
  - `StartAsync`/`ContinueAsync` finish cleanly without an exception.
- Errors after start (network, 500) → `Failed`, the queue stops; the UI shows the text.
- Synthesis requests are **sequential** (not parallel) so as not to overload the GPU.

#### 14.4 `PlaybackControlViewModel`

- Add the `IFragmentPlaybackService` dependency.
- Proxy the counters:
  - `FragmentPositionText` — `"Фрагмент 2 з 5 · пакет 1 з 3"` (Fragment 2 of 5 · batch 1 of 3);
  - `IsBatchPending`, `ContinueButtonText` — `"Наступний пакет (2 з 3)"` (Next batch (2 of 3)).
- `TogglePlayPause`:
  - if `IsBatchPending` → `ContinueAsync()`;
  - if playing or paused → `Pause()` / `Resume()` respectively.
- `Stop` → `_fragmentPlayback.Stop()` (if there is a queue), otherwise `_audioPlayer.Stop()`.
- Keep `PlayAsync(byte[])` for the future Reader (a single WAV).
- Subscriptions to queue events update the properties via `OnUiThread`.

**DI:** `services.AddSingleton<IFragmentPlaybackService, FragmentPlaybackService>();`

---

### Step 15 — `Alt+Q`: auto-speech and window activation ✅

**Goal:** after the hotkey, you do not need to press "Speak" manually.

**Done:** `QuickTtsViewModel` got `CaptureForHotkeyAsync` and `SynthesizeCurrentTextAsync` (the command delegates to them); `MainWindowViewModel` runs `HandleHotkeyAsync` in the order capture → activation → synthesis, has `SelectedTabIndex`, the `ActivateRequested` event and protection against a repeated `Alt+Q`; `MainWindow.axaml` binds `SelectedIndex`; `App.axaml.cs` activates the window. Verified with a harness with fake services (25/25 PASS): order of actions, both toggles, failed capture, button without activation, ignoring a repeated `Alt+Q`. A physical `Alt+Q` requires manual verification by the user, because the hook ignores simulated events.

**Order of actions (critical):**

1. Capture the selected text (the simulated `Ctrl+C` goes to the **active foreign** window).
2. If the capture fails — do not activate anything and do not speak.
3. If `Settings.ActivateOnHotkey`:
   - `SelectedTabIndex = 0` (Quick TTS);
   - `ActivateRequested?.Invoke(...)`.
4. If `Settings.AutoSynthesizeOnHotkey` — start synthesis of the first batch.

**The window must not be activated before step 1** — otherwise `Ctrl+C` goes to the app itself and copies nothing.

**Changes:**

- `QuickTtsViewModel`:
  - extract `CaptureSelectedTextCoreAsync` (shared by the button and the hook);
  - `CaptureSelectedTextCommand` — capture only (no auto-synthesis);
  - public `Task<bool> CaptureForHotkeyAsync(CancellationToken)`;
  - public `Task SynthesizeCurrentTextAsync(CancellationToken)` (the command delegates to it).
- `MainWindowViewModel`:
  - `[ObservableProperty] int SelectedTabIndex`;
  - `event EventHandler? ActivateRequested`;
  - `OnHotkeyPressed` → on the UI thread `HandleHotkeyAsync()`: capture → activate (per settings) → synthesize (per settings);
  - protection against a repeated `Alt+Q`: if a capture is already running — ignore.
- `MainWindow.axaml`: `TabControl SelectedIndex="{Binding SelectedTabIndex}"`.
- `App.axaml.cs`: subscription to `ActivateRequested`:
  ```csharp
  viewModel.ActivateRequested += (_, _) =>
  {
      if (window.WindowState == WindowState.Minimized)
      {
          window.WindowState = WindowState.Normal;
      }
      window.Activate();
  };
  ```
- The «Захопити виділене» (Capture selection) button does **not** activate the window and does **not** start auto-synthesis.

**Known limitation:** on Wayland the compositor may block focus stealing. `Activate()` is best effort; the README should note that the behavior depends on the compositor.

---

### Step 16 — Audio export (MP4 / WAV) ✅

**Goal:** save the whole text from the Quick TTS field into a single `.mp4` (default) or `.wav` file.

**Done:** `ITtsApiClient.ConvertAsync` (multipart `file` + `format`), `WavAudioConcatenator` (RIFF/fmt/data validation and concatenation of PCM chunks), `IAudioExportService` + `AudioExportService` (sequential synthesis → concatenation → conversion → atomic write, progress `AudioExportProgress`), `IFileSaveDialogService` + `AvaloniaFileSaveDialogService`, the «Зберегти аудіо…» (Save audio…) button with progress in `QuickTtsView`, DI registration. Verified: build 0/0; a harness with fake services (49 checks: headers, order, format errors, progress stages, MP4 via `ConvertAsync`, cancellation without files, overwriting, ViewModel flow); a live server (voice «Вероніка Дорош», 3 sentences) — WAV 4.64 s and MP4 4.65 s (AAC, `ftyp isom`), both decoded by pydub.

**Semantics:**

- the **entire current text** from the Quick TTS field is exported, not just the batches that were listened to;
- export does not depend on the queue and can run without listening first;
- before export starts, the current playback and queue are **stopped** (R18);
- export is a separate sequential synthesis of all fragments, with no parallel requests to the GPU.

#### 16.1 `ITtsApiClient.ConvertAsync`

```csharp
/// <summary>Converts WAV to the given format via POST /api/convert.</summary>
Task<byte[]> ConvertAsync(
    byte[] wavData,
    string format,
    CancellationToken cancellationToken = default);
```

- `TtsApiClient`: multipart `file` (WAV) + `format` (`"mp4"` / `"wav"`), reads the response as `byte[]`.
- Errors — via the existing `SendAsync` / `ReadErrorMessageAsync` (JSON or plain text).
- `PreviewTtsApiClient` in `DesignData` — returns `Array.Empty<byte>()`.

**Verified on the server venv:** `pydub.AudioSegment.export(format="mp4")` creates an audio-only MP4 (`ftyp isom`, AAC). `format="m4a"` **does not work** on this machine — do not use it. The server's `/api/convert` already accepts `mp4`; the client only passes the format. For MP4 the server needs `ffmpeg` (`static-ffmpeg` is already in `Server_py/requirements.txt`).

#### 16.2 `WavAudioConcatenator` (Infrastructure/Audio)

- Input: `IReadOnlyList<byte[]>` of WAV fragments (24 kHz, mono, 16-bit PCM).
- Output: one WAV `byte[]`.
- RIFF parsing: check `RIFF`/`WAVE`, find `fmt ` and `data`, check that all have `audioFormat=1`, `channels=1`, `sampleRate=24000`, `bitsPerSample=16`; otherwise — `InvalidDataException` with an explanation.
- Concatenation: all `data` chunks back to back plus a new correct header (`RIFF size = 36 + totalData`, `data size = totalData`).
- Extra source chunks are ignored; the fragment order is preserved.
- No artificial gaps are inserted: the export must sound the same as sequential playback.
- A pure synchronous class, with no dependencies on BASS or HTTP.

#### 16.3 `IAudioExportService` + `AudioExportService`

```csharp
public sealed record AudioExportRequest
{
    public required string Text { get; init; }
    public string? Voice { get; init; }
    public double Speed { get; init; } = 1.0;
    public TextChunkingOptions Chunking { get; init; } = new();
    public AudioExportFormat Format { get; init; } = AudioExportFormat.Mp4;
    public required string DestinationPath { get; init; }
}

public enum AudioExportStage { Synthesizing, Concatenating, Converting, Writing }

public sealed record AudioExportProgress(
    AudioExportStage Stage,
    int CompletedFragments,
    int TotalFragments);

public interface IAudioExportService
{
    Task ExportAsync(
        AudioExportRequest request,
        IProgress<AudioExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

**`AudioExportService` algorithm:**

1. `ITextChunker.Split(request.Text, request.Chunking)`; an empty result → `ArgumentException("Немає тексту для збереження.")` (no text to save).
2. For each fragment, sequentially: `ITtsApiClient.SynthesizeAsync(TtsRequestDto { Text, Voice, Speed, Volume = 1.0, Translate = false })`; collect the WAVs in a `List<byte[]>`; report `Synthesizing (i, N)`.
3. `WavAudioConcatenator.Concat(list)`; report `Concatenating`.
4. `Wav` → write the WAV bytes; `Mp4` → `ConvertAsync(wav, "mp4")`, report `Converting`, write the result.
5. The write is atomic: a temporary file in the same directory → `File.Move(temp, destination, overwrite: true)`; on cancel or error the temporary file is deleted.
6. `IOException` and `UnauthorizedAccessException` are not swallowed — the ViewModel shows a clear message.
7. Memory: all WAV fragments are kept in memory (24 kHz / 16-bit ≈ 48 KB/s). This is acceptable for typical selections; for very long books it is a future optimization (streaming to a temporary file), not done now.

#### 16.4 File dialog

```csharp
public interface IFileSaveDialogService
{
    Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default);
}
```

- Implementation `AvaloniaFileSaveDialogService` (`Views/Services` — the only UI-specific service, so that `Infrastructure` does not depend on Avalonia):
  - takes `IStorageProvider` from `Application.Current.ApplicationLifetime` → `MainWindow`;
  - `SaveFilePickerAsync` with `FilePickerSaveOptions`:
    - `SuggestedFileName = "UA_TTS_yyyy-MM-dd_HH-mm"`;
    - `DefaultExtension` and `FileTypeChoices` according to the selected format (`.mp4` or `.wav`);
    - `ShowOverwritePrompt = true`.
  - returns a local path, or `null` if the user cancelled.
- Registration in `App.axaml.cs`; in `DesignData` — a fake that returns `null`.
- The dialog is called only from the UI thread.

**DI:**

```csharp
services.AddSingleton<IAudioExportService, AudioExportService>();
services.AddSingleton<IFileSaveDialogService, AvaloniaFileSaveDialogService>();
```

#### 16.5 QuickTts: button and progress

- The **«Зберегти аудіо…»** (Save audio…) button next to «Озвучити» (Speak).
- `ExportCommand` (`IncludeCancelCommand = true`), `IsExporting`, `ExportProgressText`, `ExportProgressPercent`.
- `CanExport => !IsBusy && !IsExporting && !string.IsNullOrWhiteSpace(Text)`.
- During export `CanCapture` / `CanSynthesize` = `false`, so the text does not change in the middle of the operation.
- Flow:
  1. the format is taken from `Settings.ExportFormat`;
  2. `IFileSaveDialogService.PickSavePathAsync(...)`; cancel → exit without changes;
  3. `_playback.Stop()` (if playing);
  4. an `AudioExportRequest` with the current text, voice, speed and `ChunkingMode`;
  5. `_audioExport.ExportAsync(...)`, progress shown in the status:
     - `"Експорт: синтез 12 із 30…"` (Export: synthesis 12 of 30…);
     - `"Експорт: склеювання…"` (Export: concatenating…);
     - `"Експорт: конвертація в MP4…"` (Export: converting to MP4…);
     - `"Збережено: /path/file.mp4"` (Saved: /path/file.mp4);
  6. cancel → `"Експорт скасовано."` (Export cancelled.), no file is created.
- An MP4 conversion error → show the server's text; a hint in the README: try WAV.

---

### Step 17 — UI: settings, counters, sliders ✅

**Done:** in settings — checkboxes `AutoSynthesizeOnHotkey` / `ActivateOnHotkey`, a ComboBox for the chunking mode (`ChunkingModeOption` + `SelectedChunkingMode`; default «Абзаци» (Paragraphs)), a `NumericUpDown` «Фрагментів у пакеті» (Fragments per batch) (1–10, step 1), a checkbox «Пауза після кожного пакета» (Pause after each batch) (`PauseAfterEachBatch`; when it is off, `FragmentPlaybackService` starts the next batch itself), a ComboBox for the export format (`ExportFormatOption`), and speed/volume sliders with a step of 0.1 and the `0.0` format. In Quick TTS — the position «Фрагмент N з M · пакет X з Y» (Fragment N of M · batch X of Y), a preview of the current fragment (up to 60 characters), the «Наступний пакет (X з Y)» (Next batch (X of Y)) button when `IsBatchPending` is set, and statuses for preparation, batch completion and queue completion with correct Ukrainian plural forms; in the mini player — the position in the queue and the volume with a step of 0.1. Queue errors now reach `ErrorMessage` in Quick TTS (the `Playback.QueueFailed` event). Verified: build 0/0; harness — 64/64 PASS.

#### 17.1 SettingsView

- Checkbox **«Озвучувати автоматично після Alt+Q»** (Auto-speak after Alt+Q) → `AutoSynthesizeOnHotkey` (default ✅).
- Checkbox **«Активувати вікно й переходити на Quick TTS після Alt+Q»** (Activate window and switch to Quick TTS after Alt+Q) → `ActivateOnHotkey` (default ☐).
- ComboBox **«Режим фрагментації»** (Chunking mode):
  - «Абзаци» (Paragraphs) — default (clarified after manual testing);
  - «Речення» (Sentences);
  - «До 250 символів (розумне пакування)» (Up to 250 characters (smart packing)).
  - Implementation: a list of `ChunkingModeOption(Mode, Title)` + `SelectedChunkingMode`, without magic indexes.
- `NumericUpDown` **«Фрагментів у пакеті»** (Fragments per batch): min 1, max 10, increment 1, default 5.
- Checkbox **«Пауза після кожного пакета»** (Pause after each batch) → `PauseAfterEachBatch` (default ✅; when off, all batches play one after another without the «Наступний пакет» (Next batch) button).
- ComboBox **«Формат збереження аудіо»** (Audio save format):
  - «MP4 (аудіо, типово)» (MP4 (audio, default));
  - «WAV (без стиснення)» (WAV (uncompressed)).
  - Implementation: `ExportFormatOption(Format, Title)` + `SelectedExportFormat`.
- Sliders:
  - `TickFrequency="0.1"`, `IsSnapToTickEnabled="True"`;
  - display `StringFormat='{}{0:0.0}'` (no thousandths);
  - `Speed` 0.5–2.0; `Volume` 0.0–1.0;
  - in the mini player the volume also uses a step of 0.1 and the same format.
- A hint that the new settings take effect immediately, while the server URL takes effect after restart.

#### 17.2 QuickTtsView

- Below the buttons or in the status: `"Фрагмент 2 з 5 · пакет 1 з 3"` (Fragment 2 of 5 · batch 1 of 3) (`Playback.FragmentPositionText`).
- Display of the current fragment (first ~60 characters) — optional, so it is visible what is being read.
- The **«Наступний пакет (2 з 3)»** (Next batch (2 of 3)) button — visible when `Playback.IsBatchPending` is set.
- The **«Зберегти аудіо…»** (Save audio…) button — next to «Озвучити» (Speak); during export the progress and «Скасувати експорт» (Cancel export) are shown.
- Statuses:
  - `"Підготовлено 12 фрагментів · пакет 1 з 3"` (Prepared 12 fragments · batch 1 of 3);
  - `"Пакет 1 з 3 завершено. Натисніть ▶ або «Наступний пакет»"` (Batch 1 of 3 completed. Press ▶ or “Next batch”);
  - `"Готово: 3 пакети, 12 фрагментів"` (Done: 3 batches, 12 fragments);
  - `"Експорт: синтез 12 із 30…"` / `"Збережено: …/file.mp4"` (Export: synthesis 12 of 30… / Saved: …/file.mp4).
- A queue or export error → `ErrorMessage` in Quick TTS.

#### 17.3 MainWindow (mini player)

- Add `Playback.FragmentPositionText` next to the state and the voice.
- The ▶/⏸ button:
  - while playing — pause;
  - while paused — resume;
  - when a batch is complete — start the next batch.
- The ■ button — a full stop of the queue and reset of the counters.

---

### Step 18 — Verification and final documentation ✅

**Done (2026-10-22):** `dotnet build` — 0 warnings / 0 errors. Manual scenarios (§4, 23 items) confirmed by the user during live work with the app: `Alt+Q` with auto-speech and activation, empty selection, chunking modes, batches and "Pause after each batch", pause/stop, sliders with step 0.1, export MP4/WAV and cancellation. Updated `Doc/API.md` (sequential `/api/synthesize`, `/api/convert` for MP4), §5–6 of this plan, `README.md`, `AGENTS.md`, `Doc/Project_Structure.md`. Known limitation: Windows runtime not verified (the `TextCopy` path is kept).

**Build:** `dotnet build` — 0 warnings / 0 errors.

**Manual scenarios (mandatory):**

| # | Scenario | Expected |
|---|---|---|
| 1 | `Alt+Q` in a browser, auto-speech ✅ | text captured, the first batch plays without pressing «Озвучити» |
| 2 | `Alt+Q` with activation ✅ | text is captured first, then the window is active, the Quick TTS tab is open, sound plays |
| 3 | Activation ☐ | the window does not steal focus, but the text is captured and spoken |
| 4 | Empty selection | status «Нічого не захоплено» (Nothing captured), the window does not activate |
| 5 | "Абзаци" (Paragraphs) mode | fragments match paragraphs; long paragraphs are additionally split |
| 6 | "Речення" (Sentences) mode | one fragment = one sentence, no cut-offs |
| 7 | "До 250" (Up to 250) mode | no fragment over 250 characters (except indivisible cases) |
| 8 | Batch 5, text of 12 fragments | batch 1 (1–5) plays, then a pause; ▶ or «Наступний пакет» → 6–10; then 11–12 |
| 9 | Batch 1 and batch 10 | batch boundaries work, counters do not get out of sync |
| 10 | Pause in the middle of a fragment | sound stops, ▶ resumes from the same place |
| 11 | Pause between fragments | the next fragment does not start until ▶ |
| 12 | Stop during a batch | sound and queue stop, counters reset |
| 13 | New `Alt+Q` during playback | the previous queue stops, a new one starts |
| 14 | Sliders | step 0.1, values `1.0`, `0.7`, no `0.30000000000000004` |
| 15 | `PATH` without X11 utilities | capture via `wl-paste` works, the obsolete fallback is not called |
| 16 | Old `config.json` | loads, new fields = default values |
| 17 | Restart | new settings are saved and applied |
| 18 | MP4 export (default) | after choosing a path an `.mp4` is created that plays in a player; sound = the whole text |
| 19 | WAV export | a valid WAV is created (24 kHz, mono, 16-bit), duration ≈ sum of fragments |
| 20 | Export cancellation | no temporary or target file remains, status «Експорт скасовано» (Export cancelled) |
| 21 | Export during playback | playback stops, export starts, after completion the file is in place |
| 22 | Export of text from 1 fragment | MP4/WAV are created correctly (no empty headers) |
| 23 | MP4 not converted (no ffmpeg) | the server error is shown; WAV is still saved |

**Documentation after verification:**

- `README.md`: new features, settings, batches, MP4/WAV export (MP4 — audio without video), limitations of activation on Wayland, requirements: only `wl-clipboard`.
- `AGENTS.md`: new facts (TrackEnded, batch queue, order "capture → activate → synth", `pydub mp4` works, `m4a` does not, WAV concatenation), updated `Doc/` table, git state.
- `Doc/Project_Structure.md`: new files and flows, including export.
- `Doc/API.md`: a note that the client makes **sequential** `/api/synthesize` requests — one per fragment; for MP4 — one `POST /api/convert`.
- This plan: step statuses and journal.

---

## 5. UX specification (brief)

The labels in the blocks below are the actual Ukrainian UI texts.

### Settings

```text
[✓] Озвучувати автоматично після Alt+Q
[ ] Активувати вікно й переходити на Quick TTS після Alt+Q

Режим фрагментації: [Абзаци ▼]
Фрагментів у пакеті: [ 5 ▲▼]   (1–10)
[✓] Пауза після кожного пакета (кнопка «Наступний пакет»)
Формат збереження аудіо: [MP4 (аудіо, типово) ▼]  /  [WAV (без стиснення)]

Швидкість мовлення  [────●────] 1.0
Гучність відтворення [──────●──] 0.8
```

### Quick TTS states

| State | Text (UI) |
|---|---|
| Preparing | `Підготовлено 12 фрагментів · пакет 1 з 3` |
| Playing | `Фрагмент 2 з 5 · пакет 1 з 3` |
| Paused | `Пауза · фрагмент 2 з 5` |
| End of batch | `Пакет 1 з 3 завершено. Натисніть ▶ або «Наступний пакет»` |
| End of text | `Готово: 3 пакети, 12 фрагментів` |
| Export | `Експорт: синтез 12 із 30…` → `Збережено: …/file.mp4` |
| Error | `TtsApiException` text in a red line |

---

## 6. Contracts (summary)

### 6.1 `AppConfig`

```csharp
public bool AutoSynthesizeOnHotkey { get; init; } = true;
public bool ActivateOnHotkey { get; init; } = false;
public TextChunkingMode ChunkingMode { get; init; } = TextChunkingMode.Paragraphs;
public int BatchSize { get; init; } = 5;              // 1..10
public bool PauseAfterEachBatch { get; init; } = true;
public AudioExportFormat ExportFormat { get; init; } = AudioExportFormat.Mp4;
```

### 6.2 `IAudioPlayerService`

Only this is added:

```csharp
event EventHandler? TrackEnded;
```

### 6.3 `IFragmentPlaybackService`

See §14.2. Key points: `StartAsync` returns after the first fragment starts; a batch completes with the `BatchCompleted` event; `ContinueAsync` starts the next batch.

### 6.4 `ITextChunker`

See §13. A pure, synchronous class with no dependencies on Avalonia, HTTP or BASS.

### 6.5 `ITtsApiClient.ConvertAsync`

See §16.1. Uses the existing server `POST /api/convert`; formats `mp4` and `wav`.

### 6.6 `IAudioExportService`

See §16.3. Exports **the whole text**: synthesizes fragments sequentially, concatenates the WAVs and, if needed, converts to MP4.

---

## 7. Risks and mitigation

| Risk | Likelihood | Mitigation |
|---|---|---|
| Wayland does not allow activating the window (focus stealing) | medium | `Activate()` is best effort; the setting is disabled by default; describe it in the README |
| Activation before capture breaks `Ctrl+C` | high (if the order is mixed up) | Strict order: capture → activate → synth; test №2 |
| An audible pause between fragments | medium | Prefetch one fragment ahead; the BASS stream is created quickly |
| Pause during synthesis of the next fragment | medium | Pause gate: do not start a new fragment until `Resume` |
| Race of `Stop` / `TrackEnded` / a new `Start` | medium | One internal CTS, a `TaskCompletionSource` with `RunContinuationsAsynchronously`, state locking |
| `TrackEnded` arrives from the BASS thread | high | Do not update the UI directly; `OnUiThread` in the ViewModel |
| Old config without the new fields | low | Default values; `JsonStringEnumConverter` |
| A naive sentence split breaks abbreviations | medium | A minimal abbreviation list; better not to cut than to cut wrongly |
| `wl-paste` missing | low | `LogWarning`, empty result; the `wl-clipboard` requirement in the README |
| `StringFormat` in compiled bindings | low | Check with `dotnet build`; if needed — a computed `SpeedText` property |
| MP4 conversion depends on ffmpeg on the server | medium | The server's `/api/convert` returns an error — show it; suggest WAV |
| Different formats of WAV fragments | low | `WavAudioConcatenator` checks `fmt ` and throws a clear `InvalidDataException` |
| Memory during export of a long text | low | Typical selections are a few MB at most; for books — future streaming to disk |
| Wrong extension in the file dialog | low | `DefaultExtension` + `FileTypeChoices`; the name is built from the chosen format |
| Windows runtime not verified | high | `TextCopy` path kept; record it as unverified in the report |

---

## 8. Execution journal (iteration 2)

| Date | What was done |
|---|---|
| 2026-10-22 | Plan for iteration 2 drafted based on `Notes_Userstory.md`; decisions R7–R18 recorded |
| 2026-10-22 | **Step 10 done:** `Doc/Project_Structure.md` created; `Doc/ua_tts_client_arch.md` deleted; `AGENTS.md`, `README.md`, `Doc/API.md` updated |
| 2026-10-22 | **Step 11 done:** `WaylandClipboardAutomationService` reads the clipboard on Linux only through `wl-paste`; `TextCopy` kept for Windows; `dotnet build` — 0 warnings / 0 errors |
| 2026-10-22 | **Step 12 done:** new `AppConfig` fields, `JsonStringEnumConverter`, new `SettingsViewModel` properties with validation and rounding to 0.1 |
| 2026-10-22 | **Step 13 done:** `TextChunkingMode`, `TextChunkingOptions`, `TextFragment`, `ITextChunker`, `SentenceAwareTextChunker`; harness — 14/14 PASS |
| 2026-10-22 | **Step 14 done:** `TrackEnded` in the BASS service, `IFragmentPlaybackService` + `FragmentPlaybackService`, integration into `PlaybackControlViewModel` and `QuickTtsViewModel`; harness on a live server (batches, pause/resume, stop) — ALL PASSED |
| 2026-10-22 | **Step 15 done:** `CaptureForHotkeyAsync` / `SynthesizeCurrentTextAsync`, `HandleHotkeyAsync` (capture → activate → synth), `SelectedTabIndex` + `ActivateRequested`; harness with fake services — 25/25 PASS |
| 2026-10-22 | **Step 16 done:** `ConvertAsync`, `WavAudioConcatenator`, `AudioExportService`, `AvaloniaFileSaveDialogService`, the «Зберегти аудіо…» button with progress; harness 49 checks + live WAV/MP4 export (pydub: 4.64/4.65 s) — ALL PASSED |
| 2026-10-22 | **Step 17 done:** checkboxes and ComboBoxes in settings (`ChunkingModeOption`, `ExportFormatOption`), `NumericUpDown` for the batch size, sliders with step 0.1; queue counters and fragment preview in Quick TTS, the «Наступний пакет» button, `QueueFailed` event → `ErrorMessage`; harness 46/46 PASS |
| 2026-10-22 | **Step 17, clarification after manual testing:** checkbox «Пауза після кожного пакета» (`PauseAfterEachBatch`; when off, batches play back to back without `BatchCompleted`); default chunking mode is «Абзаци» (Paragraphs); harness 64/64 PASS |
| 2026-10-22 | **Step 18 done:** build 0/0; manual scenarios (23 items) confirmed by the user; final documentation (`API.md`, plan §5–6, `README`, `AGENTS`, `Project_Structure`); **iteration 2 completed** |
| | | |

---

## 9. How to continue

1. Iteration 2 is **completed**: all steps 10–18 ✅; the history is in the journal above.
2. For the next iteration create a new plan (archive this file or add a new section), based on `README.md`, `Doc/Project_Structure.md` and `AGENTS.md`.
3. Do not commit other people's uncommitted changes without a request.
4. Known limitations: Windows runtime not verified; MP4 export depends on `ffmpeg` on the server; long texts are kept in memory during export; window activation on Wayland is best effort.

---

## 10. Out of scope for the iteration

- Test project `UA_TTS_Client.Tests` (deliberately).
- Reader and Character Voice.
- Hotkey editor (stays `Alt+Q`).
- Changes to `server.py` / API (the existing `/api/convert` is used).
- Runtime verification of Windows 11.
- Automatic continuation of batches without user participation.
- Video track in MP4 (only the audio track is exported).
