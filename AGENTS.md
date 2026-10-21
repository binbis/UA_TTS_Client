# AGENTS.md — UA_TTS_Client

Avalonia/.NET 10 клієнт (MVVM, CommunityToolkit.Mvvm) для локального Python TTS-сервера
(`Server_py/`, Flask + StyleTTS2). Цільові ОС: Linux EndeavourOS/Arch (Wayland + PipeWire) та Windows 11.

**Клієнт у процесі розробки.** Реалізовано Кроки 0–8: `Core/` (контракти), `Infrastructure/`
(API, BASS-аудіо, хук, буфер, JSON-конфіг), ViewModels, Views (XAML) і Composition Root
у `App.axaml.cs`. Лишився Крок 9 — тести й фінальна документація. Застосунок уже запускається
як повноцінне вікно з чотирма вкладками та міні-плеєром.

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
- Тестовий проєкт ще не створено (Крок 9).
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

---

## ManagedBass 4.0.2 — реальний API (у `Doc/` він описаний ХИБНО)

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
  Помилка тут тихо ламає автоперехід у `Stopped` (перевірено на Кроці 4).
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
  гра «Beneath A Steel Sky»**, не бібліотека. `Doc/` тут помиляється.
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
- **TextCopy на Linux працює лише через X11 (`xsel`)** і може віддати застарілий X11-буфер.
  Тому `WaylandClipboardAutomationService` спершу викликає `wl-paste`, а TextCopy/xsel — лише
  fallback. Після `Ctrl+C` сервіс опитує буфер до 900 мс і повертає порожній рядок, якщо вміст
  не змінився (щоб не підсунути старий текст).
- Перевірено: `wl-paste --no-newline` бачить 1312-байтовий Wayland-буфер, `xsel` у той самий
  момент повертає 0 байт. Повний ланцюжок «виділення → Ctrl+C → wl-paste» перевірено на
  `zenity --entry`: захоплено рівно введений маркер.
- **Термінали копіюють через `Ctrl+Shift+C`, а не `Ctrl+C`**: симуляція `Ctrl+C` у терміналі
  надсилає SIGINT. Захоплення виділеного з термінала не підтримується — там треба скопіювати
  вручну й вставити в поле Quick TTS (`Ctrl+V`).
- `ClipboardService.GetTextAsync(CancellationToken)` і `SetTextAsync` — статичні методи.

---

## Середовище цієї машини

- Wayland (`XDG_SESSION_TYPE=wayland`) + PipeWire; `wl-copy`, `wl-paste`, `ydotool`, `xsel` встановлені.
- Демон `ydotool` — **enabled + active** (перевіряй `systemctl --user is-active ydotool`).
  Але в цій сесії `ydotool key/type` завершуються з кодом 0, а події до вікон не доходять
  (у журналі — `unable to find device pointer:ydotoold virtual device`). Для автоматизації UI
  не покладайся на `ydotool`; SharpHook `EventSimulator` працює.
- Користувач у групі `input` → `/dev/uinput` доступний для симуляції клавіатури.
- NuGet-кеш прогрітий, мережа є; `dotnet build` проходить без завантаження.

---

## Конвенції коду

Авторитетне джерело: **`Doc/Єдна кодова база linux win11.md`** (розділ зі стандартами).
Коротко:

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
- Тести (пізніший етап): xUnit + FluentAssertions + NSubstitute, патерн AAA,
  іменування `Метод_Умови_ОчікуванийРезультат`.

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

---

## Підводні камені (вже наступили або майже)

1. **`ViewLocator` виправлено** (Крок 8): він замінює `.ViewModels` → `.Views` і `ViewModel` → `View`,
   а для `MainWindowViewModel` додатково пробує `Views.MainWindow`. Головне вікно створюється
   явно в `App.axaml.cs`, тому локатор відповідає лише за вміст вкладок (`ContentControl`).
2. **`App.axaml.cs` — Composition Root.** Конфіг читається синхронно до побудови контейнера
   (адреса потрібна для `HttpClient.BaseAddress`), далі реєструються сервіси й ViewModel-и.
   `desktop.Exit` звільняє `MainWindowViewModel` і `ServiceProvider`.
