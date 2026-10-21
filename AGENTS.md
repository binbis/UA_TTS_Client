# AGENTS.md — UA_TTS_Client

Avalonia/.NET 10 клієнт (MVVM, CommunityToolkit.Mvvm) для локального Python TTS-сервера
(`Server_py/`, Flask + StyleTTS2). Цільові ОС: Linux EndeavourOS/Arch (Wayland + PipeWire) та Windows 11.

**Клієнт ще не реалізовано** — це чистий шаблон Avalonia. `Core/`, `Infrastructure/`, `Models/`
існують лише як порожні `<Folder Include=…>` у `.csproj`, на диску їх немає.

---

## Команди (перевірено)

```bash
# Клієнт — збірка (зараз 0 warnings / 0 errors)
dotnet build

# Клієнт — запуск
dotnet run

# Сервер — ОБОВ'ЯЗКОВО з каталогу Server_py (див. нижче про cwd)
cd Server_py && venv/bin/python server.py
```

- `.sln` **не існує** — лише `UA_TTS_Client.csproj`. Тестовий проєкт теж відсутній.
- Немає lint/format/codegen-конфігурації. Немає CI.
- SDK: .NET 10.0.113 · Python 3.14.8 (`Server_py/venv`) · torch 2.14.1+cu130, **CUDA доступна**.
- Запуск сервера ≈45 с (модель `patriotyk/styletts2_ukrainian_multispeaker` ~911 МБ уже в кеші HF).
  Модель вантажиться при кожному старті — не здивуйся, що до першого запиту довго.

### Сервер запускати ТІЛЬКИ з `Server_py/`

`server.py` використовує відносні шляхи (`glob("voices/*.pt")`, `"dictionary.json"`).
Запуск з кореня репозиторію → 0 голосів і порожній словник, без помилки в логу.

---

## Контракт API сервера (перевірено curl-ом)

Base URL: `http://127.0.0.1:5000` (Flask dev server, CORS `Access-Control-Allow-Origin: *`).

| Метод | Шлях | Тіло | Успіх | Помилка |
|---|---|---|---|---|
| GET | `/api/voices` | — | `["Вероніка Дорош", …]` (31 голос) | — |
| POST | `/api/verbalize` | `{"text"}` | `{"text"}` | 400 `{"error":"No text"}` |
| POST | `/api/synthesize` | `{"text","voice","speed","volume","translate"}` | `audio/wav`, 24 кГц моно 16-bit PCM | 500 `{"error"}` |
| POST | `/api/convert` | multipart `file`, `format` | `audio/<fmt>` | **400 plain-text `No file`** (`text/html`) |
| POST | `/api/parse_file` | multipart `file` | `{"text"}` | **400 plain-text `No file`** (`text/html`) |

- **`/api/health` і `/api/ping` НЕ ІСНУЮТЬ** → 404. Health-check роби через `GET /api/voices`.
- Помилки з `/api/convert` і `/api/parse_file` — **не JSON**, а 7-байтовий текст `No file`.
  Клієнт має бути стійким і до JSON, і до plain text.
- Не ламати CORS-заголовки: браузерне розширення користується цим самим сервером.

---

## ManagedBass 4.0.2 — реальний API (у `Doc/` він описаний ХИБНО)

Класів `ManagedBass.Playback` / `ManagedBass.Channel` **не існує**. Є лише статичний `ManagedBass.Bass`:

```csharp
Bass.Init(int device, int freq, DeviceInitFlags flags, IntPtr win, IntPtr clsid);
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

- Делегат: `SyncProcedure(int handle, int channel, int data, IntPtr user)`.
- У `BassFlags` **немає** прапорця `Memory` — перегрузка `byte[]` обробляє це сама, але масив
  мусить жити весь час відтворення (pin через `GCHandle` або зберігати в полі сервісу).
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

## Середовище цієї машини

- Wayland (`XDG_SESSION_TYPE=wayland`) + PipeWire; `wl-copy`, `wl-paste`, `ydotool`, `xsel` встановлені.
- Демон `ydotool` — **enabled + active** (перевіряй `systemctl --user is-active ydotool`).
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

## Підводні камені (вже наступили або майже)

1. **`ViewLocator` зламаний.** Він будує ім'я шляхом `FullName.Replace("ViewModel", "View")`, тобто
   `UA_TTS_Client.ViewModels.MainViewModel` → `UA_TTS_Client.Views.MainView`, чого **не існує**
   (є `Views.MainWindow`). Результат — `TextBlock "Not Found: ..."` замість вікна.
2. **`App.axaml.cs` без DI.** DataContext створюється вручну: `new MainViewModel()`.
   Точка композиції (Composition Root) — саме цей файл.
3. **Не «виправляй» `partial`-властивість у Toolkit-моделі.** Форма
   `[ObservableProperty] public partial string Greeting { get; set; }` — **коректна** для
   CommunityToolkit.Mvvm 8.x: генератор створює partial-властивість на C# 13 `field`
   (`get => field;`) і `PropertyChanged` працює. Єдине, що тут лишнє — шаблонне значення
   `"Welcome to Avalonia!"`.
4. **Сервер не має `/api/health`** — будь-яка health-check логіка в клієнті має спиратися на
   `GET /api/voices`.
5. **Помилки сервера неоднорідні** (JSON vs plain text `No file`) — парсер має терпіти обидва.
6. **Імена файлів у `Doc/` потребують екранування в shell**: `Єдина кодова база linux win11.md`
   (пробіли) та `ua_tts_client_architecture(remove).md` (дужки). Також Linux чутливий до
   регістру — namespace у XAML (`x:Class`, `x:DataType`) має точно збігатися з текстом у `.cs`.
7. **`.gitignore`**: `Server_py/venv/` ігнорується лише вбудованим `venv/.gitignore` (`*`),
   а не кореневим правилом — кореневе правило додано вже у поточних (незакомічених) змінах.

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

Останній коміт: `757dd25 додано теку до проєкту`. Незакомічені зміни: `.gitignore`
(`Server_py/__pycache__/`, `Server_py/venv/`, `*.wav`, `*.mp3`; прибрано неробочий `./Server_py/venv`),
`App.axaml` (`RequestedThemeVariant="Dark"`), `UA_TTS_Client.csproj` (прибрано зайвий
`System.Text.Json` → NU1510 зник), `Doc/Readme.md` (`yay -S bass` → `yay -S libbass`).
Не комітай ці зміни без запиту — частина з них уже узгоджена з користувачем.

План роботи — у `Doc/Final_Plan.md` (актуальніший за цей розділ).

---

## План роботи

**Канонічний план — `Doc/Final_Plan.md`.** Не дублюй його зміст у цьому файлі: там є
нумерація кроків, поточний стан, контракти й журнал виконання.

Поточний напрям — **вертикальний зріз «Quick TTS + міні-плеєр»**: буфер → API → BASS → UI.

Фактичний стан історії та вимоги — у `Doc/`; **приоритет має виконуваний код і ці два файли**, не документи.
