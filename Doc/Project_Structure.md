# Структура проєкту UA_TTS_Client

> **Призначення:** карта коду для агента й розробника: де що лежить, як пов'язані шари,
> куди додавати нове. Це не історія архітектури й не план робіт.
>
> **Актуально на:** 2026-10-22 (після Кроків 10–11).
> **План робіт:** `Doc/Curent_Work_Plan.md`. **Перевірені факти й пастки:** `AGENTS.md`.
> **Контракт сервера:** `Doc/API.md`.

---

## 1. Що це

`UA_TTS_Client` — Avalonia 12 / .NET 10 десктопний клієнт (MVVM, CommunityToolkit.Mvvm)
для локального Python TTS-сервера (`Server_py/`, Flask + StyleTTS2).

Цільові ОС: Linux (EndeavourOS/Arch, Wayland + PipeWire) і Windows 11.

Один проєкт (`UA_TTS_Client.csproj`), без окремих бібліотек. Шари розділені каталогами
й правилами залежностей, а не проєктами.

---

## 2. Шари та правила залежностей

| Шар | Каталог | Що містить | Може залежати від |
|---|---|---|---|
| **Domain** | `Core/` | enum-и, DTO, доменні винятки, інтерфейси сервісів | **ні від чого** (без Avalonia, HttpClient, ManagedBass, SharpHook) |
| **Infrastructure** | `Infrastructure/` | реалізації `Core`-інтерфейсів: HTTP, BASS, хук, буфер, JSON-конфіг | `Core`, зовнішні бібліотеки |
| **Presentation (VM)** | `ViewModels/` | MVVM-логіка, `public partial class`, команди, стани | `Core`, `ILogger<T>` |
| **Presentation (View)** | `Views/` | XAML + code-behind, design-time DataContext | `ViewModels` |
| **Composition Root** | `App.axaml.cs` | реєстрація DI, створення вікна, старт/завершення | усі шари |
| **UI-сервіси** | `Views/Services/` (за потреби) | сервіси, яким потрібен Avalonia (напр. файлові діалоги) | Avalonia, `Core` |

Правила:

- DI — **лише через конструктори**; реєстрація — тільки в `App.axaml.cs`.
- `Core` не знає про UI та бібліотеки; DTO — `record`.
- Помилки транспорту перетворюються на `TtsApiException` до того, як дійти до ViewModel.
- Аудіо відтворюється **з пам'яті** (`byte[]`), без тимчасових `.wav`.
- Шляхи — `Path.Combine` + `Environment.SpecialFolder.ApplicationData`.
- Коментарі та XML-документація — українською.

---

## 3. Фактичне дерево

```text
UA_TTS_Client/
├── AGENTS.md                     ← факти, API, підводні камені
├── README.md                     ← запуск і користування
├── UA_TTS_Client.sln
├── UA_TTS_Client.csproj
├── App.axaml / App.axaml.cs      ← Composition Root (DI)
├── Program.cs
├── ViewLocator.cs                ← ViewModel → View
├── Assets/
├── Server_py/                    ← Flask + StyleTTS2 (окремий Python-проєкт)
├── Doc/
│   ├── Curent_Work_Plan.md       ← план робіт
│   ├── Project_Structure.md      ← цей файл
│   ├── API.md                    ← контракт сервера
│   └── Промпт для ллм - Єдина кодова база arch-win11.md
├── Core/
│   ├── Enums/
│   │   ├── PlaybackState.cs      ← Idle, Playing, Paused, Stopped, Error
│   │   └── ServerStatus.cs       ← Unknown, Online, Offline, Error
│   ├── Exceptions/
│   │   └── TtsApiException.cs    ← StatusCode, IsConnectionError
│   ├── Models/
│   │   ├── AppConfig.cs          ← ServerUrl, SelectedVoice, Speed, Volume, Hotkey
│   │   ├── TtsRequestDto.cs      ← text, voice, speed, volume, translate
│   │   ├── VerbalizeRequestDto.cs
│   │   ├── TextResponseDto.cs
│   │   ├── ApiErrorResponse.cs
│   │   └── ServerHealth.cs
│   └── Interfaces/
│       ├── ITtsApiClient.cs
│       ├── ISettingsService.cs
│       ├── IAudioPlayerService.cs
│       ├── IClipboardAutomationService.cs
│       └── IGlobalHotkeyService.cs
├── Infrastructure/
│   ├── Api/
│   │   └── TtsApiClient.cs       ← typed HttpClient
│   ├── Audio/
│   │   └── BassAudioPlayerService.cs
│   ├── Automation/
│   │   ├── SharpHookGlobalHotkeyService.cs
│   │   └── WaylandClipboardAutomationService.cs
│   └── Storage/
│       └── JsonSettingsService.cs
├── ViewModels/
│   ├── ViewModelBase.cs          ← OnUiThread, SymbolsWord
│   ├── MainWindowViewModel.cs    ← вкладки, health-check, гаряча клавіша
│   ├── Components/
│   │   └── PlaybackControlViewModel.cs
│   ├── Tabs/
│   │   ├── QuickTtsViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   ├── ReaderViewModel.cs    ← заглушка
│   │   └── CharacterVoiceViewModel.cs ← заглушка
│   └── Design/
│       └── DesignData.cs         ← фейки для прев'ю Rider
└── Views/
    ├── MainWindow.axaml(.cs)
    └── Tabs/
        ├── QuickTtsView.axaml(.cs)
        ├── SettingsView.axaml(.cs)
        ├── ReaderView.axaml(.cs)
        └── CharacterVoiceView.axaml(.cs)
```

