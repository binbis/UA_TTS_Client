# Фінальний план реалізації UA_TTS_Client

> **Статус:** активний. Останнє оновлення: 2026-10-21.
> **Призначення:** єдине джерело правди щодо того, *що і в якому порядку* реалізувати.
> Цей файл — для продовження роботи в **новій сесії** OpenCode або з іншого агента.
>
> **Перед початком роботи прочитай:**
> 1. цей файл (план і поточний стан),
> 2. `AGENTS.md` у корені репозиторію (перевірені факти, підводні камені, точні API).
>
> `AGENTS.md` відповідає на *«як саме»*, цей файл — на *«що і в якому порядку»*.

---

## 1. Проєкт

**UA_TTS_Client** — кросплатформний (Linux EndeavourOS/Arch, Wayland + PipeWire; Windows 11)
десктопний клієнт на Avalonia UI + .NET 10 для локального Python TTS-сервера
(`Server_py/`, Flask + StyleTTS2, GPU).

Стек (зафіксований, не змінюється):

| Шар | Технологія |
|---|---|
| UI | Avalonia UI 12.1.4 (XAML, compiled bindings), `x:DataType` у кожному файлі |
| MVVM | CommunityToolkit.Mvvm 8.4.2, усі ViewModel — `public partial class` |
| Аудіо | ManagedBass 4.0.2 (відтворення з пам'яті, `byte[]`) + системний `libbass.so` |
| Хуки | SharpHook 8.0.0 (`TaskPoolGlobalHook`) |
| Буфер | TextCopy 6.2.1 (`wl-clipboard` на Wayland) |
| HTTP | `Microsoft.Extensions.Http` — типізований `HttpClient` |
| DI | `Microsoft.Extensions.DependencyInjection` (лише constructor injection) |
| Логи | `Microsoft.Extensions.Logging` → `ILogger<T>` |

Серверний стек: Python 3.14, Flask, `torch 2.14.1+cu130` (CUDA), StyleTTS2
(`patriotyk/styletts2_ukrainian_multispeaker`), 31 голос у `Server_py/voices/*.pt`.

---

## 2. Зафіксовані рішення

| # | Рішення | Обґрунтування |
|---|---|---|
| R1 | **Старт з вертикального зрізу «Quick TTS + міні-плеєр»** (вкладка 1) | Дає найшвидший живий результат і перевіряє весь ланцюг: буфер → API → BASS → UI |
| R2 | **Мінімальні правки в `server.py`** дозволені | Потрібні `/api/health` та єдиний формат помилок; зворотна сумісність з браузерним розширенням (CORS) збережена |
| R3 | **Тести — на фінальному етапі** | Спершу функціонал; тестовий проєкт створюється на Кроці 9.1 (на Кроці 9 розділено: спершу документація, потім тести) |
| R4 | **Документи оновлюються під реалію** під час роботи, не після | `Doc/` зараз розходиться з реальністю (див. `AGENTS.md`) |
| R5 | **Кроки 0–5 — один довгий turn агента**, Кроки 6–9 — окремо з перевіркою UI/звуку користувачем | Кроки 0–5 низького ризику; кроки з UI та звуком потребують очей користувача |
| R6 | **Жодних WPF/WinForms** | Проєкт створювався під Win11 і не працював на Linux — саме через це переписуємо |

---

## 3. Поточний стан

| Крок | Назва | Статус |
|---|---|---|
| 0 | Передумови та гігієна репозиторію | ✅ **виконано** |
| 1 | Core: контракти | ✅ **виконано** |
| 2 | Сервер: мінімальні правки `server.py` | ✅ **виконано** |
| 3 | Infrastructure: Storage + API | ✅ **виконано** |
| 4 | Infrastructure: Audio (ManagedBass) | ✅ **виконано** |
| 5 | Infrastructure: Automation (хук + буфер) | ✅ **виконано** (Alt+Q перевірено фізично; копіювання виправлено — тригер на відпусканні) |
| 6 | ViewModels | ✅ **виконано** |
| 7 | Views (XAML) | ✅ **виконано** |
| 8 | Composition Root (DI) | ✅ **виконано** |
| 9 | Тести + документація | 🔶 **частково**: 9.2 документація ✅ · 9.1 тести ⬜ не розпочато |

Уже виконано поза кроками (див. «Журнал виконання»): аналіз репозиторію, запуск і перевірка
сервера, рефлексія API ManagedBass / SharpHook / TextCopy, створення `AGENTS.md`,
виправлення `.gitignore` і `.csproj`.

---

## 4. Визначення готовності вертикального зрізу

Мінімальна корисна версія (MVP) вважається готовою, коли:

1. застосунок стартує на Wayland і показує вікно з чотирма вкладками;
2. у статус-барі видно стан сервера (онлайн/офлайн) — перевірка через `GET /api/voices`;
3. користувач натискає **«Захопити виділене»** → текст з буфера з'являється у полі введення;
4. **Alt+Q** (глобальний хук) робить те саме з будь-якого вікна;
5. **«Озвучити»** → `POST /api/synthesize` → звук реально лунає;
6. міні-плеєр показує стан (Idle/Playing/Paused), вміє пауза, стоп і гучність;
7. помилка сервера не «збиває» застосунок — показується текстом, а не винятком.

Решта вкладок (Reader, Character Voice, повний Settings) — наступні ітерації.

---

## 5. Цільове дерево файлів

```
UA_TTS_Client/
├── AGENTS.md                     ← створено
├── README.md                     ← перенесено з Doc/Readme.md і дописано (Крок 9.2)
├── Doc/
│   ├── Final_Plan.md             ← цей файл
│   ├── API.md                    ← контракт ендпоінтів (Крок 9.2)
│   ├── ua_tts_client_arch.md     ← виправлено під реальність (Крок 9.2)
│   ├── Єдина кодова база linux win11.md
│   └── Запит на архітектуру.md
├── UA_TTS_Client.sln             ← створено (Крок 0.6)
├── App.axaml / App.axaml.cs      ← Composition Root (DI)
├── Program.cs                    ← не змінюється (шаблон Avalonia)
├── ViewLocator.cs                ← виправлено (Крок 8)
├── Core/                         ← створено
│   ├── Enums/      PlaybackState.cs, ServerStatus.cs
│   ├── Exceptions/ TtsApiException.cs
│   ├── Models/     AppConfig.cs, TtsRequestDto.cs, VerbalizeRequestDto.cs,
│   │               TextResponseDto.cs, ApiErrorResponse.cs, ServerHealth.cs
│   └── Interfaces/ ISettingsService.cs, ITtsApiClient.cs, IAudioPlayerService.cs,
│                   IClipboardAutomationService.cs, IGlobalHotkeyService.cs
├── Infrastructure/               ← створено
│   ├── Api/        TtsApiClient.cs
│   ├── Audio/      BassAudioPlayerService.cs
│   ├── Automation/ SharpHookGlobalHotkeyService.cs, WaylandClipboardAutomationService.cs
│   └── Storage/    JsonSettingsService.cs
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs
│   ├── Tabs/       QuickTtsViewModel.cs, SettingsViewModel.cs
│   │               ReaderViewModel.cs, CharacterVoiceViewModel.cs   ← заглушки
│   ├── Components/ PlaybackControlViewModel.cs
│   └── Design/     DesignData.cs   ← дані для прев'ю XAML у Rider
└── Views/
    ├── MainWindow.axaml(.cs)
    └── Tabs/       QuickTtsView.axaml(.cs), SettingsView.axaml(.cs),
                    ReaderView.axaml(.cs), CharacterVoiceView.axaml(.cs)
```

> Порожні `<Folder Include=…>` з `.csproj` прибрано на Кроці 0.4. Усі каталоги вище
> **вже містять реалізовані файли** (Кроки 1–9.2). У `UA_TTS_Client.sln` поки що лише
> основний проєкт — тестовий (`UA_TTS_Client.Tests`) додається на Кроці 9.1.
>
> **Видалено на Кроці 9.2:** `Doc/implementation_roadmap.md` (посилання на неіснуючі
> `/api/ping` і `/health`), `Doc/ua_tts_client_architecture(remove).md` (конфліктний дубль),
> `Doc/Readme.md` (перенесено в `README.md` у корінь).

---

## 6. Кроки

### Крок 0 — Передумови та гігієна ✅

**Мета:** привести репозиторій і середовище до стану, придатного для розробки.

| # | Дія | Статус |
|---|---|---|
| 0.1 | Встановити системну бібліотеку BASS: `yay -S libbass` (AUR-пакет — саме `libbass`; `bass` з `extra` — це гра!) | ✅ `/usr/lib/libbass.so` встановлено |
| 0.2 | `.gitignore`: `Server_py/__pycache__/`, `Server_py/venv/`, `*.wav`, `*.mp3`, `*.log`; прибрати непрацюючий `./Server_py/venv` | ✅ зроблено |
| 0.3 | `.csproj`: прибрати зайвий `System.Text.Json` (NU1510) | ✅ зроблено |
| 0.4 | `.csproj`: прибрати 13 порожніх `<Folder Include=…>` (будуть замінені реальними файлами) | ✅ прибрано |
| 0.5 | `Server_py/run_server.sh` — запуск венва з правильним cwd | ✅ створено, `chmod +x` |
| 0.6 | `UA_TTS_Client.sln` (проєкт + майбутній тестовий) | ✅ `UA_TTS_Client.sln` (класичний формат; `dotnet build` з кореня працює) |
| 0.7 | Перевірити демон `ydotool` (`systemctl --user is-active ydotool`) | ✅ enabled + active |

**0.5 — зміст `Server_py/run_server.sh`:**

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
exec venv/bin/python server.py
```

**Перевірка Кроку 0 (виконано):**
```bash
dotnet build                                   # 0 warnings, 0 errors
./Server_py/run_server.sh                      # Flask на http://127.0.0.1:5000
curl -sS http://127.0.0.1:5000/api/voices       # JSON-масив із 31 голосом
```

---

### Крок 1 — Core: контракти ✅

**Мета:** визначити чисті абстракції, від яких залежатимуть усі наступні кроки. `Core` не
посулається на Avalonia, `HttpClient`, ManagedBass чи SharpHook.

**Файли:**

| Файл | Вміст |
|---|---|
| `Core/Enums/PlaybackState.cs` | `Idle, Playing, Paused, Stopped, Error` — **власний** enum, не `ManagedBass.PlaybackState` |
| `Core/Enums/ServerStatus.cs` | `Unknown, Online, Offline, Error` |
| `Core/Models/AppConfig.cs` | `record`/`class`: `ServerUrl`, `SelectedVoice`, `Speed`, `Volume`, `Hotkey` |
| `Core/Models/TtsRequestDto.cs` | `record` з JSON-іменами, які сервер очікує: `text`, `voice`, `speed`, `volume`, `translate` |
| `Core/Models/VerbalizeRequestDto.cs` | `record { text }` |
| `Core/Models/ApiErrorResponse.cs` | `record { error }` |
| `Core/Interfaces/ISettingsService.cs` | `LoadAsync`, `SaveAsync`, `CancellationToken` |
| `Core/Interfaces/ITtsApiClient.cs` | `GetVoicesAsync`, `VerbalizeAsync`, `SynthesizeAsync` (→ `byte[]`), `ParseFileAsync` |
| `Core/Interfaces/IAudioPlayerService.cs` | `PlayAsync(byte[])`, `Pause`, `Stop`, `SetVolume`, `State`, подія зміни стану |
| `Core/Interfaces/IClipboardAutomationService.cs` | `CaptureSelectedTextAsync` (симулює Ctrl+C + читає буфер) |
| `Core/Interfaces/IGlobalHotkeyService.cs` | `Start`, `Stop`, подія `HotkeyPressed` |

**Вимоги:** XML-документація українською на всіх публічних членах; Guard Clauses;
`#nullable enable`; жодних залежностей від зовнішніх бібліотек.

**Перевірка (виконано):** `dotnet build` проходить; `Core/` не імпортує `Avalonia.*`,
`ManagedBass.*`, `SharpHook.*`.

**Фактично створено:** усі файли з таблиці плюс `Core/Models/ServerHealth.cs`,
`Core/Models/TextResponseDto.cs` (спільна відповідь `/api/verbalize` і `/api/parse_file`)
та `Core/Exceptions/TtsApiException.cs` (доменний виняток, щоб ViewModel не бачила
`HttpRequestException`). Подія стану — `EventHandler<PlaybackState>`; `Pause`/`Resume` розділені.

---

### Крок 2 — Сервер: мінімальні правки `server.py` ✅

**Мета:** додати health-check і привести помилки до єдиного формату. **Не ламати** сумісність
з браузерним розширенням (CORS-заголовки, формат відповіді `/api/synthesize`).

**Зміни в `Server_py/server.py`:**

1. Додати ендпоінт:
   ```python
   @app.route('/api/health', methods=['GET'])
   def api_health():
       return jsonify({
           "status": "ok",
           "device": device,            # 'cuda' | 'cpu'
           "voices": len(voices),
           "model": "patriotyk/styletts2_ukrainian_multispeaker",
       })
   ```
2. Уніфікувати помилки `/api/convert` та `/api/parse_file`:
   `return "No file", 400` → `return jsonify({"error": "No file"}), 400`
   (зараз це plain text із `Content-Type: text/html` — клієнт змушений терпіти обидва формати).
3. Жодних інших змін. CORS-блок `after_request` не чіпати.

**Перевірка (виконано 2026-10-21):**
```bash
curl -sS http://127.0.0.1:5000/api/health
# {"device":"cuda","model":"...","status":"ok","voices":31}

curl -sS -X POST http://127.0.0.1:5000/api/convert -F "x=1"
# {"error":"No file"}   (Content-Type: application/json)

curl -sS -X POST http://127.0.0.1:5000/api/synthesize \
  -H "Content-Type: application/json" \
  -d '{"text":"Привіт.","voice":"Денис Денисенко","speed":1.0,"volume":1.0}' -o t.wav
# 200, audio/wav, RIFF WAVE 16-bit mono 24000 Hz
```

---

### Крок 3 — Infrastructure: Storage + API ✅

**Файли:**

| Файл | Ключові рішення |
|---|---|
| `Infrastructure/Storage/JsonSettingsService.cs` | Шлях: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UA_TTS_Client", "config.json")`. Атомарний запис: temp-файл + `File.Move(..., overwrite: true)`. `System.Text.Json` з `WriteIndented = true` |
| `Infrastructure/Api/TtsApiClient.cs` | Typed `HttpClient`. Таймаут **≥120 с** (генерація великого тексту довша). Парсинг помилок: спочатку пробує JSON (`{"error"}`), інакше читає plain text. `SynthesizeAsync` → `byte[]` (WAV) |

**Обробка помилок — обов'язковий контракт:**
- `404` на `/api/health` → fallback на `GET /api/voices` (щоб клієнт працював і зі старим сервером);
- помилка сервера → доменний виняток/результат, **не** `HttpRequestException` у ViewModel.

**Реєстрація (Крок 8):** `services.AddHttpClient<ITtsApiClient, TtsApiClient>(c => c.BaseAddress = new Uri(cfg.ServerUrl))`

**Перевірка (виконано):** тимчасовий консольний smoke-test усіх ендпоінтів через `TtsApiClient`
(`/tmp/opencode/smoke`, поза репозиторієм): voices, health, verbalize, synthesize, parse_file,
помилка JSON, offline-сервер, fallback `404 /api/health → /api/voices` — усе PASS.

**Фактично:**
- таймаут у конструкторі піднімається до 5 хв, якщо він менший за 2 хв;
- `TtsApiException` має `StatusCode` і `IsConnectionError` (мережа/таймаут);
- `JsonSettingsService` має другий конструктор із явним шляхом — для тестів Кроку 9;
- на Linux `TextCopy` вимагає X11, тому в automation-сервісі основний шлях читання — `wl-paste`,
  а TextCopy/xsel — fallback; після Ctrl+C буфер опитується до 900 мс.

---

### Крок 4 — Infrastructure: Audio (ManagedBass 4.0.2) ✅

**Файл:** `Infrastructure/Audio/BassAudioPlayerService.cs` — реалізація `IAudioPlayerService`,
`IDisposable`.

**Життєвий цикл:**
1. `Bass.Init(Bass.DefaultDevice, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero)` —
   один раз, з перевіркою `Bass.LastError`. **Не `0`**: на Linux індекс 0 — це «No sound»,
   а `Bass.DefaultDevice == -1` — активний вихід PipeWire/PulseAudio.
2. `int handle = Bass.CreateStream(wavBytes, 0, wavBytes.Length, BassFlags.Default)` —
   масив `byte[]` **має жити весь час відтворення** (pin через `GCHandle` або зберегти в полі).
3. `Bass.ChannelSetSync(handle, SyncFlags.End, 0, syncProc, IntPtr.Zero)` — автозупинка/подія
   «дослухано». Делегат: `SyncProcedure(int syncHandle, int channel, int data, IntPtr user)` —
   **перший параметр це дескриптор синхронізації**, канал — другий.
4. `Bass.ChannelPlay(handle, false)` — іменованого аргументу `restart` у 4.0.2 немає.
5. Пауза — `Bass.ChannelPause(handle)` (toggle; атрибута `PlaybackPaused` **не існує**).
6. Гучність — `Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, 0f..1f)`.
7. Стан — `Bass.ChannelIsActive(handle)` → `ManagedBass.PlaybackState { Stopped, Playing, Stalled, Paused }`.
8. Зупинка — `Bass.ChannelStop(handle)`, звільнення — `Bass.StreamFree(handle)`, вихід — `Bass.Free()`.

**Обов'язково:** `IDisposable`, обробка `Bass.LastError` / `BassException.ErrorCode`,
`ILogger<T>`, без запису тимчасових `.wav` на диск.

**Перевірка (виконано):** сервіс відтворив WAV, отриманий від сервера (позиція BASS рухалася);
пауза/продовження/стоп/гучність працюють; кінець треку автоматично дає `Stopped`.
Знайдено й виправлено помилку: `SyncProcedure` передає дескриптор синхронізації першим
параметром, тому автозупинка спершу не спрацьовувала.
**Пізніше знайдено причину тиші в UI:** `Bass.Init(0, …)` відкривав пристрій «No sound».
Після переходу на `Bass.DefaultDevice` `pactl list sink-inputs` показує потік
`PipeWire ALSA [app]` на активному sink (Bluetooth-навушники), звук іде.

---

### Крок 5 — Infrastructure: Automation (хук + буфер) ✅ ⚠️ Wayland

**Файли:**

| Файл | Реалізація |
|---|---|
| `Infrastructure/Automation/SharpHookGlobalHotkeyService.cs` | `TaskPoolGlobalHook(parallelismLevel: 1, new UioHookProvider())`; підписка на `KeyPressed`/`KeyReleased`; Alt+Q озброюється на натисканні й спрацьовує **після відпускання обох клавіш** (інакше Ctrl+C іде як Ctrl+Alt+C). `KeyboardEventData` — **структура**: поле `KeyCode`, властивість `KeyChar`; імена enum — з префіксом `Vc` (`VcQ`, `VcLeftAlt`, `VcLeftControl`). Модифікатори треба тримати у власному стані (окремим хуком на `VcLeftAlt`/`VcRightAlt`), у `KeyboardEventData` їх немає. Ігнорувати власні симульовані події через `IsEventSimulated` |
| `Infrastructure/Automation/WaylandClipboardAutomationService.cs` | `SharpHook.Simulation.EventSimulator` → `Sequence()` → `AddKeyPress(KeyCode.VcLeftControl)` + `AddKeyPress(KeyCode.VcC)` + `AddKeyRelease(...)` → `.Simulate()` → опитувати буфер до 900 мс, доки він не зміниться (Wayland: `wl-paste`, fallback — `TextCopy`/`xsel`) |

**Wayland-специфіка:** Wayland не дає доступу до виділення в чужих вікнах — лише симуляція
Ctrl+C через uinput. Користувач у групі `input`, демон `ydotool` активний.

**Перевірка:** Alt+Q з будь-якого вікна (напр. терміналу) додає виділений текст у поле;
кнопка «Захопити виділене» працює так само.

**Фактично:**
- хук стартує й отримує події (перевірено симульованими `Ctrl+F13`; власні симульовані події
  відфільтровуються через `IsEventSimulated`);
- **тригер перенесено на відпускання Alt+Q**: інакше симуляція Ctrl+C надсилалася, поки
  фізичний Alt ще затиснутий, і вікно отримувало `Ctrl+Alt+C`;
- резервні шляхи: SharpHook `EventSimulator` → `ydotool key 56:0 100:0 29:1 46:1 46:0 29:0`
  для симуляції; `wl-paste` (Wayland) → `TextCopy`/`xsel` для читання буфера;
- повний ланцюжок «виділення → симульований Ctrl+C → `wl-paste`» перевірено на
  `zenity --entry`: захоплено рівно `CLIP_TEST_98765`;
- якщо буфер не змінився за 900 мс, сервіс повертає порожній рядок, а не старий вміст;
- **обмеження:** у терміналах копіювання — `Ctrl+Shift+C`, тому симульований `Ctrl+C` там
  надсилає SIGINT; для термінала треба скопіювати вручну й вставити в поле (`Ctrl+V`);
- **живу перевірку Alt+Q фізично виконано** (хук спрацьовує); у цій сесії `ydotool` для
  автоматизації не працює, тому UI-перевірка виконується вручну.

> Якщо SharpHook під Wayland не перехоплює клавіатуру — fallback: викликати `ydotool` CLI
> через `Process.Start` замість `EventSimulator`.

---

### Крок 6 — ViewModels ✅

**Файли:**

| Файл | Відповідальність |
|---|---|
| `ViewModels/MainWindowViewModel.cs` | Статус сервера (періодична перевірка з `CancellationToken`), заголовки вкладок, координація |
| `ViewModels/Tabs/QuickTtsViewModel.cs` | Текст, «Захопити виділене», «Озвучити», стан синтезу, прогрес/помилки |
| `ViewModels/Tabs/SettingsViewModel.cs` | URL сервера, список голосів, швидкість, гучність, гаряча клавіша |
| `ViewModels/Tabs/ReaderViewModel.cs` | Заглушка (наступна ітерація) |
| `ViewModels/Tabs/CharacterVoiceViewModel.cs` | Заглушка (наступна ітерація) |
| `ViewModels/Components/PlaybackControlViewModel.cs` | Play / Pause / Stop, гучність, поточний стан, підписка на подію стану |

**Вимоги:** усі — `public partial class` + `[ObservableProperty]` / `[RelayCommand]`;
залежності лише через конструктор; асинхронні команди з `CancellationToken`;
жодного `ConfigureAwait` без потреби; `ILogger<T>`.

**Перевірка (виконано):** `dotnet build` — 0 warnings; консольний harness на реальних сервісах
перевірив завантаження 31 голосу, захоплення тексту, синтез, перехід `Playing → Stopped`,
збереження й читання конфігу, синхронізацію гучності, валідацію URL, стани `Online`/`Offline`.

**Фактично:**
- `ViewModelBase` запам'ятовує `SynchronizationContext` і надає `OnUiThread` — події BASS
  приходять із фонового потоку, тому стан плеєра оновлюється через нього;
- `SettingsViewModel` є джерелом істини для `QuickTtsViewModel` (голос, швидкість);
  гучність синхронізується з `PlaybackControlViewModel` в обидва боки;
- `MainWindowViewModel` тримає `PeriodicTimer` (перевірка сервера кожні 15 с) і підписку
  на `IGlobalHotkeyService.HotkeyPressed` (Alt+Q → захоплення виділеного);
- для прев'ю Rider додано `ViewModels/Design/DesignData.cs` з фіктивними реалізаціями
  інтерфейсів; production-код його не використовує.

---

### Крок 7 — Views (XAML) ✅

**Файли:** `Views/MainWindow.axaml(.cs)`, `Views/Tabs/QuickTtsView.axaml(.cs)`,
`Views/Tabs/SettingsView.axaml(.cs)`.

**Обов'язково:**
- `x:DataType="vm:XxxViewModel"` у кожному XAML (compiled bindings → перевірка ще на збірці);
- design-time DataContext — у code-behind через `if (Design.IsDesignMode) DataContext = DesignData…`
  (Rider-аналізатор помилково скаржиться на `Design.DataContext` з `x:Static`);
- `RequestedThemeVariant="Dark"` уже встановлено в `App.axaml` — не змінювати;
- Avalonia 12: замість `Window` у вкладках — `UserControl`; іконки — лише з `Assets/`.

**Макет `MainWindow`:**
- верхня панель: назва + **індикатор стану сервера** (зелений/червоний коло) + кнопка перевірки;
- центральний `TabControl` на 4 вкладки;
- нижня панель: міні-плеєр (Play/Pause/Stop, гучність, стан, назва голосу).

**Перевірка (виконано):** вікно відкривається, вкладки перемикаються, індикатор сервера стає зеленим.

**Фактично:**
- додано `Views/Tabs/ReaderView.axaml(.cs)` і `CharacterVoiceView.axaml(.cs)`, щоб ViewLocator
  резолвив усі чотири вкладки (у початковому дереві були лише дві);
- Avalonia 12: `TextBox.Watermark` застарілий → `PlaceholderText`; прокрутка всередині
  `TextBox` задається через `ScrollViewer.VerticalScrollBarVisibility`;
- стилі `Classes="hint"` і `Ellipse.status-dot.online` описані в `MainWindow.Styles`;
- візуальну перевірку зроблено скриншотом робочого вікна (KDE/Wayland): верхня панель
  «Онлайн · cuda · 31 голосів» із зеленим індикатором, 4 вкладки, міні-плеєр працюють.

---

### Крок 8 — Composition Root (DI) ✅

**Файл:** `App.axaml.cs` — єдине місце реєстрації.

```csharp
var services = new ServiceCollection();
services.AddSingleton<ISettingsService, JsonSettingsService>();
services.AddSingleton<IAudioPlayerService, BassAudioPlayerService>();
services.AddSingleton<IClipboardAutomationService, WaylandClipboardAutomationService>();
services.AddSingleton<IGlobalHotkeyService, SharpHookGlobalHotkeyService>();
services.AddHttpClient<ITtsApiClient, TtsApiClient>(...);   // таймаут ≥120 с
services.AddSingleton<MainWindowViewModel>();
services.AddSingleton<QuickTtsViewModel>();
services.AddSingleton<SettingsViewModel>();
services.AddSingleton<PlaybackControlViewModel>();
desktop.MainWindow = new MainWindow { DataContext = services.BuildServiceProvider().GetRequiredService<MainWindowViewModel>() };
```

**Обов'язково виправити `ViewLocator.cs`:** зараз він будує ім'я через
`FullName.Replace("ViewModel", "View")` → `UA_TTS_Client.Views.MainView`, чого **не існує**
(є `Views.MainWindow`). Результат — `TextBlock "Not Found: …"` замість вікна.

**Перевірка (виконано):** запуск застосунку → вікно, вкладки, статус сервера, звук.

**Фактично:**
- `App.axaml.cs` реєструє `AddLogging` (консоль, фільтр для шумних `HttpClient`-логів),
  `JsonSettingsService`, `BassAudioPlayerService`, automation-сервіси, типізований
  `HttpClient` і чотири ViewModel;
- конфігурація читається синхронно **до** побудови контейнера (адреса сервера потрібна
  для `BaseAddress`) і реєструється як singleton `AppConfig`; зміна URL застосовується
  після перезапуску — це прямо написано в UI;
- `MainWindowViewModel.InitializeAsync` викликається з `window.Opened`;
  `desktop.Exit` звільняє ViewModel і `ServiceProvider` (BASS, хук, клієнти);
- `ViewLocator` тепер замінює `.ViewModels` → `.Views` і `ViewModel` → `View`, а для
  `MainWindowViewModel` додатково пробує `Views.MainWindow`.

---

### Крок 9 — Тести + документація 🔶 (9.2 ✅ · 9.1 ⬜)

**9.1 Тестовий проєкт** ⬜ **не розпочато** (за користувачем — виконувати окремо від документації):
`UA_TTS_Client.Tests` (xUnit + FluentAssertions + NSubstitute):
- `JsonSettingsService` — збереження/завантаження, атомарність, відсутній файл;
  готовий другий конструктор `JsonSettingsService(logger, filePath)` із явним шляхом;
- `TtsApiClient` — помилки JSON і plain text, таймаут, недоступний сервер (offline),
  fallback `404 /api/health → /api/voices`;
- ViewModel — guard-клози та скасування через `CancellationToken`.

Правила: AAA, ізоляція зовнішніх залежностей через інтерфейси, іменування
`Метод_Умови_ОчікуванийРезультат`.

**9.2 Документація** ✅ **виконано:**

| # | Дія | Стан |
|---|---|---|
| 9.2.1 | `Doc/Readme.md` → **`README.md` у корінь**: розділ «Як користуватися», реальний запуск Linux/Windows, прибрано `run_server.bat` / `UA_TTS_Client_Full_Cycle` | ✅ |
| 9.2.2 | `Doc/ua_tts_client_arch.md` — переписано під реальність: ManagedBass 4.0.2 (`Bass.CreateStream(byte[])`, `Bass.DefaultDevice`), фактичне дерево файлів, сигнатури інтерфейсів, DI, Wayland | ✅ |
| 9.2.3 | `Doc/implementation_roadmap.md` — видалити | ✅ |
| 9.2.4 | `Doc/ua_tts_client_architecture(remove).md` — видалити (конфліктний дубль) | ✅ |
| 9.2.5 | `Doc/API.md` — новий: точний контракт ендпоінтів із прикладами запитів/відповідей | ✅ |
| 9.2.6 | `AGENTS.md` і `Doc/Final_Plan.md` — оновити стан | ✅ |

**Фактично (Крок 9.2):**
- `README.md` (корінь): вимоги (`libbass`, `wl-clipboard`/`xsel`, група `input`),
  запуск сервера (`./Server_py/run_server.sh` / `cd Server_py && python server.py`),
  запуск клієнта (`dotnet run`), повний розділ «Як користуватися» (статус-бар,
  Quick TTS, Alt+Q, міні-плеєр, налаштування, обмеження терміналів), посилання на
  `Doc/Final_Plan.md`, `AGENTS.md`, `Doc/API.md`;
- `Doc/API.md`: 6 ендпоінтів із таблицями полів, прикладами `curl` і JSON,
  форматом помилок, правилами сумісності клієнта (fallback health, терпимий парсер
  помилок, таймаут 5 хв). `/api/ping` і `/health` позначені як неіснуючі;
- `Doc/ua_tts_client_arch.md`: прибрано хибні `BASS_StreamCreateFile`/`GCHandle`,
  `IDocumentParserService`, `ExportFormat`, `PingServerAsync`, `yay -S bass` і «план
  наступних дій»; додано фактичний ManagedBass API, дерево, контракти, DI і Wayland;
- `README.md` пояснює, чому `Doc/Readme.md` перенесено: файл був обриваний на
  порожньому розділі «Як користуватися» й посилався на неіснуючі `run_server.bat`
  та `UA_TTS_Client_Full_Cycle`;
- **тести (9.1) не створювалися** — за запитом користувача лише документація.

---

## 7. Довідкові контракти

### 7.1 API сервера (перевірено curl-ом, 2026-10-21)

Base URL `http://127.0.0.1:5000`. CORS: `Access-Control-Allow-Origin: *`.

| Метод | Шлях | Тіло | Успіх | Помилка |
|---|---|---|---|---|
| GET | `/api/voices` | — | `["Вероніка Дорош", …]` (31) | — |
| GET | `/api/health` | — | `{"status","device","voices","model"}` *(після Кроку 2)* | — |
| POST | `/api/verbalize` | `{"text"}` | `{"text"}` | 400 `{"error":"No text"}` |
| POST | `/api/synthesize` | `{"text","voice","speed","volume","translate"}` | `audio/wav`, 24 кГц моно 16-bit PCM | 500 `{"error"}` |
| POST | `/api/convert` | multipart `file`, `format` | `audio/<fmt>` | 400 `{"error":"No file"}` *(після Кроку 2)* |
| POST | `/api/parse_file` | multipart `file` | `{"text"}` | 400 `{"error":"No file"}` *(після Кроку 2)* |

`translate` — опціональний, `false` за замовчуванням; `speed`/`volume` — за замовчуванням `1.0`.

### 7.2 ManagedBass 4.0.2 — фактичний API

Класів `ManagedBass.Playback` / `ManagedBass.Channel` **не існує**. Лише статичний `ManagedBass.Bass`:
`Init`, `Free`, `CreateStream(byte[], long, long, BassFlags)`, `ChannelPlay`, `ChannelPause`,
`ChannelStop`, `ChannelSetAttribute`, `ChannelSetSync`, `ChannelIsActive`, `StreamFree`,
`LastError`, `Volume`. Делегат `SyncProcedure(int, int, int, IntPtr)`.
Повний перелік — в `AGENTS.md`.

### 7.3 SharpHook 8.0.0 — фактичний API

`TaskPoolGlobalHook(int parallelismLevel, IGlobalHookProvider)`; події `KeyPressed`, `KeyReleased`,
`KeyTyped`, `HookEnabled`, `HookDisabled`. `KeyboardHookEventArgs`: `Data`, `RawEvent`,
`EventTime`, `IsEventSimulated`, `SuppressEvent`. `KeyboardEventData` — структура з полями
`KeyCode`, `RawCode`, `RawKeyChar` та властивістю `KeyChar`. Імена `KeyCode` — з префіксом `Vc`.
Симуляція: `EventSimulator` → `Sequence()` → `AddKeyPress(KeyCode)` / `AddKeyRelease(KeyCode)` →
`.Simulate()`; скорочено — `SimulateKeyStroke(params KeyCode[])`. Читання буфера:
`TextCopy.ClipboardService.GetText()` / `GetTextAsync(CancellationToken)`.

---

## 8. Ризики

| Ризик | Ймовірність | Мітигація |
|---|---|---|
| SharpHook не перехоплює клавіатуру під Wayland | середня | ✅ хук стартує й отримує події на цій машині; для симуляції є fallback на `ydotool`, для читання — на `wl-paste` |
| `CreateStream(byte[])` не працює як очікується (прапорця `Memory` в enum немає) | низька | ✅ перевірено: WAV із `byte[]` відтворюється, позиція рухається |
| Немає ALSA/PulseAudio-виходу в сеансі Wayland | низька | ✅ `Bass.DefaultDevice` створює потік у PipeWire (перевірено `pactl`); `Bass.Init(0, …)` вів на «No sound» — виправлено. Помилка ініціалізації переводить сервіс у `Error` |
| Сервер довго генерує великий текст | низька | Таймаут 5 хв у `TtsApiClient`, індикатор синтезу в UI |
| Помилка парсингу відповіді сервера | середня | Парсер терпить і JSON, і plain text (див. Крок 3) |
| Помилки компіляції в XAML через compiled bindings | висока | `x:DataType` у кожному файлі; перевірка на `dotnet build` після Кроку 7 |

---

## 9. Журнал виконання

| Дата | Що зроблено |
|---|---|
| 2026-10-21 | Аналіз репозиторію: структура, `.csproj`, `server.py`, усі 6 файлів `Doc/`, стан середовища |
| 2026-10-21 | Сервер запущено та перевірено curl-ом: `/api/voices` 200, `/api/verbalize` 200, `/api/synthesize` 200 (WAV 24 кГц моно 16-bit), `/api/ping` 404, `/health` 404, `/api/health` 404. Модель у кеші HF (~911 МБ), CUDA активна, синтез ≈0.5 с |
| 2026-10-21 | Рефлексія API: ManagedBass 4.0.2, SharpHook 8.0.0, TextCopy 6.2.1 (через тимчасовий проєкт) |
| 2026-10-21 | Перевірено `dotnet build` → успішно; встановлено `libbass`; увімкнено демон `ydotool` |
| 2026-10-21 | Створено `AGENTS.md` — перевірені факти, підводні камені, точні API |
| 2026-10-21 | Виправлено `.gitignore` (не закомічено) та `.csproj` — прибрано `System.Text.Json` (не закомічено) |
| 2026-10-21 | Створено цей документ |
| 2026-10-21 | **Кроки 0–5 реалізовано.** Крок 0: прибрано `<Folder Include>` з `.csproj`, створено `Server_py/run_server.sh`, `UA_TTS_Client.sln`. Крок 1: `Core/` (enum-и, 7 моделей, 5 інтерфейсів, `TtsApiException`). Крок 2: `/api/health`, уніфіковані JSON-помилки (`server.py`). Крок 3: `JsonSettingsService`, `TtsApiClient` із fallback health і доменними винятками. Крок 4: `BassAudioPlayerService` — відтворення з пам'яті, пауза/стоп/гучність, автозупинка. Крок 5: `SharpHookGlobalHotkeyService` (Alt+Q) + `WaylandClipboardAutomationService` (Ctrl+C, ydotool/wl-paste fallback). `dotnet build` — 0 warnings / 0 errors; smoke-тест усіх ендпоінтів і сервісів — PASS |
| 2026-10-21 | Виявлено під час Кроку 4: `Bass.SyncProcedure` передає **дескриптор синхронізації** першим, канал — другим; через це автозупинка не спрацьовувала. Виправлено, внесено в `AGENTS.md` |
| 2026-10-21 | Виявлено: `TextCopy` на Linux працює лише через X11; додано fallback `wl-paste` → `xsel` у `WaylandClipboardAutomationService` |
| 2026-10-21 | **Кроки 6–8 реалізовано.** ViewModels: `MainWindowViewModel` (health-check кожні 15 с, Alt+Q), `QuickTtsViewModel` (захоплення → синтез → плеєр, скасування), `SettingsViewModel` (голоси, швидкість, гучність, збереження), `PlaybackControlViewModel`, заглушки Reader/Character Voice. Views: головне вікно з індикатором сервера, 4 вкладки, міні-плеєр; XAML з `x:DataType`, design-time DataContext у code-behind через `Design.IsDesignMode`. DI: усі сервіси + ViewModel-и, конфіг читається до побудови контейнера, `ViewLocator` виправлено |
| 2026-10-21 | Перевірено: `dotnet build` — 0 warnings / 0 errors; VM-harness на реальних сервісах (31 голос, синтез, стан плеєра, збереження конфігу, Online/Offline) — ALL PASSED; скриншот робочого вікна підтвердив макет і зелений індикатор «Онлайн · cuda · 31 голосів» |
| 2026-10-21 | Виправлено тишу: `Bass.Init(0)` відкривав пристрій «No sound». Перейшли на `Bass.DefaultDevice` (-1). Перевірено через `pactl list sink-inputs`: з'являється `PipeWire ALSA [app]` на sink 86 (Bluetooth-навушники), гучність 100% |
| 2026-10-21 | Виправлено захоплення виділеного: Alt+Q тепер спрацьовує після відпускання обох клавіш (раніше Ctrl+C ішов як Ctrl+Alt+C); на Wayland першим читається `wl-paste`; буфер опитується до 900 мс, і якщо не змінився — повертається порожній рядок, а не старий вміст. Ланцюжок перевірено harness-ом на `zenity`: захоплено `CLIP_TEST_98765` |
| 2026-10-21 | Прибрано попередження Rider: `Design.DataContext="{x:Static …}"` замінено на `if (Design.IsDesignMode) DataContext = DesignData…` у code-behind. XAML-аналізатор більше не плутає attached property; `dotnet build` — 0 warnings |
| 2026-10-21 | Закомічено роботу Кроків 6–8 і виправлення: `e7b04a1 feat(ui)`, `32068fa fix(audio)`, `5d547f2 fix(automation)`, `2a2cdc6 docs`. `dotnet build` — 0 warnings / 0 errors; робоче дерево після комітів чисте |
| 2026-10-21 | **Крок 9.2 (документація) виконано** — за запитом користувача **без тестів (9.1 лишився)**. `Doc/Readme.md` перенесено в `README.md` у корінь і дописано: вимоги (`libbass`, `wl-clipboard`/`xsel`, група `input`), запуск сервера (`./Server_py/run_server.sh` або `cd Server_py && python server.py`), запуск клієнта, повний розділ «Як користуватися», прибрано неіснуючі `run_server.bat` і `UA_TTS_Client_Full_Cycle`. Створено `Doc/API.md` — контракт 6 ендпоінтів із таблицями полів, `curl`-прикладами, форматом помилок і правилами сумісності клієнта. `Doc/ua_tts_client_arch.md` переписано під реальність: прибрано `BASS_StreamCreateFile`/`GCHandle`, `yay -S bass`, `IDocumentParserService`, `ExportFormat`, `PingServerAsync`; додано фактичний API ManagedBass 4.0.2, дерево файлів, сигнатури інтерфейсів, Composition Root і Wayland-поведінку. Видалено `Doc/implementation_roadmap.md` та `Doc/ua_tts_client_architecture(remove).md`. Оновлено `AGENTS.md` (статус, таблиця `Doc/`, стан git) |

---

## 10. Як продовжити роботу в новій сесії

1. Прочитати `AGENTS.md` і цей файл.
2. Перевірити стан Кроків: `git status`, наявність файлів у `Core/`, `Infrastructure/`,
   `ViewModels/`, `Views/`.
3. Перший незавершений крок (розділ 3) — **Крок 9.1: тести** (`UA_TTS_Client.Tests`,
   xUnit + FluentAssertions + NSubstitute). Далі — наступні ітерації: Reader,
   Character Voice, зміна гарячої клавіші.
4. Після кроку — оновити статус у розділі 3 і додати рядок у журнал (розділ 9).
5. **Коміти:** не комітити чужі незакомічені зміни без запиту. Працю над Кроками 0–5 можна
   вести одним turn-ом (рішення R5); Кроки 6–9 — окремо з перевіркою користувача.
6. Перед Кроком 4 переконатися, що `libbass` на місці:
   `ldconfig -p | grep libbass`.
