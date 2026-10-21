# Архітектура UA_TTS_Client

> **Статус станом на 2026-10-21:** реалізовано Кроки 0–9 плану (без юніт-тестів).
> Цей документ описує **фактичну** архітектуру реалізованого клієнта.
> Порядок роботи — `Doc/Final_Plan.md`; перевірені факти API та підводні камені — `AGENTS.md`;
> контракт сервера — `Doc/API.md`.

Клієнт — Avalonia 12 + .NET 10 (MVVM через CommunityToolkit.Mvvm) для локального
Python TTS-сервера (`Server_py/`, Flask + StyleTTS2). Цільові ОС: Linux
(EndeavourOS/Arch, Wayland + PipeWire) і Windows 11.

---

## 1. Шари та правила залежностей

| Шар | Каталог | Що містить | Може залежати від |
|---|---|---|---|
| **Domain** | `Core/` | enum-и, DTO, доменний виняток, інтерфейси сервісів | **ні від чого** (без `Avalonia.*`, `HttpClient`, `ManagedBass.*`, `SharpHook.*`) |
| **Infrastructure** | `Infrastructure/` | реалізації `Core`-інтерфейсів: HTTP, BASS, хук, JSON-конфіг | `Core`, зовнішні бібліотеки |
| **Presentation** | `ViewModels/` | MVVM-логіка, `partial class` + генератори | `Core` (+ `ILogger`) |
| **View** | `Views/` | XAML з compiled bindings (`x:DataType` обов'язковий) | `ViewModels` через `ViewLocator` |
| **Composition Root** | `App.axaml.cs` | **єдине** місце реєстрації DI, старт/завершення | усе разом |

Правила: DI лише через конструктори; помилки транспорту не доходять до
ViewModel (перетворюються на `TtsApiException`); аудіо відтворюється **з пам'яті**
(`byte[]`), тимчасові `.wav` не пишуться.

---

## 2. Вибір аудіо-рушія (історичний зріз)

Вимоги: відтворення з `byte[]` без запису на диск, пауза/гучність/автозупинка,
стабільна робота в PipeWire (Linux) і WASAPI (Windows).

| Критерій | `NetCoreAudio` | `Silk.NET.OpenAL` | **`ManagedBass` (обрано)** |
| --- | :--- | :--- | :--- |
| Відтворення з пам'яті | ❌ лише файли | ✅ повний контроль | ✅ `Bass.CreateStream(byte[], long, long, BassFlags)` |
| Кросплатформність | ⚠️ потребує `ffplay`/`aplay` | ✅ `libopenal.so` | ✅ `libbass.so` / `bass.dll` |
| PipeWire/PulseAudio | ⚠️ нестабільні форки процесів | ✅ добре | ✅ чудово (шар ALSA в PipeWire) |
| Пауза/гучність/позиція | ❌ | ⚠️ буферизація вручну | ✅ `ChannelPause`, `ChannelSetAttribute`, `ChannelSetSync` |
| Складність коду | низька, бідний функціонал | висока (C-like) | збалансована |

**Фактичний API ManagedBass 4.0.2** (класів `ManagedBass.Playback` /
`ManagedBass.Channel` **не існує**; лише статичний `ManagedBass.Bass`):

```csharp
Bass.Init(Bass.DefaultDevice, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero);
int handle = Bass.CreateStream(wavBytes, 0, wavBytes.Length, BassFlags.Default);
Bass.ChannelSetSync(handle, SyncFlags.End, 0, syncProc, IntPtr.Zero);
Bass.ChannelPlay(handle, false);
Bass.ChannelPause(handle);                                   // toggle
Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, v);
Bass.ChannelIsActive(handle);                                // → PlaybackState
Bass.ChannelStop(handle); Bass.StreamFree(handle); Bass.Free();
```

Перевірені підводні камені (повний перелік — в `AGENTS.md`):

- **`Bass.Init(0, …)` — це пристрій «No sound»**, а не пристрій за замовчуванням.
  Завжди `Bass.DefaultDevice` (-1) → потік з'являється в PipeWire
  (`pactl list sink-inputs`). Саме через `0` звук був відсутній.
- `SyncProcedure(int syncHandle, int channel, int data, IntPtr user)` — **перший
  параметр це дескриптор синхронізації, канал — другий**; порівнювати треба з `channel`.
- Прапорця `BassFlags.Memory` немає — перегрузка `byte[]` робить усе сама, але
  **масив має жити весь час відтворення** (поле сервісу / `GCHandle`).
- NuGet-пакет не містить нативних бібліотек: на Linux потрібен `yay -S libbass`
  (`bass` з `extra` — це гра); на Windows — `bass.dll` біля бінарника.
- `ManagedBass.PlaybackState` ↔ доменний `Core.Enums.PlaybackState` конвертується
  в сервісі, `Core` від ManagedBass не залежить.

---

## 3. Фактичне дерево файлів

```text
UA_TTS_Client/
├── AGENTS.md                     ← перевірені факти для агентів/розробника
├── README.md                     ← запуск і користування
├── UA_TTS_Client.sln
├── App.axaml / App.axaml.cs      ← Composition Root (DI, старт, завершення)
├── Program.cs                    ← шаблон Avalonia (не змінюється)
├── ViewLocator.cs                ← .ViewModels → .Views, ViewModel → View (+ MainWindow)
├── Core/
│   ├── Enums/        PlaybackState.cs, ServerStatus.cs
│   ├── Exceptions/   TtsApiException.cs
│   ├── Models/       AppConfig.cs, TtsRequestDto.cs, VerbalizeRequestDto.cs,
│   │                 TextResponseDto.cs, ApiErrorResponse.cs, ServerHealth.cs
│   └── Interfaces/   ISettingsService.cs, ITtsApiClient.cs, IAudioPlayerService.cs,
│                     IClipboardAutomationService.cs, IGlobalHotkeyService.cs
├── Infrastructure/
│   ├── Api/          TtsApiClient.cs
│   ├── Audio/        BassAudioPlayerService.cs
│   ├── Automation/   SharpHookGlobalHotkeyService.cs, WaylandClipboardAutomationService.cs
│   └── Storage/      JsonSettingsService.cs
├── ViewModels/
│   ├── ViewModelBase.cs            ← SynchronizationContext.OnUiThread для подій BASS
│   ├── MainWindowViewModel.cs      ← статус сервера (15 с), вкладки, Alt+Q
│   ├── Tabs/       QuickTtsViewModel.cs, SettingsViewModel.cs,
│   │               ReaderViewModel.cs (заглушка), CharacterVoiceViewModel.cs (заглушка)
│   ├── Components/ PlaybackControlViewModel.cs
│   └── Design/     DesignData.cs   ← фіктивні сервіси лише для прев'ю в Rider
├── Views/
│   ├── MainWindow.axaml(.cs)
│   └── Tabs/       QuickTtsView, SettingsView, ReaderView, CharacterVoiceView (.axaml/.cs)
├── Server_py/                     ← Flask-сервер (запускати ТІЛЬКИ звідси)
└── Doc/                           ← план, API, архітектура, стандарти
```

---

## 4. Контракти (`Core/Interfaces`)

```csharp
public interface IAudioPlayerService
{
    PlaybackState State { get; }
    double Volume { get; }                                   // 0.0…1.0
    event EventHandler<PlaybackState>? StateChanged;
    Task PlayAsync(byte[] wavData, CancellationToken ct = default);
    void Pause(); void Resume(); void Stop();
    void SetVolume(double volume);
}

public interface ITtsApiClient
{
    Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken ct = default);
    Task<ServerHealth> GetHealthAsync(CancellationToken ct = default);   // 404 → fallback /api/voices
    Task<string> VerbalizeAsync(string text, CancellationToken ct = default);
    Task<byte[]> SynthesizeAsync(TtsRequestDto request, CancellationToken ct = default);
    Task<string> ParseFileAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}

public interface IClipboardAutomationService
{
    Task<string> CaptureSelectedTextAsync(CancellationToken ct = default);
}

public interface IGlobalHotkeyService
{
    event EventHandler? HotkeyPressed;
    void Start();
    Task StopAsync();
}

public interface ISettingsService
{
    Task<AppConfig> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppConfig config, CancellationToken ct = default);
}
```

Enum-и: `PlaybackState { Idle, Playing, Paused, Stopped, Error }`,
`ServerStatus { Unknown, Online, Offline, Error }`.
Моделі — `record` із `[JsonPropertyName]`, що повторюють імена полів сервера
(`text`, `voice`, `speed`, `volume`, `translate`). Помилка API —
`TtsApiException { int? StatusCode; bool IsConnectionError; }`.

---

## 5. Конфігурація (`ISettingsService` → `JsonSettingsService`)

- Шлях: `Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
  "UA_TTS_Client", "config.json")` → `~/.config/UA_TTS_Client/config.json` (Linux),
  `%APPDATA%\UA_TTS_Client\config.json` (Windows). Жорстко закодованих `\`/`/` немає.
- **Атомарний запис:** temp-файл у тому ж каталозі → `File.Move(temp, path, overwrite: true)`;
  прибирання temp-файлу в `finally`.
- Відсутній/пошкоджений файл → значення за замовчуванням (`AppConfig`), без винятку.
- Другий конструктор `JsonSettingsService(logger, filePath)` — ізявним шляхом
  (використовується тестами Кроку 9.1).
- `SemaphoreSlim(1,1)` серіалізує читання/запис.

Поля `AppConfig`: `ServerUrl` (default `http://127.0.0.1:5000`), `SelectedVoice`,
`Speed` (1.0), `Volume` (1.0), `Hotkey` (`Alt+Q`).

---

## 6. Composition Root (`App.axaml.cs`)

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(...).AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));

