# План робіт UA_TTS_Client — ітерація 2

> **Статус:** активний. Оновлено 2026-10-22. Кроки 10–14 виконано; наступний — **Крок 15**.
> **Джерело вимог:** `Notes_Userstory.md` (нотатки після ручного користування робочою версією).
> **База:** робоча версія після Кроків 0–9; автотести свідомо **не** створюються.
> **Перед початком роботи прочитати:**
> 1. `AGENTS.md` — перевірені факти, точні API, підводні камені;
> 2. `Doc/Project_Structure.md` — фактична карта коду й точки розширення (створено на Кроці 10).
>
> Цей файл відповідає на *«що і в якому порядку»*. `AGENTS.md` — на *«як саме»*.

---

## 1. Стан на вході

- Кроки 0–9 виконано; застосунок запускається, синтез і відтворення працюють.
- **Кроки 10–14 виконано** (документація, X11-fallback, конфіг, фрагментація, пакетна черга) — див. журнал.
- Крок 9.1 (тестовий проєкт) **закривається як неактуальний** — на цьому етапі автотести не потрібні.
- Виявлені користувачем UI/UX-потреби:
  1. після `Alt+Q` текст захоплюється, але озвучення треба запускати вручну;
  2. весь текст іде на сервер одним запитом — потрібна фрагментація;
  3. потрібні налаштування режиму фрагментації та автозапуску;
  4. у Quick TTS немає лічильника фрагментів;
  5. пауза/продовження має працювати з фрагментами;
  6. після `Alt+Q` вікно має активуватися / перемикатися на Quick TTS (за бажанням);
  7. повзунки швидкості й гучності мають крок `0.1` і нормальний формат чисел;
  8. потрібне збереження всього озвученого тексту у файл `.mp4` (типово) або `.wav`;
     вибір формату — в налаштуваннях.
- X11 більше не підтримується: для буфера на Wayland достатньо `wl-clipboard`.
  `TextCopy` залишається в проєкті **лише для Windows 11**.

---

## 2. Зафіксовані рішення

| # | Рішення | Обґрунтування |
|---|---|---|
| R7 | **Фрагментація виконується на клієнті**, сервер не змінюється | Сервер приймає один `text`; клієнт сам ріже текст і робить послідовні запити |
| R8 | **Пакетний режим:** текст ділиться на фрагменти, фрагменти — на пакети по N (1–10, типово 5). Після пакета відтворення зупиняється; далі — вручну | Вибір користувача: «пакетами по N», без автоматичного продовження |
| R9 | **Автоозвучення після `Alt+Q` — увімкнено типово**; активація вікна — **вимкнена типово** | Швидкий сценарій «виділив → почув», але без несподіваного перехоплення фокуса |
| R10 | **Типовий режим фрагментації — «до 250 символів»** (розумне пакування речень). Режими: абзаци / речення / до 250 | Компроміс між якістю TTS і швидкістю; усе налаштовується |
| R11 | **Застарілий X11-fallback видаляється з коду й документації.** Linux — тільки `wl-paste`; Windows — `TextCopy` | X11 більше не цільова платформа; зайва залежність і плутанина |
| R12 | **`Doc/ua_tts_client_arch.md` видаляється**, замість нього — `Doc/Project_Structure.md` | Потрібна карта коду для агента, а не історичний документ архітектури |
| R13 | **Тестовий проєкт не створюється** | Рішення користувача; перевірка — ручні сценарії + тимчасові harness-и поза репозиторієм |
| R14 | **Reader і Character Voice — поза ітерацією** | Не входять у поточні потреби |
| R15 | **Один чекбокс «Активувати вікно після Alt+Q»** керує і активацією вікна, і переходом на вкладку Quick TTS | Активація без переходу на потрібну вкладку не має сенсу |
| R16 | **Експорт усього тексту** з поля Quick TTS: кнопка «Зберегти аудіо…», формати **MP4 (типово)** і **WAV**; MP4 робить серверний `/api/convert` | У сервері вже є pydub + ffmpeg; клієнту не потрібна нова нативна залежність |
| R17 | **MP4 — це аудіо без відео** (AAC у контейнері MP4), а не відеоролик | Вимога — зберегти звук; відеоряд не потрібен |
| R18 | Під час експорту поточне відтворення **зупиняється**, експорт іде окремим послідовним синтезом | Уникнути конкуренції за GPU й плутанини з чергою |

---

## 3. Цільове дерево (нові та змінені файли)

