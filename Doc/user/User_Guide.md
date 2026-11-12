# User guide

> **Purpose:** how to use the client: the window, Quick TTS, the `Alt+Q` hotkey, batch playback, export and settings.
> **Audience:** users.
> **Source of truth:** `Views/*.axaml` and `ViewModels/*`. The labels and messages are quoted from the code.
> **Verified:** 2026-11-12.

## Window layout

- **Status bar (top):** a dot (green when the server is online, red when it is offline), the status text, and the **«Перевірити»** (Check) button. Status texts: `Онлайн · cuda · 31 голосів` (Online · cuda · 31 voices), `Онлайн · cuda · 31 голосів · словник: помилка` (dictionary load error, synthesis runs without dictionary rules), `Офлайн` (Offline), `Помилка сервера` (Server error), `Помилка` (Error). Hover over the text to see the details. The server is checked every 15 seconds.
- **Tabs:** **Quick TTS** (the main work area), **Reader** and **Character Voice** (placeholders, not implemented), **Налаштування** (Settings).
- **Mini player (bottom):** ▶/⏸ (pause and resume; starts the next batch when one is pending), ■ (stop), the volume slider **«Гучність»** (0.0–1.0, step 0.1), the state text, and the position in the queue.

State texts of the mini player: `Готово` (Ready), `Відтворення` (Playing), `Пауза` (Paused), `Зупинено` (Stopped), `Помилка аудіо` (Audio error).

## Quick TTS

1. Type or paste text into the field, or select text in another window and click **«Захопити виділене»** (Capture selection). The client simulates `Ctrl+C` in the active window and reads the clipboard.
2. Click **«Озвучити»** (Speak). The text is split into fragments by the chunking mode from the settings, the first fragment is synthesized, and playback starts.
3. Fragments play in batches (5 per batch by default). The label shows the position, for example `Фрагмент 2 з 5 · пакет 1 з 3` (Fragment 2 of 5 · batch 1 of 3). Below it you see the first 60 characters of the current fragment.
4. When a batch ends and **«Пауза після кожного пакета (кнопка «Наступний пакет»)»** is on (the default), playback stops. Press ▶ in the mini player, or the **«Наступний пакет (X з Y)»** (Next batch (X of Y)) button, to continue. When the option is off, all batches play one after another.
5. **«Скасувати»** (Cancel) interrupts synthesis while it runs. After the first fragment has started, use ■ to stop playback.
6. **«Зберегти аудіо…»** (Save audio…) saves the whole text from the field to one file. See the export section below.

Status messages you can see:

| Message | Meaning |
|---|---|
| `Нічого не захоплено — виділіть текст в активному вікні.` | Nothing was captured. Select the text in the other window first. |
| `Захоплено N символів.` | N characters were captured. |
| `Немає тексту для озвучення.` | The field is empty. |
| `Підготовлено N фрагментів · пакет 1 з M` | Synthesis has started: N fragments in M batches. |
| `Пакет X з Y завершено. Натисніть ▶ або «Наступний пакет»` | A batch is complete and waits for you. |
| `Готово: N пакетів, M фрагментів` | The whole queue has played. |
| `Синтез скасовано.` | Synthesis was cancelled. |

Errors are shown in a separate error line. A server error shows the server's message. If the queue fails during playback, the line reads `Черга відтворення зупинилася з помилкою. Деталі — у журналі застосунку.` (The playback queue stopped with an error. See the application log.)

## Alt+Q (global hotkey)

Select text in any application and press `Alt+Q`:

1. The client captures the selection by simulating `Ctrl+C`. The hotkey fires when both keys are released.
2. If nothing was captured, nothing else happens.
3. If **«Активувати вікно й переходити на Quick TTS після Alt+Q»** is on, the window is activated and the Quick TTS tab opens. Window activation is best effort on Wayland: the compositor may refuse it.
4. If **«Озвучувати автоматично після Alt+Q»** is on (the default), the speech starts at once.

