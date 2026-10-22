# AGENTS.md — UA_TTS_Client

Avalonia/.NET 10 клієнт (MVVM, CommunityToolkit.Mvvm) для локального Python TTS-сервера
(`Server_py/`, Flask + StyleTTS2). Цільові ОС: Linux EndeavourOS/Arch (Wayland + PipeWire) та Windows 11.

**Клієнт у робочому стані.** Реалізовано Кроки 0–9: `Core/` (контракти), `Infrastructure/`
(API, BASS-аудіо, хук, буфер, JSON-конфіг), ViewModels, Views (XAML), Composition Root
у `App.axaml.cs` і документація. Ітерацію 2 **завершено**: виконано **Кроки 10–18**
(документація, фрагментація, пакетна черга, автоозвучення після `Alt+Q`, експорт MP4/WAV,
UI-перемикачі та лічильники, фінальна перевірка).

Канонічний план — **`Doc/Curent_Work_Plan.md`**, карта коду — **`Doc/Project_Structure.md`**.

---

## Команди (перевірено)

```bash
# Клієнт — збірка (зараз 0 warnings / 0 errors)
dotnet build

# Клієнт — запуск (відкриває вікно; сервер має бути вже запущений)
dotnet run

# Сервер — з правильним cwd (скрипт сам переходить у Server_py/)
./Server_py/run_server.sh
```

- Є `UA_TTS_Client.sln` (класичний формат — сумісний з усіма версіями Rider; .NET 10 за
  замовчуванням створює `.slnx`, тому формат задано явно). `dotnet build` з кореня працює.
- Немає lint/format/codegen-конфігурації. Немає CI.
- SDK: .NET 10.0.113 · Python 3.14.8 (`Server_py/venv`) · torch 2.14.1+cu130, **CUDA доступна**.
- Запуск сервера ≈45 с (модель `patriotyk/styletts2_ukrainian_multispeaker` ~911 МБ уже в кеші HF).
  Модель вантажиться при кожному старті — не здивуйся, що до першого запиту довго.

### Явні `using` обов'язкові

У `.csproj` **немає** `<ImplicitUsings>` — `System`, `System.IO`, `System.Net.Http`,
`System.Threading.Tasks` не імпортуються автоматично. Додавай `using` у кожен файл.
У `BassAudioPlayerService` є конфлікт імен `PlaybackState` з `ManagedBass.PlaybackState`,
тому там використано alias.

### Сервер запускати ТІЛЬКИ з `Server_py/`

`server.py` використовує відносні шляхи (`glob("voices/*.pt")`, `"dictionary.json"`).
Запуск з кореня репозиторію → 0 голосів і порожній словник, без помилки в логу.

---

## Контракт API сервера (перевірено curl-ом)

Base URL: `http://127.0.0.1:5000` (Flask dev server, CORS `Access-Control-Allow-Origin: *`).

| Метод | Шлях | Тіло | Успіх | Помилка |
|---|---|---|---|---|
| GET | `/api/voices` | — | `["Вероніка Дорош", …]` (31 голос) | — |
| GET | `/api/health` | — | `{"status":"ok","device":"cuda","voices":31,"model":"…"}` | — |
| POST | `/api/verbalize` | `{"text"}` | `{"text"}` | 400 `{"error":"No text"}` |
| POST | `/api/synthesize` | `{"text","voice","speed","volume","translate"}` | `audio/wav`, 24 кГц моно 16-bit PCM | 500 `{"error"}` |
| POST | `/api/convert` | multipart `file`, `format` | `audio/<fmt>` | 400 `{"error":"No file"}` |
| POST | `/api/parse_file` | multipart `file` | `{"text"}` | 400 `{"error":"No file"}` |

- `/api/health` **додано** в `Server_py/server.py` (Крок 2). `/api/ping` і `/health` досі не існують → 404.
- `TtsApiClient.GetHealthAsync` на 404 від `/api/health` робить fallback на `GET /api/voices`
  (сумісність зі старим сервером без патча).
- `/api/convert` і `/api/parse_file` тепер теж повертають JSON `{"error": …}`. Але парсер клієнта
  усе одно терпить і plain text: сервер міг бути не оновлений.
- Не ламати CORS-заголовки: браузерне розширення користується цим самим сервером.
- `/api/convert` приймає `format=mp4`; перевірено на серверному venv: pydub/ffmpeg створює
  audio-only MP4 (AAC, `ftyp isom`). `format=m4a` на цій машині **не працює** — не використовувати.
  Клієнт викликає цей ендпоінт для експорту MP4 (`TtsApiClient.ConvertAsync`, Крок 16);
  для WAV конвертація не потрібна — клієнт склеює фрагменти сам.