```text
Core/
├── Enums/
│   ├── TextChunkingMode.cs              ← новий: Paragraphs | Sentences | MaxLength
│   └── AudioExportFormat.cs             ← новий: Mp4 | Wav
├── Models/
│   ├── AppConfig.cs                     ← нові поля (див. §6.1)
│   ├── TextChunkingOptions.cs           ← новий: режим + MaxChunkLength = 250
│   ├── TextFragment.cs                  ← новий: record (Index 1-based, Text)
│   ├── SpeechQueueOptions.cs            ← новий: Voice, Speed, BatchSize
│   ├── AudioExportRequest.cs            ← новий: текст, голос, швидкість, чанкінг, формат, шлях
│   └── AudioExportProgress.cs           ← новий: етап + виконано/всього фрагментів
├── Interfaces/
│   ├── ITextChunker.cs                  ← новий
│   ├── IFragmentPlaybackService.cs      ← новий
│   ├── IAudioExportService.cs           ← новий
│   ├── IFileSaveDialogService.cs        ← новий
│   └── ITtsApiClient.cs                 ← + ConvertAsync (MP4/WAV через сервер)

Infrastructure/
├── Text/
│   └── SentenceAwareTextChunker.cs      ← новий: чиста реалізація ITextChunker
├── Playback/
│   └── FragmentPlaybackService.cs       ← новий: синтез + черга + пакети
├── Audio/
│   ├── BassAudioPlayerService.cs        ← + подія TrackEnded (природний кінець)
│   ├── WavAudioConcatenator.cs          ← новий: склеювання WAV у пам'яті
│   └── AudioExportService.cs            ← новий: синтез усіх фрагментів → WAV/MP4
├── Automation/
│   └── WaylandClipboardAutomationService.cs ← прибрано X11-fallback; TextCopy лише Windows
├── Api/
│   └── TtsApiClient.cs                  ← + ConvertAsync (multipart /api/convert)
└── Storage/
    └── JsonSettingsService.cs           ← JsonStringEnumConverter

ViewModels/
├── MainWindowViewModel.cs               ← SelectedTabIndex, ActivateRequested, новий hotkey-флоу
├── Components/
│   └── PlaybackControlViewModel.cs      ← черга, лічильники, пауза/продовження пакетів
├── Tabs/
│   ├── QuickTtsViewModel.cs             ← фрагментація, автосинтез, експорт, статуси
│   └── SettingsViewModel.cs             ← нові налаштування, формат експорту, крок 0.1
└── Design/
    └── DesignData.cs                    ← фейкові ITextChunker / IFragmentPlaybackService /
                                             IAudioExportService / IFileSaveDialogService

Views/
├── MainWindow.axaml(.cs)                ← SelectedIndex, лічильник фрагментів
├── Services/
│   └── AvaloniaFileSaveDialogService.cs ← новий: SaveFilePickerAsync (UI-шар)
└── Tabs/
    ├── QuickTtsView.axaml               ← лічильник, «Наступний пакет», «Зберегти аудіо…»
    └── SettingsView.axaml               ← чекбокси, ComboBox, NumericUpDown, слайдери

App.axaml.cs                             ← реєстрація нових сервісів, ActivateRequested
Doc/
├── Project_Structure.md                 ← новий (замість ua_tts_client_arch.md)
├── Curent_Work_Plan.md                  ← цей план
└── ua_tts_client_arch.md                ← видалено (замінено на Project_Structure.md)
```

---

## 4. Кроки

### Крок 10 — Документаційна гігієна ✅

**Мета:** прибрати історичний документ і дати агенту актуальну карту коду.

| # | Дія | Стан |
|---|---|---|
| 10.1 | Створити `Doc/Project_Structure.md`: шари, фактичне дерево, ключові типи, DI, потоки, точки розширення, платформні відмінності | ✅ |
| 10.2 | Видалити `Doc/ua_tts_client_arch.md` | ✅ |
| 10.3 | Переписати цей план під ітерацію 2 | ✅ |
| 10.4 | Оновити посилання в `AGENTS.md`, `README.md`, `Doc/API.md` | ✅ |

`Project_Structure.md` не дублює `AGENTS.md`: там факти й пастки, тут — **де що лежить і куди додавати нове**.

---

### Крок 11 — Прибрати застарілий X11-fallback, залишити `TextCopy` для Windows ✅

**Мета:** Linux-шлях читання буфера — тільки `wl-paste`; на Windows — `TextCopy`.

**Зміни в `WaylandClipboardAutomationService.cs` (виконано):**

1. `ReadClipboardAsync`:
   - Linux: якщо `WaylandSession` — `wl-paste --no-newline`; якщо `wl-paste` недоступний → порожній рядок + `LogWarning`.
   - Linux без Wayland: порожній рядок + `LogWarning` (X11 не підтримується).
   - Windows (і інші ОС): `TextCopy.ClipboardService.GetTextAsync`.
2. Видалено X11-гілку та згадки про неї з XML-документації.
3. `TextCopy` **не викликається на Linux**.
4. Симуляція `Ctrl+C` і 900-мс опитування буфера не змінилися.

**Перевірка:**
- `dotnet build` — 0 warnings / 0 errors (перевірено 2026-10-22).
- У коді й користувацькій документації не залишилося згадок застарілого пакета.
- Запуск із `PATH`, де немає застарілого пакета, але є `wl-paste` → `Alt+Q` захоплює текст (перевірити в сесії з живим вікном).
- Windows-шлях компілюється; runtime на Windows не перевіряється (немає середовища) — зафіксувати в звіті.

**Документація:** `README.md` — вимога `sudo pacman -S wl-clipboard`; `AGENTS.md` — «TextCopy лише Windows».

---

### Крок 12 — Конфіг і нові налаштування ✅