A repeated `Alt+Q` while the capture is running is ignored. A new `Alt+Q` during playback stops the current queue and starts a new one.

**Terminals:** `Ctrl+C` sends SIGINT in a terminal. Copy with `Ctrl+Shift+C`, then paste into the Quick TTS field with `Ctrl+V`.

## Export (MP3 or WAV)

1. Type the text into Quick TTS and click **«Зберегти аудіо…»** (Save audio…).
2. Choose the file in the system dialog. Cancelling changes nothing.
3. Playback stops. The progress is shown: `Експорт: підготовка…`, `Експорт: синтез N із M…`, `Експорт: склеювання…`, `Експорт: конвертація в MP3…`, `Експорт: запис файлу…`.
4. When the export finishes, the line reads `Збережено: <path>`. **«Скасувати експорт»** (Cancel export) stops the export, and no file is created.

The format comes from the settings: **MP3** (the default) or **WAV** (24 kHz, mono, 16-bit). MP3 needs a working ffmpeg on the server. If the conversion fails, save the file as WAV: WAV needs no conversion.

## Settings tab (Налаштування)

Changes take effect immediately. **«Зберегти»** (Save) writes them to `config.json`. The server address takes effect after a restart.

| Control (label in the UI) | Setting | Default | Range or options |
|---|---|---|---|
| Сервер (Server) | `serverUrl` | `http://127.0.0.1:5000` | an absolute `http` or `https` URL; takes effect after a restart |
| Голос (Voice) and **«Оновити голоси»** (Refresh voices) | `selectedVoice` | the first voice on the server | the voices from `GET /api/voices` |
| Швидкість мовлення (Speech rate) | `speed` | 1.0 | 0.5–2.0, step 0.1 |
| Гучність відтворення (Playback volume) | `volume` | 1.0 | 0.0–1.0, step 0.1 |
| Фрагментація тексту (Text chunking): Режим поділу (Split mode) | `chunkingMode` | «Абзаци» (Paragraphs) | «Абзаци», «Речення» (Sentences), «До 250 символів (розумне пакування)» (Up to 250 characters, smart packing) |
| Фрагментів у пакеті (Fragments per batch) | `batchSize` | 5 | 1–10 |
| Пауза після кожного пакета (кнопка «Наступний пакет») (Pause after each batch) | `pauseAfterEachBatch` | on | on / off |
| Експорт аудіо (Audio export): Формат збереження (Save format) | `exportFormat` | «MP3 (аудіо, типово)» | «MP3 (аудіо, типово)», «WAV (без стиснення)» |
| Гаряча клавіша (Hotkey) | `hotkey` | `Alt+Q` | informational only; cannot be changed yet |
| Озвучувати автоматично після Alt+Q (Speak automatically after Alt+Q) | `autoSynthesizeOnHotkey` | on | on / off |
| Активувати вікно й переходити на Quick TTS після Alt+Q (Activate the window and switch to Quick TTS after Alt+Q) | `activateOnHotkey` | off | on / off |

The chunking modes are described in `architecture/Text_Chunking.md`. The config file is described in `user/Configuration.md`.

## Glossary

| UI term | Meaning | Code term |
|---|---|---|
| Фрагмент (fragment) | A piece of text sent to the server in one request | `TextFragment` |
| Пакет (batch) | A group of N fragments that plays before a pause | `BatchSize`, `CurrentBatch` |
| Черга (queue) | All fragments of the current text, in order | `IFragmentPlaybackService` |
| Озвучити (Speak) | Synthesize and play the text of the field | `SynthesizeCommand` |
| Захопити виділене (Capture selection) | Copy the selection into the field | `CaptureSelectedTextCommand` |
| Зберегти аудіо (Save audio) | Export the whole text of the field to a file | `ExportCommand`, `IAudioExportService` |