- Повний контракт із прикладами `curl`/JSON — **`Doc/API.md`** (створено на Кроці 9.2).

---

## ManagedBass 4.0.2 — реальний API

Класів `ManagedBass.Playback` / `ManagedBass.Channel` **не існує**. Є лише статичний `ManagedBass.Bass`:

```csharp
Bass.Init(int device, int freq, DeviceInitFlags flags, IntPtr win, IntPtr clsid); // 0 = No sound, -1 = default
Bass.Free();
Bass.CreateStream(byte[] memory, long offset, long length, BassFlags flags); // перегрузка byte[] Є
Bass.ChannelPlay(int handle, bool restart);
Bass.ChannelPause(int handle);        // toggle; атрибута PlaybackPaused НЕ існує
Bass.ChannelStop(int handle);
Bass.ChannelSetAttribute(int handle, ChannelAttribute.Volume, float value);
Bass.ChannelSetSync(int handle, SyncFlags.End, 0, SyncProcedure proc, IntPtr user);
Bass.ChannelIsActive(int handle);     // → PlaybackState { Stopped, Playing, Stalled, Paused }
Bass.StreamFree(int handle);
Bass.LastError;                       // → ManagedBass.Errors
Bass.Volume;                          // double, глобальний
```

- Делегат: `SyncProcedure(int syncHandle, int channel, int data, IntPtr user)` — **перший параметр
  це дескриптор синхронізації, а не каналу**. Порівнювати з `_streamHandle` треба `channel`.
  Помилка тут тихо ламає автоперехід у `Stopped`.
- У `BassFlags` **немає** прапорця `Memory` — перегрузка `byte[]` обробляє це сама, але масив
  мусить жити весь час відтворення (pin через `GCHandle` або зберігати в полі сервісу).
- **На Linux `Bass.Init(0, …)` відкриває пристрій «No sound»**, а не пристрій за замовчуванням:
  `Bass.NoSoundDevice == 0`, `Bass.DefaultDevice == -1`. Треба
  `Bass.Init(Bass.DefaultDevice, 44100, …)` — тоді BASS створює потік у PipeWire
  (`pactl list sink-inputs` показує `PipeWire ALSA [app]` на активному sink, зокрема Bluetooth).
  Перевірено на цій машині: з `0` потоку в `pactl` немає і звуку немає; з `-1` звук іде на навушники.
- Версія BASS 2.4.18.3; відтворення з `byte[]` перевірено — позиція рухається,
  `ChannelIsActive` → `Playing`.
- NuGet-пакет **не містить** нативних бібліотек. Потрібен системний `/usr/lib/libbass.so`.
  AUR-пакет називається **`libbass`** (`yay -S libbass`) — **`bass` з репозиторію `extra` — це
  гра «Beneath A Steel Sky»**, не бібліотека.
- `libbass.so` на цій машині встановлено.

**Як перевірити API, а не гадати** (ManagedBass/SharpHook/TextCopy не мають нормальних доків):

```bash
mkdir -p /tmp/apidump && cd /tmp/apidump
# csproj: net10.0 + <PackageReference Include="ManagedBass" Version="4.0.2" />
# Program.cs: Assembly.Load("ManagedBass").GetType("ManagedBass.Bass")
#             .GetMembers(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly)
```

---

## SharpHook 8.0.0 і TextCopy 6.2.1 — перевірені факти

```csharp
var hook = new TaskPoolGlobalHook(parallelismLevel: 1, UioHookProvider.Instance);
hook.KeyPressed += (_, e) => { /* e.Data.KeyCode, e.IsEventSimulated */ };
var task = hook.RunAsync();          // не блокує; завершується після Stop()
hook.Stop(); hook.Dispose();

var simulator = SharpHook.Simulation.EventSimulator.Create("app-name", UioHookProvider.Instance);
simulator.Sequence().AddKeyPress(KeyCode.VcLeftControl) /* … */ .Simulate();
```

- `KeyboardHookEventArgs` живе в namespace **`SharpHook`**, а не `SharpHook.Data`
  (`KeyboardEventData` — у `SharpHook.Data`).
