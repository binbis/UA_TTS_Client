# Execution flows

> **Purpose:** the order of operations for startup, the hotkey, synthesis and playback, export and settings. Read this before changing any of these flows: the order matters.
> **Audience:** developers and AI agents.
> **Source of truth:** `ViewModels/MainWindowViewModel.cs`, `ViewModels/Tabs/QuickTtsViewModel.cs`, `ViewModels/Components/PlaybackControlViewModel.cs`, `Infrastructure/Playback/FragmentPlaybackService.cs`, `Infrastructure/Audio/AudioExportService.cs`, `Infrastructure/Automation/*.cs`, `App.axaml.cs`.
> **Verified:** 2026-11-03.

## 1. Startup

`App.OnFrameworkInitializationCompleted` builds the DI container and creates `MainWindow` with `MainWindowViewModel` as the DataContext. When the window opens, `MainWindowViewModel.InitializeAsync()` runs these steps:

1. Subscribe to `IGlobalHotkeyService.HotkeyPressed` and call `Start()`.
2. `SettingsViewModel.InitializeAsync()` loads the voices (`GET /api/voices`).
3. `RefreshServerStatusAsync()` calls `GET /api/health`. On 404 it falls back to `GET /api/voices` (an older server without the health endpoint).
4. `MonitorServerAsync()` checks the server every 15 seconds with a `PeriodicTimer`.

If initialization throws, the server status becomes an error with the text «Помилка ініціалізації» (Initialization error).

## 2. Alt+Q: from the hotkey to speech

1. `SharpHookGlobalHotkeyService` tracks the Alt keys (left and right) and `Q`. Pressing `Q` while Alt is held arms the hotkey. Auto-repeat of `Q` does not make it fire twice. The hotkey is raised only after **both** keys are released. Simulated events are ignored (`IsEventSimulated`).
2. The event is raised on the thread pool. `MainWindowViewModel.OnHotkeyPressed` marshals it to the UI thread: `OnUiThread(() => HandleHotkeyAsync())`.
3. If a capture is already running (`_hotkeyCaptureInProgress`), the new event is ignored.
4. **Capture, always first.** `QuickTtsViewModel.CaptureForHotkeyAsync` calls `IClipboardAutomationService.CaptureSelectedTextAsync`:
   - remembers the current clipboard text as the baseline;
   - releases Alt (left and right), then presses and releases `Ctrl+C` through SharpHook `EventSimulator`; if that fails, the `ydotool` fallback runs;
   - polls the clipboard every 50 ms for at most 900 ms until the text differs from the baseline;
   - on Linux the text comes from `wl-paste --no-newline`; on Windows from TextCopy.
   If the text never changes, the result is empty. This includes a selection identical to the current clipboard text.
5. **Empty result:** the status «Нічого не захоплено — виділіть текст в активному вікні.» (Nothing captured — select text in the active window) is shown. The flow stops: no activation, no speech.
6. **Activation (only if `activateOnHotkey` is on):** `SelectedTabIndex` becomes 0 (Quick TTS) and `ActivateRequested` is raised. `App.axaml.cs` restores a minimized window and calls `Window.Activate()`. On Wayland the compositor may ignore the request.
7. **Speech (only if `autoSynthesizeOnHotkey` is on):** `QuickTtsViewModel.SynthesizeCurrentTextAsync` (section 3).

**The order is critical.** Activating the window before the capture makes `Ctrl+C` go to this application, so nothing is copied.

## 3. Synthesis and the playback queue

`QuickTtsViewModel.SynthesizeCurrentTextAsync` is shared by the «Озвучити» (Speak) button and by the hotkey:

1. `Text.Trim()`. An empty text leads to the status «Немає тексту для озвучення.» (No text to speak).
2. `ITextChunker.Split(text, { Mode = settings.ChunkingMode })`. The maximum length is the default 250 characters.
3. The batch size is clamped to 1–10. `IsBusy` becomes true and the status «Підготовлено N фрагментів…» (Prepared N fragments…) is shown.
4. `PlaybackControlViewModel.StartQueueAsync(fragments, voice, speed, batchSize, pauseAfterEachBatch, ct)` calls `FragmentPlaybackService.StartAsync`:
   - stops the previous queue and audio. A new `Alt+Q` therefore replaces a running queue;
   - starts a background pipeline and **returns as soon as the first fragment has started playing**;
   - in the pipeline, fragments are synthesized one at a time. While fragment *i* plays, fragment *i+1* is already being synthesized;
   - each fragment plays through `IAudioPlayerService.PlayAsync(byte[])`. The pipeline waits for `TrackEnded` before it moves on;
   - at the end of a batch, if `pauseAfterEachBatch` is on, `IsBatchPending` becomes true, `BatchCompleted` is raised and the queue waits. Otherwise the next batch starts at once;
   - after the last fragment, `QueueCompleted` is raised.