> Наступна ітерація додасть нові файли (`TextChunker`, `FragmentPlaybackService`,
> `AudioExportService`, `WavAudioConcatenator`, `IFileSaveDialogService` тощо).
> Їхній перелік і порядок — у `Doc/Curent_Work_Plan.md`; цей файл оновлюється після реалізації.

---

## 4. Ключові контракти та реалізації

| Інтерфейс (`Core/Interfaces`) | Реалізація (`Infrastructure`) | Призначення |
|---|---|---|
| `ITtsApiClient` | `Api/TtsApiClient.cs` | voices, health, verbalize, synthesize, parse_file |
| `ISettingsService` | `Storage/JsonSettingsService.cs` | `config.json` з атомарним записом |
| `IAudioPlayerService` | `Audio/BassAudioPlayerService.cs` | BASS: play/pause/resume/stop/volume, `StateChanged` |
| `IClipboardAutomationService` | `Automation/WaylandClipboardAutomationService.cs` | симуляція `Ctrl+C` + читання буфера |
| `IGlobalHotkeyService` | `Automation/SharpHookGlobalHotkeyService.cs` | глобальний `Alt+Q`, подія `HotkeyPressed` |

`ITtsApiClient` реєструється як typed `HttpClient` з `BaseAddress` з конфігу та таймаутом 5 хв.
`IClipboardAutomationService` на Linux читає буфер лише через `wl-paste`; на Windows
використовує `TextCopy`. X11 не підтримується.

---

## 5. Composition Root (`App.axaml.cs`)

Порядок:

1. `LoadStartupConfig()` — **синхронно** читає `config.json` до побудови контейнера
   (адреса потрібна для `HttpClient.BaseAddress`).
2. `ConfigureServices`:
   - `AddLogging` (консоль, фільтр шумних `HttpClient`-логів);
   - `AppConfig` як singleton;
   - `ISettingsService`, `IAudioPlayerService`, `IClipboardAutomationService`,
     `IGlobalHotkeyService`;
   - `AddHttpClient<ITtsApiClient, TtsApiClient>()`;
   - `PlaybackControlViewModel`, `SettingsViewModel`, `QuickTtsViewModel`,
     `MainWindowViewModel` — singletons.
3. Створюється `MainWindow` з `DataContext = MainWindowViewModel`.
4. `window.Opened` → `MainWindowViewModel.InitializeAsync()`.
5. `desktop.Exit` → `Dispose()` ViewModel і `ServiceProvider`.

Зміна URL у налаштуваннях застосовується **після перезапуску** (це написано в UI).

---

## 6. Потоки виконання

### 6.1 Старт

`Program` → `App.OnFrameworkInitializationCompleted` → DI → `MainWindow` → `Opened` →
`MainWindowViewModel.InitializeAsync`:

1. підписка на `IGlobalHotkeyService.HotkeyPressed` і `Start()`;
2. `SettingsViewModel.InitializeAsync()` → `GET /api/voices`;
3. `RefreshServerStatusAsync()` → `GET /api/health` (fallback на `/api/voices` при 404);
4. фоновий `PeriodicTimer` — health-check кожні 15 с.

### 6.2 `Alt+Q` → захоплення тексту

1. `SharpHookGlobalHotkeyService` ловить `Alt+Q` після відпускання **обох** клавіш
   (щоб симульований `Ctrl+C` не став `Ctrl+Alt+C`).
2. Подія `HotkeyPressed` → `MainWindowViewModel.OnHotkeyPressed` → UI-потік.
3. `QuickTts.CaptureSelectedTextCommand` → `IClipboardAutomationService.CaptureSelectedTextAsync`.
4. Сервіс:
   - запам'ятовує поточний буфер;
   - симулює `Ctrl+C` через SharpHook `EventSimulator` (fallback — `ydotool` CLI);
   - опитує буфер до 900 мс, доки він не зміниться;
   - на Linux читає `wl-paste --no-newline`, на Windows — `TextCopy`;
   - повертає порожній рядок, якщо буфер не змінився.
5. Текст потрапляє в `QuickTts.Text`.

### 6.3 Синтез і відтворення (поточний стан)

1. `QuickTts.SynthesizeCommand` бере `Text`, `Settings.SelectedVoice`, `Settings.Speed`.
2. `ITtsApiClient.SynthesizeAsync` → `POST /api/synthesize` → WAV `byte[]`.
3. `PlaybackControlViewModel.PlayAsync` → `BassAudioPlayerService.PlayAsync`:
   - `Bass.Init(Bass.DefaultDevice, …)`;
   - `Bass.CreateStream(byte[], …)`;
   - `Bass.ChannelSetSync(End)` → `StateChanged = Stopped`;
   - `Bass.ChannelPlay`.