- `GlobalHookBase` має `RunAsync(GlobalHookType = All, bool useBackgroundThread = false)`,
  `Stop()`, `Dispose()`; події `KeyPressed/KeyReleased/KeyTyped/HookEnabled/HookDisabled`.
- `EventSimulator.Create(applicationName, provider)` на Linux ініціалізує uinput-пристрої;
  `SimulateKeyStroke(params KeyCode[])` — це extension у `SharpHook.Simulation`.
- Симульовані події повертаються у хук із `IsEventSimulated == true` — саме так відсікається
  власний `Ctrl+C`.
- Перевірено на цій машині: `HookEnabled` спрацьовує, симульовані `Ctrl+F13`/`Ctrl+C` видно
  в обробнику. **Alt+Q фізично перевірено користувачем** (хук спрацьовує); тригер тепер
  на відпусканні обох клавіш, інакше Ctrl+C іде як Ctrl+Alt+C.
- Гаряча клавіша Alt+Q спрацьовує **після відпускання обох клавіш**. Якщо симулювати `Ctrl+C`
  ще до відпускання фізичного Alt, активне вікно отримує `Ctrl+Alt+C` і нічого не копіює.
- **На Linux буфер читається лише через `wl-paste`** (`wl-clipboard`); X11 не підтримується,
  тому застарілий X11-fallback видалено (Крок 11). На Windows використовується `TextCopy`
  (Win32 Clipboard); на Linux `TextCopy` не викликається.
- Після `Ctrl+C` сервіс опитує буфер до 900 мс і повертає порожній рядок, якщо вміст
  не змінився (щоб не підсунути старий текст).
- Перевірено: `wl-paste --no-newline` бачить 1312-байтовий Wayland-буфер. Повний ланцюжок
  «виділення → Ctrl+C → wl-paste» перевірено на `zenity --entry`: захоплено рівно введений маркер.
- **Термінали копіюють через `Ctrl+Shift+C`, а не `Ctrl+C`**: симуляція `Ctrl+C` у терміналі
  надсилає SIGINT. Захоплення виділеного з термінала не підтримується — там треба скопіювати
  вручну й вставити в поле Quick TTS (`Ctrl+V`).
- `ClipboardService.GetTextAsync(CancellationToken)` і `SetTextAsync` — статичні методи.

---

## Фрагментація, черга і `Alt+Q` (Кроки 13–15) — перевірені факти

- `IAudioPlayerService.TrackEnded` виникає **лише на природному кінці треку**; `Stop()`
  і заміна потоку його не викликають. Черга фрагментів чекає саме на цю подію.
- `FragmentPlaybackService.StartAsync` повертається, щойно **перший фрагмент почав грати**,
  а не після всього пакета. Далі пакет грає у фоні.
- Між пакетами: `BatchCompleted` + `IsBatchPending == true`; наступний пакет запускає
  `ContinueAsync`. `QueueCompleted` — коли відтворено останній фрагмент.
- `SpeechQueueOptions.PauseAfterEachBatch` (типово `true`): коли `false`, сервіс сам
  запускає наступний пакет — `BatchCompleted`/`IsBatchPending` не виникають, кнопка
  «Наступний пакет» не показується; `QueueCompleted` з підсумком усе одно піднімається.
- Синтез-запити **послідовні** (один фрагмент наперед), щоб не перевантажувати GPU.
- Пауза, подана одразу після `FragmentStarted`, застосовується вже після старту
  `BassAudioPlayerService.PlayAsync` — це перевірено harness-ом (інакше пауза «губилася»).
- `SentenceAwareTextChunker`: три режими (`Paragraphs`, `Sentences`, `MaxLength`),
  скорочення/ініціали не рвуть речення, задовге речення ріжеться по пробілу/комі;
  порожній текст → **0 фрагментів**, а не виняток.
- `MainWindowViewModel.HandleHotkeyAsync` виконує **захоплення → активація → синтез**.
  Активація вікна до захоплення зіпсувала б `Ctrl+C` (він пішов би в сам застосунок).
- Повторний `Alt+Q` під час захоплення ігнорується (`_hotkeyCaptureInProgress`);
  `QuickTts.CaptureForHotkeyAsync` додатково не стартує, якщо `IsBusy`.
- Кнопка «Захопити виділене» **не** активує вікно й **не** запускає синтез.
- `ActivateRequested` → `App.axaml.cs` → `Window.Activate()`. На Wayland композитор може
  проігнорувати запит (best effort); захоплення й озвучення працюють і без активації.