**Мета:** додати поля, які керують новою поведінкою, зі зворотною сумісністю.

**Виконано:** `AppConfig` розширено (`AutoSynthesizeOnHotkey`, `ActivateOnHotkey`, `ChunkingMode`,
`BatchSize`, `ExportFormat`); `JsonSettingsService` зберігає enum-и рядками; `SettingsViewModel`
має нові властивості, клампить `BatchSize` (1–10) і округлює швидкість/гучність до `0.1`.

#### 12.1 Нові поля `AppConfig`

```csharp
public bool AutoSynthesizeOnHotkey { get; init; } = true;
public bool ActivateOnHotkey { get; init; } = false;
public TextChunkingMode ChunkingMode { get; init; } = TextChunkingMode.MaxLength;
public int BatchSize { get; init; } = 5;              // 1..10
public AudioExportFormat ExportFormat { get; init; } = AudioExportFormat.Mp4;
```

- Старі `config.json` без цих полів читаються з типовими значеннями.
- `JsonSettingsService`: додати `JsonStringEnumConverter` (режим фрагментації й формат експорту зберігаються рядками, а не числами).
- `SettingsViewModel`: нові `[ObservableProperty]` (`AutoSynthesizeOnHotkey`, `ActivateOnHotkey`,
  `SelectedChunkingMode`, `BatchSize`, `SelectedExportFormat`) + валідація `BatchSize` (clamp 1–10)
  і `Math.Round(Speed/Volume, 1)`.
- `SettingsViewModel.SaveAsync` зберігає нові поля.
- `SettingsViewModel.ApplyConfig` округлює швидкість/гучність до `0.1`.
- Нові налаштування діють **одразу** після зміни (не тільки після «Зберегти»); «Зберегти» лише пише на диск.

**Перевірка:** старий конфіг завантажується; новий зберігається; після перезапуску значення зберігаються.

---

### Крок 13 — Фрагментація тексту ✅

**Мета:** розбити текст на логічні фрагменти без обривання речень посередині.

**Виконано:** `ITextChunker` + `SentenceAwareTextChunker`; три режими; обробка скорочень
та ініціалів; задовгі речення ріжуться по останньому пробілу/комі; порожній текст → 0 фрагментів.
Перевірено тимчасовим harness-ом поза репозиторієм (14/14 PASS).

**Нові типи:**

```csharp
public enum TextChunkingMode { Paragraphs, Sentences, MaxLength }

public sealed record TextChunkingOptions
{
    public const int DefaultMaxChunkLength = 250;
    public TextChunkingMode Mode { get; init; } = TextChunkingMode.MaxLength;
    public int MaxChunkLength { get; init; } = DefaultMaxChunkLength;
}

public sealed record TextFragment(int Index, string Text); // Index з 1

public interface ITextChunker
{
    IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options);
}
```

**Алгоритм `SentenceAwareTextChunker`:**

1. Нормалізація: `\r\n` → `\n`, зайві пробіли/таби всередині абзацу згортаються, порожні рядки зберігаються як межі абзаців.
2. Розбиття на речення: після `.`, `!`, `?`, `…`, якщо далі пробіл/кінець і наступний символ — велика літера, лапка або цифра.
3. Мінімальний список скорочень, після яких **не** ріжемо: `т.д.`, `т.п.`, `і т.ін.`, `р.`, `м.`, `вул.`, `ім.`, `проф.`, `акад.`, ініціали (`І. І.`). Якщо сумнівно — краще не різати.
4. Режими:
   - `Paragraphs` — абзаци; абзац довший за `MaxChunkLength` додатково ріжеться по реченнях.
   - `Sentences` — одне речення = один фрагмент; занадто довге речення ріжеться по останньому пробілу/комі до ліміту.
   - `MaxLength` (типово) — послідовні речення пакуються до `250` символів; речення не ріжеться, якщо вміщується; якщо одне речення > 250 — різка по останньому пробілу/комі, в крайньому разі — жорстко.
5. Порожні/пробільні фрагменти відкидаються; дуже короткі (< 20 символів) за можливості приєднуються до сусіднього.
6. `Index` присвоюється після фільтрації, з 1.

**Перевірка:** тимчасовий harness поза репозиторієм (`/tmp/opencode/...`) на 5–6 текстах:
- кілька абзаців;
- довге речення > 250;
- текст без розділових знаків;
- скорочення `т.д.` / ініціали;
- список із переносами рядків;
- порожній/пробільний текст → 0 фрагментів.

Очікування: жоден фрагмент не перевищує 250 символів, крім випадку, коли розбити нема де; речення не обірване посередині без потреби.

**DI:** `services.AddSingleton<ITextChunker, SentenceAwareTextChunker>();`

---

### Крок 14 — Пакетна черга відтворення ✅

**Мета:** синтезувати й відтворювати фрагменти послідовно, пакетами по N, із паузою між пакетами.

**Виконано:** `IAudioPlayerService.TrackEnded`; `IFragmentPlaybackService` + `FragmentPlaybackService`
(передзавантаження на один фрагмент, пакети, пауза/продовження, стоп, лічильники);
`PlaybackControlViewModel` інтегровано; `QuickTtsViewModel` тепер ріже текст і запускає чергу.
Перевірено на живому сервері: 5 фрагментів / 3 пакети, пауза/продовження, стоп — ALL PASSED.