3. **Не «виправляй» `partial`-властивість у Toolkit-моделі.** Форма
   `[ObservableProperty] public partial string Greeting { get; set; }` — **коректна** для
   CommunityToolkit.Mvvm 8.x: генератор створює partial-властивість на C# 13 `field`
   (`get => field;`) і `PropertyChanged` працює. Єдине, що тут лишнє — шаблонне значення
   `"Welcome to Avalonia!"`.
4. **`/api/health` тепер є** (додано в `server.py`), але `TtsApiClient` усе одно має fallback на
   `GET /api/voices` для сумісності зі старим сервером. Не видаляй цей fallback.
5. **Помилки сервера історично неоднорідні** (JSON vs plain text `No file`) — парсер
   `TtsApiClient.ReadErrorMessageAsync` терпить обидва формати. Не звужуй його до JSON.
6. **BASS `SyncProcedure`**: перший аргумент — дескриптор синхронізації, другий — канал.
   Помилка тут не дає жодної помилки в лозі, просто автозупинка не спрацьовує.
7. **Імена файлів у `Doc/` потребують екранування в shell**: `Єдина кодова база linux win11.md`
   (пробіли) та `ua_tts_client_architecture(remove).md` (дужки). Також Linux чутливий до
   регістру — namespace у XAML (`x:Class`, `x:DataType`) має точно збігатися з текстом у `.cs`.
8. **`.gitignore`**: `Server_py/venv/` ігнорується лише вбудованим `venv/.gitignore` (`*`),
   а не кореневим правилом — кореневе правило додано вже у поточних (незакомічених) змінах.
9. **`Bass.Init(0, …)` — це пристрій «No sound»**, не пристрій за замовчуванням. Завжди
   використовуй `Bass.DefaultDevice` (-1), інакше синтез проходить (сервер віддає 200),
   але звуку немає взагалі. Перевіряй через `pactl list sink-inputs`.
10. **Не симулюй `Ctrl+C`, поки затиснутий фізичний Alt**: активне вікно отримає `Ctrl+Alt+C`
    і нічого не скопіює. Хук Alt+Q спрацьовує на відпусканні обох клавіш; не повертай
    тригер на key press.

---

## `Doc/` — ненадійне джерело

Файли згенеровані Gemini і **розходяться з реальністю**. Не копіюй з них API без перевірки.

| Файл | Стан |
|---|---|
| `Doc/Єдина кодова база linux win11.md` | **Найнадійніший** — цілі та стандарти коду. Єдине, що варто читати повністю |
| `Doc/Запит на архітектуру.md` | Початкові вимоги: 4 вкладки, `Alt+Q`, Wayland-фокус |
| `Doc/ua_tts_client_arch.md` | ManagedBass API описано **неправильно**: `BASS_StreamCreateFile` + `GCHandle` — це стиль до-4.0, у 4.0.2 такого немає (див. вище) |
| `Doc/ua_tts_client_architecture(remove).md` | Дубль `ua_tts_client_arch.md` з **конфліктною** структурою: верхні `Models/` + `DTOs/` + `Services/` замість `Core/Models/` + `Infrastructure/`. Кандидат на видалення |
| `Doc/implementation_roadmap.md` | Roadmap від Gemini; посилається на неіснуючі `/api/ping` і `/health` |
| `Doc/Readme.md` | Обривається на порожньому розділі «## 📖 Як користуватися»; називає неіснуючі `run_server.bat` та `UA_TTS_Client_Full_Cycle` |

---

## Стан git

Останній коміт: `d027288 docs: оновити AGENTS.md і Final_Plan.md за підсумками Кроків 0–5`.
Після нього в робочому дереві: Кроки 6–8 (ViewModels, Views, DI, `ViewLocator`,
`Microsoft.Extensions.Logging.Console`) та оновлення цього файлу й `Doc/Final_Plan.md`.
Незакомічено — не комітити без запиту.

План роботи — у `Doc/Final_Plan.md` (актуальніший за цей розділ).

---

## План роботи

**Канонічний план — `Doc/Final_Plan.md`.** Не дублюй його зміст у цьому файлі: там є
нумерація кроків, поточний стан, контракти й журнал виконання.

Поточний напрям — **вертикальний зріз «Quick TTS + міні-плеєр»**: буфер → API → BASS → UI.

Фактичний стан історії та вимоги — у `Doc/`; **приоритет має виконуваний код і ці два файли**, не документи.