- Хук ігнорує **симульовані** події (`IsEventSimulated`), тому `Alt+Q` не перевірити
  автоматизацією — лише фізичним натисканням. Логіку перевірено harness-ом із фейковими
  сервісами (25/25 PASS).

## Експорт аудіо (Крок 16) — перевірені факти

- `/api/convert` приймає multipart `file` (WAV) + поле `format`. Перевірено на живому
  сервері: `format=mp4` дає audio-only MP4 (`ftyp isom`, AAC); `format=m4a` на цій машині
  **не працює** — не використовувати. `ffmpeg` на сервері підключає `static-ffmpeg`.
- `WavAudioConcatenator` — чистий синхронний клас: перевіряє RIFF/fmt/data (PCM 24 кГц,
  моно, 16-bit), склеює `data`-чанки під новим заголовком, зайві чанки ігнорує;
  невідповідний формат → `InvalidDataException`.
- `AudioExportService` синтезує фрагменти **послідовно**, тримає WAV у пам'яті
  (~48 КБ/с) і пише атомарно: тимчасовий файл у тому ж каталозі → `File.Move(overwrite)`.
  Скасування/помилка не створює цільовий файл і прибирає тимчасовий.
- MP4-експорт **залежить від сервера**; WAV — ні (конвертація не потрібна).
- `IFileSaveDialogService` — єдиний UI-сервіс поза `Infrastructure`
  (`Views/Services/AvaloniaFileSaveDialogService`): `Infrastructure` не має залежати від
  Avalonia. Діалог викликається лише з UI-потоку; скасування нічого не змінює.
- Під час експорту `CanCapture`/`CanSynthesize`/`CanExport` вимкнені, а `Alt+Q`-захоплення
  ігнорується (`IsExporting`), тож текст не зміниться посеред операції.
- Перевірено harness-ом (49 перевірок: заголовки, порядок даних, помилки формату, етапи
  прогресу, скасування без файлів, перезапис, флоу ViewModel) і живим сервером
  (WAV 4.64 с / MP4 4.65 с, обидва декодуються pydub).

## Середовище цієї машини

- Wayland (`XDG_SESSION_TYPE=wayland`) + PipeWire; `wl-copy`, `wl-paste`, `ydotool` встановлені.
  Для буфера достатньо `wl-clipboard`; X11-утиліти не потрібні.
- Демон `ydotool` — **enabled + active** (перевіряй `systemctl --user is-active ydotool`).
  Але в цій сесії `ydotool key/type` завершуються з кодом 0, а події до вікон не доходять
  (у журналі — `unable to find device pointer:ydotoold virtual device`). Для автоматизації UI
  не покладайся на `ydotool`; SharpHook `EventSimulator` працює.
- Користувач у групі `input` → `/dev/uinput` доступний для симуляції клавіатури.
- NuGet-кеш прогрітий, мережа є; `dotnet build` проходить без завантаження.
- Конфіг користувача — `~/.config/UA_TTS_Client/config.json`; уже містить усі поля Кроків
  12/17, enum-и зберігаються рядками (`"chunkingMode": "Paragraphs"`, `"exportFormat": "Mp4"`).

---

## Конвенції коду

Авторитетне джерело: **`Doc/Промпт для ллм - Єдина кодова база arch-win11.md`**
(розділ зі стандартами). Коротко:

- `file-scoped namespaces`; `public partial class` для **всіх** ViewModel (інакше згенеруються
  неправильно `[ObservableProperty]`/`[RelayCommand]`).
- `x:DataType="vm:XxxViewModel"` у **кожному** XAML (compiled bindings).
- `#nullable enable`; Guard Clauses; `ILogger<T>`; жодних `NullReferenceException`.
- Асинхронність з `CancellationToken`; DI **лише через конструктори**; реєстрація в `App.axaml.cs`.
- Typed HttpClient: `AddHttpClient<ITtsApiClient, TtsApiClient>()` з таймаутом **≥120 с**.
- Шляхи тільки через `Path.Combine` + `Environment.SpecialFolder.ApplicationData`
  (`~/.config/UA_TTS_Client` / `%APPDATA%\UA_TTS_Client`). Linux чутливий до регістру.
- Аудіо відтворюється **з пам'яті** (`byte[]`/`MemoryStream`), без запису тимчасових `.wav`.
- Wayland: після симуляції `Ctrl+C` обов'язково `await Task.Delay(80..120)` перед читанням буфера.
- XML-документація та коментарі — **виключно українською**; `<inheritdoc />` для реалізацій інтерфейсів.
- DTO — `record`; усе, що можна — primary constructors, collection expressions, `required`.
- Автотести в ітерації 2 **не створюються** (рішення користувача). Перевірка — ручні
  сценарії та тимчасові harness-и поза репозиторієм.