#### 14.1 `IAudioPlayerService`: подія природного завершення

```csharp
/// <summary>Виникає після природного завершення потоку (не після Stop()).</summary>
event EventHandler? TrackEnded;
```

- `BassAudioPlayerService.OnStreamEnded` після перевірки `channel == _streamHandle` виставляє `Stopped` і повідомляє `TrackEnded`.
- Подія не спрацьовує на `Stop()` / заміну потоку.
- Обробники не блокують потік BASS: черга завершує `TaskCompletionSource` з `RunContinuationsAsynchronously`.

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

`FragmentStartedEventArgs`: фрагмент, номер пакета, всього пакетів.
`BatchCompletedEventArgs`: завершений пакет, всього пакетів, чи є наступний.

#### 14.3 `FragmentPlaybackService` (Infrastructure/Playback)

- Залежності: `ITtsApiClient`, `IAudioPlayerService`, `ILogger`.
- `StartAsync`:
  - зупиняє попередню чергу й аудіо;
  - створює внутрішній `CancellationTokenSource`, зв'язаний із зовнішнім;
  - запускає фоновий pipeline і **повертається, щойно перший фрагмент почав грати** (щоб UI не був «busy» весь час);
  - якщо перший фрагмент не вдався — кидає `TtsApiException` назовні.
- Pipeline одного пакета:
  1. синтезує фрагмент `i`;
  2. поки він грає, заздалегідь синтезує `i+1` (один крок попереду — «імітація безшовності»);
  3. `PlayAsync(wav)` → чекає `TrackEnded` → наступний;
  4. після `BatchSize` фрагментів або кінця тексту — зупиняється.
- Між пакетами:
  - `IsBatchPending = true`, `BatchCompleted`;
  - аудіо вже `Stopped`, черга не рухається;
  - `ContinueAsync` запускає наступний пакет з того самого місця.
- `Pause`/`Resume`:
  - `Pause` → `_audio.Pause()` і прапорець паузи; pipeline не починає новий фрагмент;
  - `Resume` → `_audio.Resume()` і зняття прапорця;
  - якщо пауза настала між фрагментами — новий фрагмент не стартує до `Resume`.
- `Stop`:
  - скасовує CTS, зупиняє аудіо, скидає `IsBatchPending` і лічильники;
  - `StartAsync`/`ContinueAsync` коректно завершуються без винятку.
- Помилки після старту (мережа, 500) → `Failed`, черга зупиняється; UI показує текст.
- Синтез запити **послідовні** (не паралельні), щоб не перевантажувати GPU.

#### 14.4 `PlaybackControlViewModel`

- Додати залежність `IFragmentPlaybackService`.
- Проксіювати лічильники:
  - `FragmentPositionText` — `"Фрагмент 2 з 5 · пакет 1 з 3"`;
  - `IsBatchPending`, `ContinueButtonText` — `"Наступний пакет (2 з 3)"`.
- `TogglePlayPause`:
  - якщо `IsBatchPending` → `ContinueAsync()`;
  - якщо грає/пауза → відповідно `Pause()`/`Resume()`.
- `Stop` → `_fragmentPlayback.Stop()` (якщо є черга), інакше `_audioPlayer.Stop()`.
- `PlayAsync(byte[])` залишити для майбутнього Reader (одиночний WAV).
- Підписки на події черги оновлюють властивості через `OnUiThread`.

**DI:** `services.AddSingleton<IFragmentPlaybackService, FragmentPlaybackService>();`

---

### Крок 15 — `Alt+Q`: автоозвучення та активація вікна

**Мета:** після гарячої клавіші не треба вручну тиснути «Озвучити».

**Порядок дій (критично):**

1. Захопити виділений текст (симуляція `Ctrl+C` йде в **активне чуже** вікно).
2. Якщо захоплення невдале — нічого не активувати й не озвучувати.
3. Якщо `Settings.ActivateOnHotkey`:
   - `SelectedTabIndex = 0` (Quick TTS);
   - `ActivateRequested?.Invoke(...)`.
4. Якщо `Settings.AutoSynthesizeOnHotkey` — запустити синтез першого пакета.

**Не можна активувати вікно до кроку 1** — інакше `Ctrl+C` піде в сам застосунок і нічого не скопіює.

**Зміни:**

- `QuickTtsViewModel`:
  - виділити `CaptureSelectedTextCoreAsync` (спільний для кнопки й хука);
  - `CaptureSelectedTextCommand` — лише захоплення (без автосинтезу);
  - публічний `Task<bool> CaptureForHotkeyAsync(CancellationToken)`;
  - публічний `Task SynthesizeCurrentTextAsync(CancellationToken)` (команда делегує йому).
- `MainWindowViewModel`:
  - `[ObservableProperty] int SelectedTabIndex`;
  - `event EventHandler? ActivateRequested`;
  - `OnHotkeyPressed` → на UI-потоці `HandleHotkeyAsync()`:
    capture → activate (за налаштуванням) → synthesize (за налаштуванням);
  - захист від повторного `Alt+Q`: якщо вже йде захоплення — ігнорувати.