4. `StateChanged` оновлює міні-плеєр через `ViewModelBase.OnUiThread`.
5. Пауза/продовження/стоп/гучність — методи `IAudioPlayerService`.

### 6.4 Налаштування

- `SettingsViewModel` — джерело істини для голосу, швидкості, гучності.
- `Volume` синхронізується з `PlaybackControlViewModel` в обидва боки
  (захист від циклу — `_syncingVolume`).
- `SaveCommand` валідовує URL і пише `AppConfig` через `ISettingsService`.
- `JsonSettingsService`: `~/.config/UA_TTS_Client/config.json` (Linux) /
  `%APPDATA%\UA_TTS_Client\config.json` (Windows); тимчасовий файл + `File.Move`.

### 6.5 Вкладки

`MainWindow.axaml` містить `TabControl` з чотирма `TabItem`; вміст — `ContentControl`
з ViewModel. `ViewLocator` перетворює `ViewModels.*` → `Views.*`, а для
`MainWindowViewModel` додатково пробує `Views.MainWindow`. Головне вікно створюється явно
в `App.axaml.cs`.

---

## 7. View ↔ ViewModel

| View | ViewModel | Примітка |
|---|---|---|
| `Views/MainWindow.axaml` | `MainWindowViewModel` | статус сервера, вкладки, міні-плеєр |
| `Views/Tabs/QuickTtsView.axaml` | `QuickTtsViewModel` | текст, захоплення, синтез |
| `Views/Tabs/SettingsView.axaml` | `SettingsViewModel` | URL, голос, швидкість, гучність, hotkey |
| `Views/Tabs/ReaderView.axaml` | `ReaderViewModel` | заглушка |
| `Views/Tabs/CharacterVoiceView.axaml` | `CharacterVoiceViewModel` | заглушка |

У кожному XAML обов'язковий `x:DataType`. Design-time DataContext ставиться в code-behind
через `if (Design.IsDesignMode) DataContext = DesignData...` (не через `Design.DataContext`).
`DesignData` містить фейкові реалізації інтерфейсів; production-код його не використовує.

---

## 8. Точки розширення

### Новий сервіс

1. Інтерфейс — `Core/Interfaces/I...cs`.
2. Моделі/enum-и — `Core/Models` або `Core/Enums`.
3. Реалізація — відповідний каталог `Infrastructure/`.
4. Реєстрація — `App.axaml.cs`.
5. Фейк для прев'ю — `ViewModels/Design/DesignData.cs`, якщо сервіс потрібен ViewModel.

### Нове налаштування

1. Поле в `Core/Models/AppConfig.cs` з типовим значенням (старі конфіги мають читатися).
2. Властивість у `SettingsViewModel` + `ApplyConfig` + `SaveAsync`.
3. Поле в `SettingsView.axaml`.
4. Якщо потрібне для прев'ю — значення в `DesignData.PreviewConfig`.

### Нова вкладка

1. `ViewModels/Tabs/XxxViewModel.cs` (`public partial class`).
2. `Views/Tabs/XxxView.axaml(.cs)` з `x:DataType`.
3. Реєстрація ViewModel у DI.
4. `TabItem` у `MainWindow.axaml`.
5. За потреби — `DesignData`.

### Новий ендпоінт API

1. Метод в `ITtsApiClient`.
2. Реалізація в `TtsApiClient` (використовувати `SendAsync`, щоб помилки були однорідні).
3. Опис у `Doc/API.md`.
4. Фейк у `DesignData`.

---

## 9. Платформні відмінності

| Аспект | Linux (Wayland + PipeWire) | Windows 11 |
|---|---|---|
| Буфер обміну | `wl-paste` (`wl-clipboard`); X11 не підтримується | `TextCopy` (Win32 Clipboard) |
| Симуляція клавіатури | SharpHook `EventSimulator` (uinput); fallback `ydotool` | SharpHook `EventSimulator` |
| Аудіо | `libbass.so` (AUR `libbass`), `Bass.DefaultDevice` → PipeWire | `bass.dll` поруч із бінарником |
| Конфіг | `~/.config/UA_TTS_Client/config.json` | `%APPDATA%\UA_TTS_Client\config.json` |
| Перевірено | так, робоча машина | збірка; runtime не перевірявся |

Windows-специфічні шляхи не видаляти: `TextCopy` потрібен саме для Windows.

---

## 10. Де шукати далі

| Питання | Файл |
|---|---|
| Що робити далі й у якому порядку | `Doc/Curent_Work_Plan.md` |
| Точні API, пастки, середовище | `AGENTS.md` |
| Контракт сервера | `Doc/API.md` |
| Запуск і користування | `README.md` |
| Стандарти коду | `Doc/Промпт для ллм - Єдина кодова база arch-win11.md` |