---

## UI і DI (Кроки 6–8) — перевірені факти

- Avalonia 12: `TextBox.Watermark` **застарілий** → `PlaceholderText`; прокрутка в `TextBox`
  задається attached-властивістю `ScrollViewer.VerticalScrollBarVisibility="Auto"`.
- Rider-аналізатор помилково скаржиться на `Design.DataContext="{x:Static …}"`
  («expected type is AttachedProperty<object?>»). Тому design-time DataContext ставиться
  в code-behind: `if (Design.IsDesignMode) DataContext = DesignData.MainWindow;` —
  без `Design.DataContext` у XAML. `DesignData` (`ViewModels/Design/DesignData.cs`) містить
  фіктивні реалізації інтерфейсів лише для прев'ю.
- Умовні класи: `Classes.online="{Binding IsServerOnline}"` + стиль
  `Ellipse.status-dot.online`. Стилі з `Window.Styles` діють на всі дочірні UserControl.
- `IsVisible` для рядків робиться через `{Binding X, Converter={x:Static conv:StringConverters.IsNotNullOrEmpty}}`.
- ViewModel-и створюються в UI-потоці; `ViewModelBase.OnUiThread` потрібен для подій BASS,
  що приходять із фонового потоку.
- `BaseAddress` для `HttpClient` фіксується на старті, тому зміна URL у налаштуваннях
  застосовується **після перезапуску** (це написано в UI).
- `MainWindowViewModel` перевіряє сервер кожні 15 с через `PeriodicTimer`; кнопка
  «Перевірити» викликає той самий `RefreshServerStatusCommand`.
- ComboBox зі списками налаштувань працює через option-записи (`ChunkingModeOption`,
  `ExportFormatOption`) — без магічних індексів; `SelectedItem` біндиться на nullable
  властивість, а логіка читає `ChunkingMode`/`ExportFormat` з fallback на типові
  (типовий режим фрагментації — `Paragraphs`, «Абзаци»).
- `NumericUpDown.Value` — `decimal?` (Avalonia 12.1.4); біндінг до `int`-властивості
  працює (перевірено), `FormatString="0"` показує цілі числа.
- Слайдери швидкості/гучності: `TickFrequency="0.1"` + `IsSnapToTickEnabled="True"`,
  значення показується як `StringFormat='{}{0:0.0}'`.
- Статуси черги для Quick TTS — `Playback.QueueStatusText` («Пакет 1 з 3 завершено…»,
  «Готово: N пакетів, M фрагментів»); позиція — `FragmentPositionText`, прев'ю —
  `CurrentFragmentText` (до 60 символів, один рядок). Помилка черги піднімає
  `Playback.QueueFailed` → `ErrorMessage` Quick TTS (підписка знімається в `Dispose`).
- Українське відмінювання чисел — `ViewModelBase.PluralWord(count, one, few, many)`
  (для символів — `SymbolsWord`).

---

## Підводні камені (вже наступили або майже)

1. **`ViewLocator` виправлено** (Крок 8): він замінює `.ViewModels` → `.Views` і `ViewModel` → `View`,
   а для `MainWindowViewModel` додатково пробує `Views.MainWindow`. Головне вікно створюється
   явно в `App.axaml.cs`, тому локатор відповідає лише за вміст вкладок (`ContentControl`).
2. **`App.axaml.cs` — Composition Root.** Конфіг читається синхронно до побудови контейнера
   (адреса потрібна для `HttpClient.BaseAddress`), далі реєструються сервіси й ViewModel-и.
   `desktop.Exit` звільняє `MainWindowViewModel` і `ServiceProvider`.
3. **Не «виправляй» `partial`-властивість у Toolkit-моделі.** Форма
   `[ObservableProperty] public partial string Foo { get; set; }` — **коректна** для
   CommunityToolkit.Mvvm 8.x: генератор створює partial-властивість на C# 13 `field`
   (`get => field;`) і `PropertyChanged` працює. Шаблонну `Greeting` із «Welcome to
   Avalonia!» із коду вже прибрано — правило лишається.
4. **`/api/health` тепер є** (додано в `server.py`), але `TtsApiClient` усе одно має fallback на
   `GET /api/voices` для сумісності зі старим сервером. Не видаляй цей fallback.