- `MainWindow.axaml`: `TabControl SelectedIndex="{Binding SelectedTabIndex}"`.
- `App.axaml.cs`: підписка на `ActivateRequested`:
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
- Кнопка «Захопити виділене» **не** активує вікно й **не** запускає автосинтез.

**Відоме обмеження:** на Wayland композитор може заблокувати викрадення фокуса. `Activate()` — best effort; у README зазначити, що поведінка залежить від композитора.

---

### Крок 16 — Експорт аудіо (MP4 / WAV)

**Мета:** зберегти весь текст із поля Quick TTS в один файл `.mp4` (типово) або `.wav`.

**Семантика:**
- експортується **весь поточний текст** із поля Quick TTS, а не лише прослухані пакети;
- експорт не залежить від черги й може виконуватися без попереднього прослуховування;
- перед стартом експорту поточне відтворення/черга **зупиняється** (R18);
- експорт — окремий послідовний синтез усіх фрагментів, без паралельних запитів до GPU.

#### 16.1 `ITtsApiClient.ConvertAsync`

```csharp
/// <summary>Конвертує WAV у вказаний формат через POST /api/convert.</summary>
Task<byte[]> ConvertAsync(
    byte[] wavData,
    string format,
    CancellationToken cancellationToken = default);
```

- `TtsApiClient`: multipart `file` (WAV) + `format` (`"mp4"` / `"wav"`), читає відповідь як `byte[]`.
- Помилки — через існуючий `SendAsync`/`ReadErrorMessageAsync` (JSON або plain text).
- `PreviewTtsApiClient` у `DesignData` — повертає `Array.Empty<byte>()`.

**Перевірено на серверному venv:** `pydub.AudioSegment.export(format="mp4")` створює
audio-only MP4 (`ftyp isom`, AAC). `format="m4a"` на цій машині **не працює** — не використовувати.
Серверний `/api/convert` уже приймає `mp4`; клієнт лише передає формат. Для MP4 на сервері
потрібен `ffmpeg` (у `Server_py/requirements.txt` уже є `static-ffmpeg`).

#### 16.2 `WavAudioConcatenator` (Infrastructure/Audio)

- Вхід: `IReadOnlyList<byte[]>` WAV-фрагментів (24 кГц, моно, 16-bit PCM).
- Вихід: один WAV `byte[]`.
- Розбір RIFF: перевірити `RIFF`/`WAVE`, знайти `fmt ` і `data`, перевірити однакові `audioFormat=1`, `channels=1`, `sampleRate=24000`, `bitsPerSample=16`; інакше — `InvalidDataException` з поясненням.
- Склеювання: усі `data`-чанки підряд + новий коректний заголовок (`RIFF size = 36 + totalData`, `data size = totalData`).
- Зайві чанки джерела ігноруються; порядок фрагментів зберігається.
- Додаткового штучного gaps не вставляємо: експорт має звучати так само, як послідовне відтворення.
- Чистий синхронний клас, без залежностей від BASS/HTTP.

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

**Алгоритм `AudioExportService`:**

1. `ITextChunker.Split(request.Text, request.Chunking)`; порожній результат → `ArgumentException("Немає тексту для збереження.")`.
2. Для кожного фрагмента послідовно: `ITtsApiClient.SynthesizeAsync(TtsRequestDto { Text, Voice, Speed, Volume = 1.0, Translate = false })`; збирати WAV у `List<byte[]>`; повідомляти `Synthesizing (i, N)`.
3. `WavAudioConcatenator.Concat(list)`; повідомити `Concatenating`.
4. `Wav` → писати WAV-байти; `Mp4` → `ConvertAsync(wav, "mp4")`, повідомити `Converting`, писати результат.
5. Запис атомарний: тимчасовий файл у тому ж каталозі → `File.Move(temp, destination, overwrite: true)`; при скасуванні/помилці тимчасовий файл видаляється.
6. `IOException`/`UnauthorizedAccessException` не ковтати — ViewModel показує зрозуміле повідомлення.
7. Пам'ять: усі WAV-фрагменти тримаються в пам'яті (24 кГц/16-bit ≈ 48 КБ/с). Для типових виділень це прийнятно; для дуже довгих книг — майбутня оптимізація (стрім у тимчасовий файл), зараз не робимо.

#### 16.4 Файловий діалог

```csharp
public interface IFileSaveDialogService
{
    Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default);
}
```

- Реалізація `AvaloniaFileSaveDialogService` (`Views/Services` — єдиний UI-специфічний сервіс,
  щоб `Infrastructure` не залежала від Avalonia):
  - бере `IStorageProvider` з `Application.Current.ApplicationLifetime` → `MainWindow`;
  - `SaveFilePickerAsync` з `FilePickerSaveOptions`:
    - `SuggestedFileName = "UA_TTS_yyyy-MM-dd_HH-mm"`;
    - `DefaultExtension` і `FileTypeChoices` за вибраним форматом (`.mp4` або `.wav`);
    - `ShowOverwritePrompt = true`.
  - повертає локальний шлях або `null`, якщо користувач скасував.