5. A failure before the first fragment starts is thrown from `StartAsync` and shown as `ErrorMessage`. A `TtsApiException` shows the server's text.
6. A failure after the first fragment starts raises `Failed`. `PlaybackControlViewModel` sets the state «Помилка аудіо» (Audio error), and `QuickTtsViewModel` shows «Черга відтворення зупинилася з помилкою. Деталі — у журналі застосунку.» (The playback queue stopped with an error. See the application log).

### Controls

| Control | Behavior |
|---|---|
| ▶/⏸ in the mini player | If a batch is pending, start the next batch. While playing, pause. While paused, resume. |
| «Наступний пакет (X з Y)» (Next batch) | Starts the next batch (`ContinueAsync`). Visible only while a batch is pending. |
| ■ in the mini player | If a queue exists or a batch is pending: stop the queue and reset the counters. Otherwise stop the audio. |
| Pause between fragments | The next fragment does not start until playback is resumed (`WaitIfPausedAsync`). |
| Pause during a fragment | The audio pauses and resumes from the same position. A pause requested right after the fragment starts is applied once `PlayAsync` has started. |

**Track end.** `IAudioPlayerService.TrackEnded` is raised only at the natural end of a track. `Stop()` and replacing the stream do not raise it.

## 4. Export (MP4 or WAV)

`QuickTtsViewModel.ExportAsync` runs in this order:

1. The format comes from `settings.ExportFormat`. The system save dialog (`IFileSaveDialogService.PickSavePathAsync`) is shown with the suggested name `UA_TTS_yyyy-MM-dd_HH-mm` and the extension `mp4` or `wav`. Cancelling changes nothing.
2. Playback is stopped (`PlaybackControlViewModel.StopCommand`). This keeps the export from competing with the queue for the GPU.
3. `IAudioExportService.ExportAsync(request, progress, ct)`:
   1. `ITextChunker.Split` with the current chunking mode. No fragments → `ArgumentException`.
   2. Each fragment is synthesized sequentially (`Volume = 1.0`, `Translate = false`). Stage: `Synthesizing`.
   3. `WavAudioConcatenator.Concat` checks every fragment (RIFF/WAVE, `fmt ` is PCM, mono, 24 kHz, 16 bit, `data` present) and writes one WAV with a new header. Stage: `Concatenating`.
   4. MP4 only: `ITtsApiClient.ConvertAsync(wav, "mp4")` → `POST /api/convert`. Stage: `Converting`.
   5. Atomic write: a hidden temporary file (`.<name>.<guid>.tmp`) is written in the target folder, then moved over the destination with `File.Move(..., overwrite: true)`. Stage: `Writing`. A cancel or an error deletes the temporary file.
4. Progress texts: «Експорт: підготовка…», «Експорт: синтез N із M…», «Експорт: склеювання…», «Експорт: конвертація в MP4…», «Експорт: запис файлу…». Success: «Збережено: <path>». Cancel: «Експорт скасовано.».
5. During the export, capture, synthesis and export buttons are disabled and `Alt+Q` capture is ignored, so the text cannot change in the middle of the operation.

## 5. Settings

- `SettingsViewModel` holds the values that the UI edits. Changes take effect immediately. The hotkey toggles are read at the moment `Alt+Q` is pressed.
- **«Зберегти»** (Save) validates the server URL (an absolute `http` or `https` URL) and then calls `ISettingsService.SaveAsync`.
- The server URL is used for `HttpClient.BaseAddress`, which is set once at startup. A change takes effect after a restart.

## 6. Tabs and window activation

- `MainWindow.axaml` binds the `TabControl` selection to `SelectedTabIndex`. Quick TTS is index 0.
- `ActivateRequested` is handled in `App.axaml.cs` (see section 2).