5. **Помилки сервера історично неоднорідні** (JSON vs plain text `No file`) — парсер
   `TtsApiClient.ReadErrorMessageAsync` терпить обидва формати. Не звужуй його до JSON.
6. **BASS `SyncProcedure`**: перший аргумент — дескриптор синхронізації, другий — канал.
   Помилка тут не дає жодної помилки в лозі, просто автозупинка не спрацьовує.
7. **Імена файлів у `Doc/` потребують екранування в shell**: `Промпт для ллм - Єдина кодова база arch-win11.md`
   (пробіли). Також Linux чутливий до регістру — namespace у XAML (`x:Class`, `x:DataType`)
   має точно збігатися з текстом у `.cs`.
8. **`.gitignore`**: `Server_py/venv/` ігнорується кореневим правилом (`.gitignore:49`)
   і вбудованим `venv/.gitignore` (`*`) — обидва закомічені.
9. **`Bass.Init(0, …)` — це пристрій «No sound»**, не пристрій за замовчуванням. Завжди
   використовуй `Bass.DefaultDevice` (-1), інакше синтез проходить (сервер віддає 200),
   але звуку немає взагалі. Перевіряй через `pactl list sink-inputs`.
10. **Не симулюй `Ctrl+C`, поки затиснутий фізичний Alt**: активне вікно отримає `Ctrl+Alt+C`
    і нічого не скопіює. Хук Alt+Q спрацьовує на відпусканні обох клавіш; не повертай
    тригер на key press.

---

## `Doc/` та `README.md` — що чим є

Стан після **Кроку 10** (ітерація 2): історичний `ua_tts_client_arch.md` видалено,
замість нього — актуальна карта коду `Project_Structure.md`.

| Файл | Стан |
|---|---|
| `README.md` (корінь) | **Актуальний**: запуск сервера/клієнта, «Як користуватися», вимоги, обмеження |
| `Doc/Project_Structure.md` | **Актуальний**: шари, фактичне дерево, контракти, DI, потоки, точки розширення |
| `Doc/Curent_Work_Plan.md` | **Канонічний план ітерації 2**: кроки 10–18, статуси, журнал. Кроки 10–18 ✅, ітерацію 2 завершено |
| `Doc/API.md` | **Актуальний**: контракт ендпоінтів із прикладами `curl`/JSON |
| `Doc/Промпт для ллм - Єдина кодова база arch-win11.md` | Цілі та стандарти коду. Варто читати повністю |
| ~~`Doc/ua_tts_client_arch.md`~~ | **Видалено** (Крок 10): замінено на `Project_Structure.md` |
| ~~`Doc/Запит на архітектуру.md`~~ | **Видалено користувачем** як історичне |
| ~~`Doc/implementation_roadmap.md`~~ | **Видалено** (Крок 9.2): посилався на неіснуючі `/api/ping` і `/health` |
| ~~`Doc/ua_tts_client_architecture(remove).md`~~ | **Видалено** (Крок 9.2): конфліктний дубль |
| ~~`Doc/Readme.md`~~ | **Перенесено** в `README.md` у корінь (Крок 9.2) |

---

## Стан git

Останні коміти: `14fdf31 feat(ui): перемикачі налаштувань, лічильники черги та слайдери`
(Крок 17) і `93656ca docs: фінальна документація ітерації 2` (Крок 18).
До них: `076cec5 feat(export)` (Крок 16), `fdb4106 docs` (Крок 15), `41d955d feat(hotkey)`,
`ae319e1 docs`, `5f35b80 feat(playback)`, `957fbbe feat(text)`, `c8fba32 feat(settings)`.

Робоче дерево чисте; ітерацію 2 завершено.

---

## План роботи

**Канонічний план — `Doc/Curent_Work_Plan.md`.** Не дублюй його зміст у цьому файлі: там є
нумерація кроків, поточний стан, контракти й журнал виконання. План ітерації 2 **закритий**
(усі кроки ✅) — для нових завдань створи план ітерації 3 (архівуй цей файл або додай новий).

Ітерацію 2 **завершено**: **Кроки 10–18 виконано**, ручні сценарії підтверджено
користувачем. Крок 9.1 (тестовий проєкт) **скасовано** — автотести на цьому етапі
не потрібні.

Фактичний стан історії та вимоги — у `Doc/` і `README.md`; **пріоритет має виконуваний код
і ці два файли**, не документи.