- Реєстрація в `App.axaml.cs`; у `DesignData` — фейк, що повертає `null`.
- Діалог викликається лише з UI-потоку.

**DI:**
```csharp
services.AddSingleton<IAudioExportService, AudioExportService>();
services.AddSingleton<IFileSaveDialogService, AvaloniaFileSaveDialogService>();
```

#### 16.5 QuickTts: кнопка й прогрес

- Кнопка **«Зберегти аудіо…»** поруч з «Озвучити».
- `ExportCommand` (`IncludeCancelCommand = true`), `IsExporting`, `ExportProgressText`, `ExportProgressPercent`.
- `CanExport => !IsBusy && !IsExporting && !string.IsNullOrWhiteSpace(Text)`.
- Під час експорту `CanCapture`/`CanSynthesize` = `false`, щоб текст не змінився посеред операції.
- Флоу:
  1. формат береться з `Settings.ExportFormat`;
  2. `IFileSaveDialogService.PickSavePathAsync(...)`; скасування → вихід без змін;
  3. `_playback.Stop()` (якщо грає);
  4. `AudioExportRequest` з поточним текстом, голосом, швидкістю та `ChunkingMode`;
  5. `_audioExport.ExportAsync(...)`, прогрес у статусі:
     - `"Експорт: синтез 12 із 30…"`;
     - `"Експорт: склеювання…"`;
     - `"Експорт: конвертація в MP4…"`;
     - `"Збережено: /path/file.mp4"`;
  6. скасування → `"Експорт скасовано."`, файл не створюється.
- Помилка конвертації MP4 → показати текст сервера; підказка в README: спробуйте WAV.

---

### Крок 17 — UI: налаштування, лічильники, слайдери

#### 17.1 SettingsView

- Чекбокс **«Озвучувати автоматично після Alt+Q»** → `AutoSynthesizeOnHotkey` (типово ✅).
- Чекбокс **«Активувати вікно й переходити на Quick TTS після Alt+Q»** → `ActivateOnHotkey` (типово ☐).
- ComboBox **«Режим фрагментації»**:
  - «Абзаци»;
  - «Речення»;
  - «До 250 символів (розумне пакування)» — типово.
  - Реалізація: список `ChunkingModeOption(Mode, Title)` + `SelectedChunkingMode`, без магічних індексів.
- `NumericUpDown` **«Фрагментів у пакеті»**: min 1, max 10, increment 1, типово 5.
- ComboBox **«Формат збереження аудіо»**:
  - «MP4 (аудіо, типово)»;
  - «WAV (без стиснення)».
  - Реалізація: `ExportFormatOption(Format, Title)` + `SelectedExportFormat`.
- Слайдери:
  - `TickFrequency="0.1"`, `IsSnapToTickEnabled="True"`;
  - відображення `StringFormat='{}{0:0.0}'` (без «тисячних»);
  - `Speed` 0.5–2.0; `Volume` 0.0–1.0;
  - у міні-плеєрі гучність теж із кроком 0.1 і тим самим форматом.
- Підказка, що нові налаштування діють одразу, а URL сервера — після перезапуску.

#### 17.2 QuickTtsView

- Під кнопками або в статусі: `"Фрагмент 2 з 5 · пакет 1 з 3"` (`Playback.FragmentPositionText`).
- Показ поточного фрагмента (перші ~60 символів) — за бажанням, щоб було видно, що саме читається.
- Кнопка **«Наступний пакет (2 з 3)»** — видима, коли `Playback.IsBatchPending`.
- Кнопка **«Зберегти аудіо…»** — поруч з «Озвучити»; під час експорту показується прогрес і «Скасувати експорт».
- Статуси:
  - `"Підготовлено 12 фрагментів · пакет 1 з 3"`;
  - `"Пакет 1 з 3 завершено. Натисніть ▶ або «Наступний пакет»"`;
  - `"Готово: 3 пакети, 12 фрагментів"`;
  - `"Експорт: синтез 12 із 30…"` / `"Збережено: …/file.mp4"`.
- Помилка черги або експорту → `ErrorMessage` у Quick TTS.

#### 17.3 MainWindow (міні-плеєр)

- Додати `Playback.FragmentPositionText` поруч зі станом/голосом.
- Кнопка ▶/⏸:
  - під час гри — пауза;
  - під час паузи — продовження;
  - коли пакет завершено — запуск наступного пакета.
- Кнопка ■ — повна зупинка черги й скидання лічильників.

---

### Крок 18 — Перевірка та фінальна документація

**Збірка:** `dotnet build` — 0 warnings / 0 errors.