var startupConfig = LoadStartupConfig();          // синхронно ДО побудови контейнера
services.AddSingleton(startupConfig);             // адреса потрібна для HttpClient.BaseAddress

services.AddSingleton<ISettingsService, JsonSettingsService>();
services.AddSingleton<IAudioPlayerService, BassAudioPlayerService>();
services.AddSingleton<IClipboardAutomationService, WaylandClipboardAutomationService>();
services.AddSingleton<IGlobalHotkeyService, SharpHookGlobalHotkeyService>();
services.AddHttpClient<ITtsApiClient, TtsApiClient>(c =>
{
    c.BaseAddress = baseAddress;                  // із AppConfig, fallback на типовий
    c.Timeout = TimeSpan.FromMinutes(5);
});

services.AddSingleton<PlaybackControlViewModel>();
services.AddSingleton<SettingsViewModel>();
services.AddSingleton<QuickTtsViewModel>();
services.AddSingleton<MainWindowViewModel>();

var provider = services.BuildServiceProvider();
var window = new MainWindow { DataContext = provider.GetRequiredService<MainWindowViewModel>() };
window.Opened += async (_, _) => await viewModel.InitializeAsync();
desktop.Exit += (_, _) => { viewModel.Dispose(); provider.Dispose(); };
```

Ключові рішення:

- конфіг читається **синхронно** до `BuildServiceProvider` — інакше `BaseAddress`
  не встигне встановитися; **зміна адреси в UI застосовується після перезапуску**
  (написано прямо в UI);
- `desktop.Exit` звільняє ViewModel і контейнер (BASS, хук, `HttpClient`);
- `MainWindowViewModel.InitializeAsync` стартує з події `window.Opened`
  (перевірка сервера + `IGlobalHotkeyService.Start()`).

---

## 7. UI: ViewLocator, вкладки, прев'ю

- `ViewLocator` замінює `.ViewModels` → `.Views` і `ViewModel` → `View`; для
  `MainWindowViewModel` додатково пробує `Views.MainWindow` (головне вікно насправді
  створюється явно в `App.axaml.cs`, локатор відповідає лише за вміст вкладок).
- Кожен XAML має `x:DataType` (compiled bindings → помилка вже на збірці).
- **Design-time DataContext — у code-behind:**
  `if (Design.IsDesignMode) DataContext = DesignData.MainWindow;`
  (`Design.DataContext="{x:Static …}"` у XAML дає хибне попередження Rider-аналізатора).
- `MainWindow.axaml`: верхня панель (назва + `Ellipse.status-dot.online` +
  `ServerStatusText` + кнопка «Перевірити»), `TabControl` на 4 вкладки, нижня панель
  міні-плеєра (`Playback.*`). Стилі описані в `Window.Styles`.
- Умовні класи: `Classes.online="{Binding IsServerOnline}"`; видимість повідомлень —
  через `StringConverters.IsNotNullOrEmpty`.
- `MainWindowViewModel`: `PeriodicTimer` (перевірка `GET /api/health` кожні 15 с),
  підписка на `HotkeyPressed` (Alt+Q → захоплення виділеного → текст у Quick TTS).
- `SettingsViewModel` — джерело істини для голосу/швидкості; гучність
  синхронізується з `PlaybackControlViewModel` в обидва боки.

---

## 8. Специфіка Wayland (Linux)

Wayland ізолює вікна, тому «захопити виділення» працює лише через симуляцію:

1. **`Alt+Q`** → `SharpHookGlobalHotkeyService`
   (`TaskPoolGlobalHook(parallelismLevel: 1, UioHookProvider.Instance)`).
   Alt+Q **озброюється на натисканні й спрацьовує після відпускання обох клавіш** —
   інакше симульований `Ctrl+C` надсилається, поки фізичний Alt затиснутий, і активне
   вікно отримує `Ctrl+Alt+C`. Власні симуляції відсікаються через `IsEventSimulated`.
2. **Симуляція `Ctrl+C`** → `SharpHook.Simulation.EventSimulator` (uinput; користувач
   у групі `input`). `ydotool` у цій сесії події не доставляє — це fallback, не основний шлях.
3. **Читання буфера** → спершу `wl-paste` (Wayland), потім `TextCopy`/`xsel`
   (X11, може віддати застарілий буфер). Після `Ctrl+C` вміст **опитується до 900 мс**;
   якщо не змінився — повертається порожній рядок, а не старий текст.
4. **Обмеження терміналів:** там копіює `Ctrl+Shift+C`, а `Ctrl+C` — це SIGINT,
   тому із термінала текст копіюється вручну й вставляється в Quick TTS.

---

## 9. Обмеження та наступні кроки

- **Тести (Крок 9.1) не створені** — тестовий проєкт `UA_TTS_Client.Tests`
  (xUnit + FluentAssertions + NSubstitute) поки не існує.
- Вкладки **Reader** і **Character Voice** — заглушки.
- Зміна гарячої клавіші — лише довідкове поле в налаштуваннях.
- Конвертація аудіо (`POST /api/convert`) у клієнті не використовується.
- Зміна адреси сервера потребує перезапуску (обмеження `HttpClient.BaseAddress`).
- Windows-збірка не тестувалася (потрібна `bass.dll` біля бінарника).

Порядок подальшої роботи — `Doc/Final_Plan.md`.