**Ручні сценарії (обов'язково):**

| # | Сценарій | Очікування |
|---|---|---|
| 1 | `Alt+Q` у браузері, автоозвучення ✅ | текст захоплено, перший пакет грає без натискання «Озвучити» |
| 2 | `Alt+Q` з активацією ✅ | спершу захоплено текст, потім вікно активне, відкрита вкладка Quick TTS, грає звук |
| 3 | Активація ☐ | вікно не краде фокус, але текст захоплено й озвучено |
| 4 | Порожнє виділення | статус «Нічого не захоплено», вікно не активується |
| 5 | Режим «Абзаци» | фрагменти відповідають абзацам; довгі абзаци додатково порізані |
| 6 | Режим «Речення» | один фрагмент = одне речення, без обривів |
| 7 | Режим «До 250» | жоден фрагмент > 250 символів (крім нероздільних випадків) |
| 8 | Пакет 5, текст на 12 фрагментів | пакет 1 (1–5) грає, далі пауза; ▶/«Наступний пакет» → 6–10; потім 11–12 |
| 9 | Пакет 1 і пакет 10 | межі пакетів працюють, лічильники не збиваються |
| 10 | Пауза посередині фрагмента | звук зупиняється, ▶ продовжує з того ж місця |
| 11 | Пауза між фрагментами | наступний фрагмент не стартує до ▶ |
| 12 | Стоп під час пакета | звук і черга зупиняються, лічильники скидаються |
| 13 | Новий `Alt+Q` під час відтворення | попередня черга зупиняється, стартує нова |
| 14 | Слайдери | крок 0.1, значення `1.0`, `0.7`, без `0.30000000000000004` |
| 15 | `PATH` без X11-утиліт | захоплення через `wl-paste` працює, застарілий fallback не викликається |
| 16 | Старий `config.json` | завантажується, нові поля = типові значення |
| 17 | Перезапуск | нові налаштування збережені й застосовані |
| 18 | Експорт MP4 (типово) | після вибору шляху створюється `.mp4`, який відтворюється плеєром; звук = весь текст |
| 19 | Експорт WAV | створюється коректний WAV (24 кГц, моно, 16-bit), тривалість ≈ сума фрагментів |
| 20 | Скасування експорту | тимчасовий/цільовий файл не залишається, статус «Експорт скасовано» |
| 21 | Експорт під час відтворення | відтворення зупиняється, експорт починається, після завершення файл на місці |
| 22 | Експорт тексту з 1 фрагмента | MP4/WAV створюються коректно (без порожніх заголовків) |
| 23 | MP4 не конвертується (немає ffmpeg) | показується помилка сервера; WAV усе одно зберігається |

**Документація після перевірки:**
- `README.md`: нові можливості, налаштування, пакети, експорт MP4/WAV (MP4 — аудіо без відео), обмеження активації на Wayland, вимоги: лише `wl-clipboard`.
- `AGENTS.md`: нові факти (TrackEnded, пакетна черга, порядок «capture → activate → synth», `pydub mp4` працює, `m4a` — ні, WAV-склеювання), оновлена таблиця `Doc/`, стан git.
- `Doc/Project_Structure.md`: нові файли й потоки, зокрема експорт.
- `Doc/API.md`: примітка, що клієнт робить **послідовні** запити `/api/synthesize` — один на фрагмент; для MP4 — один `POST /api/convert`.
- Цей план: статуси кроків + журнал.

---

## 5. UX-специфікація (коротко)

### Налаштування

```text
[✓] Озвучувати автоматично після Alt+Q
[ ] Активувати вікно й переходити на Quick TTS після Alt+Q

Режим фрагментації: [До 250 символів (розумне пакування) ▼]
Фрагментів у пакеті: [ 5 ▲▼]   (1–10)
Формат збереження аудіо: [MP4 (аудіо, типово) ▼]  /  [WAV (без стиснення)]

Швидкість мовлення  [────●────] 1.0
Гучність відтворення [──────●──] 0.8
```

### Стани Quick TTS

| Стан | Текст |
|---|---|
| Підготовка | `Підготовлено 12 фрагментів · пакет 1 з 3` |
| Грає | `Фрагмент 2 з 5 · пакет 1 з 3` |
| Пауза | `Пауза · фрагмент 2 з 5` |
| Кінець пакета | `Пакет 1 з 3 завершено. Натисніть ▶ або «Наступний пакет»` |
| Кінець тексту | `Готово: 3 пакети, 12 фрагментів` |
| Експорт | `Експорт: синтез 12 із 30…` → `Збережено: …/file.mp4` |
| Помилка | текст `TtsApiException` у червоному рядку |

---

## 6. Контракти (зведення)

### 6.1 `AppConfig`

```csharp
public bool AutoSynthesizeOnHotkey { get; init; } = true;
public bool ActivateOnHotkey { get; init; } = false;
public TextChunkingMode ChunkingMode { get; init; } = TextChunkingMode.MaxLength;
public int BatchSize { get; init; } = 5;              // 1..10
public AudioExportFormat ExportFormat { get; init; } = AudioExportFormat.Mp4;
```

### 6.2 `IAudioPlayerService`

Додається лише:

```csharp
event EventHandler? TrackEnded;
```

### 6.3 `IFragmentPlaybackService`

Див. §14.2. Ключове: `StartAsync` повертається після старту першого фрагмента; пакет завершується подією `BatchCompleted`; `ContinueAsync` — наступний пакет.

### 6.4 `ITextChunker`

Див. §13. Чистий, синхронний, без залежностей від Avalonia/HTTP/BASS.

### 6.5 `ITtsApiClient.ConvertAsync`

Див. §16.1. Використовує наявний серверний `POST /api/convert`; формати `mp4` і `wav`.

### 6.6 `IAudioExportService`

Див. §16.3. Експортує **весь текст**, послідовно синтезує фрагменти, склеює WAV і за потреби конвертує в MP4.

---

## 7. Ризики та мітигація

| Ризик | Ймовірність | Мітигація |
|---|---|---|
| Wayland не дає активувати вікно (focus stealing) | середня | `Activate()` best effort; налаштування типово вимкнене; описати в README |
| Активація до захоплення зламає `Ctrl+C` | висока (якщо переплутати порядок) | Жорсткий порядок: capture → activate → synth; тест №2 |
| Між фрагментами чутна пауза | середня | Prefetch одного фрагмента наперед; BASS-потік створюється швидко |
| Пауза під час синтезу наступного фрагмента | середня | Pause gate: не стартувати новий фрагмент до `Resume` |
| Гонки `Stop`/`TrackEnded`/нового `Start` | середня | Один внутрішній CTS, `TaskCompletionSource` з `RunContinuationsAsynchronously`, блокування стану |
| `TrackEnded` приходить із потоку BASS | висока | Не оновлювати UI напряму; `OnUiThread` у ViewModel |
| Старий конфіг без нових полів | низька | Значення за замовчуванням; `JsonStringEnumConverter` |
| Наївне розбиття на речення ламає скорочення | середня | Мінімальний список скорочень; краще не різати, ніж різати неправильно |
| `wl-paste` відсутній | низька | `LogWarning`, порожній результат; вимога `wl-clipboard` у README |
| `StringFormat` у compiled bindings | низька | Перевірити `dotnet build`; за потреби — computed-властивість `SpeedText` |
| MP4-конвертація залежить від ffmpeg на сервері | середня | Серверний `/api/convert` повертає помилку — показати її; запропонувати WAV |
| Різні формати WAV-фрагментів | низька | `WavAudioConcatenator` перевіряє `fmt ` і кидає зрозумілий `InvalidDataException` |
| Пам'ять під час експорту довгого тексту | низька | Типові виділення ≤ кількох МБ; для книг — майбутній стрім на диск |
| Неправильне розширення у файловому діалозі | низька | `DefaultExtension` + `FileTypeChoices`; ім'я формується з вибраного формату |
| Windows runtime не перевірено | висока | `TextCopy`-шлях збережено; зафіксувати як неперевірене в звіті |

---

## 8. Журнал виконання (ітерація 2)

| Дата | Що зроблено |
|---|---|
| 2026-10-22 | Сформовано план ітерації 2 на основі `Notes_Userstory.md`; зафіксовано рішення R7–R18 |
| 2026-10-22 | **Крок 10 виконано:** створено `Doc/Project_Structure.md`; `Doc/ua_tts_client_arch.md` видалено; оновлено `AGENTS.md`, `README.md`, `Doc/API.md` |
| 2026-10-22 | **Крок 11 виконано:** `WaylandClipboardAutomationService` читає буфер на Linux лише через `wl-paste`; `TextCopy` залишено для Windows; `dotnet build` — 0 warnings / 0 errors |
| 2026-10-22 | **Крок 12 виконано:** нові поля `AppConfig`, `JsonStringEnumConverter`, нові властивості `SettingsViewModel` з валідацією та округленням до 0.1 |
| 2026-10-22 | **Крок 13 виконано:** `TextChunkingMode`, `TextChunkingOptions`, `TextFragment`, `ITextChunker`, `SentenceAwareTextChunker`; harness — 14/14 PASS |
| 2026-10-22 | **Крок 14 виконано:** `TrackEnded` у BASS-сервісі, `IFragmentPlaybackService` + `FragmentPlaybackService`, інтеграція в `PlaybackControlViewModel` і `QuickTtsViewModel`; harness на живому сервері (пакети, пауза/продовження, стоп) — ALL PASSED |
| | |

---

## 9. Як продовжити роботу

1. Прочитати `AGENTS.md`, `Doc/Project_Structure.md`, цей план.
2. Перевірити `git status` і `dotnet build`.
3. Виконувати кроки **по порядку 15 → 18**; після кожного — оновлювати статус і журнал.
4. Крок 15 (активація вікна) і кроки 16–17 (експорт, UI) потребують живої перевірки
   з користувачем; логіку можна перевіряти harness-ами на живому сервері.
5. Не комітити чужі незакомічені зміни без запиту.
6. Перед фінальним звітом — повний чеклист §4/Крок 18.

---

## 10. Поза межами ітерації

- Тестовий проєкт `UA_TTS_Client.Tests` (свідомо).
- Reader і Character Voice.
- Редактор гарячої клавіші (залишається `Alt+Q`).
- Зміни `server.py` / API (використовується наявний `/api/convert`).
- Runtime-перевірка Windows 11.
- Автоматичне продовження пакетів без участі користувача.
- Відеоряд у MP4 (експортується лише аудіодоріжка).
