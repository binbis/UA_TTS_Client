# Архітектурна специфікація та дизайн UA_TTS_Client

Цей документ описує архітектуру оновленого кросплатформного клієнта **UA_TTS_Client** на базі **.NET 10**, **Avalonia UI** та **CommunityToolkit.Mvvm** для роботи під **Linux (Arch/EndeavourOS, Wayland/PipeWire)** та **Windows 11**.

---

## 1. Аналіз аудіо-рушія (`IAudioPlayerService`)

Оскільки Python-сервер повертає `audio/wav` у бінарному вигляді (через endpoint `/api/synthesize`), ключовою вимогою до плеєра є:
1. Можливість відтворення з потоку пам'яті (`Stream` / `byte[]`) без примусового збереження на диск для кожного запиту.
2. Підтримка регулювання швидкості, паузи/продовження та зупинки в реальному часі.
3. Стабільна робота під **Linux PipeWire / PulseAudio** та **Windows WASAPI**.

### Порівняння кандидатів:

| Критерій | `NetCoreAudio` | `Silk.NET.OpenAL` | `ManagedBass` (BASS Audio) |
| :--- | :--- | :--- | :--- |
| **Відтворення з пам'яті (`Stream` / `byte[]`)** | ❌ Тільки локальні файли (вимагає запису на диск) | ✅ Повний низькорівневий контроль | ✅ Нативна підтримка `BASS_StreamCreateFile` з пам'яті (`IntPtr` / `GCHandle`) |
| **Кросплатформність** | ⚠️ Залежить від наявності `ffplay` / `aplay` у системі | ✅ Працює через `libopenal.so` / `openal32.dll` | ✅ Нативні бібліотеки `libbass.so` (Linux) та `bass.dll` (Win) |
| **PipeWire / PulseAudio сумісність** | ⚠️ Нестабільні форки системних процесів | ✅ Відмінна через системний OpenAL бекенд | ✅ Чудова через PulseAudio/ALSA шар PipeWire |
| **Контроль позиції / Пауза / Гучність** | ❌ Мінімальний (лише Play/Stop) | ⚠️ Потрібно писати буферизацію вручну | ✅ Повний набір (`ChannelSetAttribute`, `ChannelPause`, `ChannelStop`) |
| **Складність коду** | Низька, але функціонал бідний | Висока (C-like низькорівневий OpenAL API) | Збалансована (лаконічний C# wrapper) |

### Рекомендація:
**ManagedBass** є найкращим вибором для продакшн-якості. Він дозволяє:
- Відтворювати `byte[]` або `MemoryStream` без тимчасових `.wav` файлів.
- Безшовно керувати гучністю, паузою та завершенням треку.
- На Linux автоматично зв'язується з `libbass.so` (потрібно підкласти біля бінарника або поставити з AUR: `yay -S bass`).
- *Альтернатива (простіший старт без сторонніх native C-бібліотек)*: Якщо не хочеться тягнути `libbass.so`, можна використати порт **SoundFlow** або **LibVLCSharp**. Проте `ManagedBass` перевірений часом саме для TTS-клієнтів.

---

## 2. Структура директорій проєкту (Tree)

Проєкт розділено на логічні шари за принципами Clean Architecture всередині одного рішення:

```text
UA_TTS_Client/
├── App.axaml
├── App.axaml.cs
├── app.manifest
├── Program.cs
├── ViewLocator.cs
├── UA_TTS_Client.csproj
├── Assets/
│   ├── avalonia-logo.ico
│   └── Icons/
├── Core/
│   ├── Enums/
│   │   ├── PlaybackState.cs              # Playing, Paused, Stopped, Buffering
│   │   └── ExportFormat.cs               # Wav, Mp3, Ogg
│   ├── Interfaces/
│   │   ├── IAudioPlayerService.cs        # Керування звуком (Play, Pause, Stop, Seek)
│   │   ├── ITtsApiClient.cs             # Взаємодія з Flask-сервером
│   │   ├── IGlobalHotkeyService.cs       # SharpHook обробник Alt+Q
│   │   ├── IClipboardAutomationService.cs# Симуляція Ctrl+C та витягування тексту
│   │   ├── IDocumentParserService.cs     # Парсинг PDF/Word/ODT/TXT
│   │   └── ISettingsService.cs           # Збереження/завантаження конфігурації
│   └── Models/
│       ├── AppConfig.cs                  # Модель налаштувань (URL, Default Voice, Hotkey)
│       ├── TtsRequestDto.cs              # Модель запиту на синтез
│       ├── VerbalizeRequestDto.cs
│       └── CharacterVoicePreset.cs       # Для майбутньої вкладки ігрових персонажів
├── Infrastructure/
│   ├── Audio/
│   │   └── BassAudioPlayerService.cs     # Реалізація через ManagedBass
│   ├── Api/
│   │   └── TtsApiClient.cs               # HttpClient + перевірка стану сервера
│   ├── Automation/
│   │   ├── SharpHookGlobalHotkeyService.cs # Обробка Alt+Q
│   │   └── WaylandClipboardAutomationService.cs # Робота через TextCopy / SharpHook uinput
│   ├── Parsing/
│   │   └── DocumentParserService.cs      # Клієнтська або серверна обробка документів
│   └── Storage/
│       └── JsonSettingsService.cs        # Збереження у ~/.config/UA_TTS_Client/config.json
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs            # Хост для вкладок та глобального статусу сервера
│   ├── Tabs/
│   │   ├── QuickTtsViewModel.cs          # Alt+Q, швидке читання буфера
│   │   ├── ReaderViewModel.cs            # Озвучування файлів, книг, навігація по абзацах
│   │   ├── CharacterVoiceViewModel.cs    # Концепт ігрової озвучки
│   │   └── SettingsViewModel.cs          # Слайдери, чекбокси, вибір голосу, тест підключення
│   └── Components/
│       └── PlaybackControlViewModel.cs   # Перевикористовуваний блок Play/Pause/Volume
└── Views/
    ├── MainWindow.axaml
    ├── MainWindow.axaml.cs
    └── Tabs/
        ├── QuickTtsView.axaml
        ├── QuickTtsView.axaml.cs
        ├── ReaderView.axaml
        ├── ReaderView.axaml.cs
        ├── CharacterVoiceView.axaml
        ├── CharacterVoiceView.axaml.cs
        ├── SettingsView.axaml
        └── SettingsView.axaml.cs
```

---

## 3. Ключові контракти сервісів (Core/Interfaces)

### 3.1. Керування аудіо: `IAudioPlayerService.cs`
Повністю абстрагує бізнес-логіку від того, яка саме аудіо-бібліотека грає звук.

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Interfaces;

public interface IAudioPlayerService : IDisposable
{
    PlaybackState State { get; }
    float Volume { get; set; } // 0.0f до 1.0f
    
    event EventHandler<PlaybackState>? StateChanged;
    event EventHandler? PlaybackEnded;

    Task PlayAsync(Stream audioStream, CancellationToken cancellationToken = default);
    Task PlayAsync(byte[] audioData, CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
    void Stop();
}
```

### 3.2. Взаємодія з Python Backend: `ITtsApiClient.cs`
Відповідає за виклики Flask API.

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

public interface ITtsApiClient
{
    Task<bool> PingServerAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default);
    Task<string> VerbalizeTextAsync(string rawText, CancellationToken cancellationToken = default);
    Task<byte[]> SynthesizeAsync(TtsRequestDto request, CancellationToken cancellationToken = default);
    Task<byte[]> ConvertAudioAsync(byte[] wavData, ExportFormat format, CancellationToken cancellationToken = default);
    Task<string> ParseDocumentAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
```

### 3.3. Кросплатформна автоматизація буфера та клавіатури: `IClipboardAutomationService.cs`
Під Wayland копіювання з активного вікна вимагає симуляції комбінації `Ctrl+C` через віртуальний uinput (через SharpHook), короткої паузи (50-100ms) для оновлення системного буфера, а потім читання через `TextCopy` (що спирається на `wl-clipboard` у системі).

```csharp
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

public interface IClipboardAutomationService
{
    Task<string?> CaptureSelectedTextAsync();
    Task<string?> GetClipboardTextAsync();
    Task SetClipboardTextAsync(string text);
}
```

---

## 4. Конфігурація та кросплатформні шляхи (`ISettingsService`)

Критично не використовувати жорстко закодовані шляхи з `\` чи `/`.

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Infrastructure.Storage;

public class JsonSettingsService : ISettingsService
{
    private readonly string _configFilePath;
    private AppConfig _currentConfig = new();

    public JsonSettingsService()
    {
        // ~/.config/UA_TTS_Client на Linux
        // %APPDATA%\UA_TTS_Client на Windows
        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UA_TTS_Client"
        );

        if (!Directory.Exists(appDataDir))
        {
            Directory.CreateDirectory(appDataDir);
        }

        _configFilePath = Path.Combine(appDataDir, "config.json");
    }

    public async Task<AppConfig> LoadAsync()
    {
        if (!File.Exists(_configFilePath))
        {
            _currentConfig = new AppConfig();
            await SaveAsync(_currentConfig);
            return _currentConfig;
        }

        try
        {
            await using var stream = File.OpenRead(_configFilePath);
            _currentConfig = await JsonSerializer.DeserializeAsync<AppConfig>(stream) ?? new AppConfig();
        }
        catch
        {
            _currentConfig = new AppConfig();
        }

        return _currentConfig;
    }

    public async Task SaveAsync(AppConfig config)
    {
        _currentConfig = config;
        var options = new JsonSerializerOptions { WriteIndented = true };
        await using var stream = File.Create(_configFilePath);
        await JsonSerializer.SerializeAsync(stream, config, options);
    }
}
```

---

## 5. Впровадження залежностей (DI) та ініціалізація (`App.axaml.cs`)

Для коректної роботи Avalonia з `Microsoft.Extensions.DependencyInjection`, реєстрація сервісів та ViewModel виконується в єдиному місці:

```csharp
using System;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Infrastructure.Api;
using UA_TTS_Client.Infrastructure.Audio;
using UA_TTS_Client.Infrastructure.Automation;
using UA_TTS_Client.Infrastructure.Storage;
using UA_TTS_Client.ViewModels;
using UA_TTS_Client.ViewModels.Tabs;
using UA_TTS_Client.Views;

namespace UA_TTS_Client;

public partial class App : Application
{
    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        ConfigureServices(collection);
        Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. HttpClient для Python Server
        services.AddHttpClient<ITtsApiClient, TtsApiClient>(client =>
        {
            client.BaseAddress = new Uri("http://127.0.0.1:5000");
            client.Timeout = TimeSpan.FromSeconds(120); // Великі файли можуть конвертуватися довго
        });

        // 2. Інфраструктурні Singleton-сервіси
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IAudioPlayerService, BassAudioPlayerService>();
        services.AddSingleton<IClipboardAutomationService, WaylandClipboardAutomationService>();
        services.AddSingleton<IGlobalHotkeyService, SharpHookGlobalHotkeyService>();

        // 3. ViewModels (Вкладки як Singleton або Transient залежно від збереження стану)
        services.AddSingleton<QuickTtsViewModel>();
        services.AddSingleton<ReaderViewModel>();
        services.AddSingleton<CharacterVoiceViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // 4. Головна ViewModel
        services.AddSingleton<MainWindowViewModel>();
    }
}
```

---

## 6. Архітектура вкладок (Tabs) та зв'язок з UI

### Головне вікно (`MainWindow.axaml`):
Використовує `TabControl`, де кожна вкладка зв'язується зі своєю ViewModel через `ViewLocator`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:UA_TTS_Client.ViewModels"
        x:Class="UA_TTS_Client.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="UA TTS Client"
        Width="950" Height="680"
        WindowStartupLocation="CenterScreen">

    <Grid RowDefinitions="Auto, *, Auto">
        <!-- Верхня панель статусу сервера -->
        <Border Grid.Row="0" Background="{DynamicResource SystemChromeLowColor}" Padding="10,6">
            <StackPanel Orientation="Horizontal" Spacing="8" VerticalAlignment="Center">
                <Ellipse Width="10" Height="10" Fill="{Binding ServerStatusColor}" />
                <TextBlock Text="{Binding ServerStatusText}" VerticalAlignment="Center" FontWeight="SemiBold" />
                <Button Command="{Binding ReconnectServerCommand}" Content="🔄 Перевірити з'єднання" Margin="10,0,0,0" />
            </StackPanel>
        </Border>

        <!-- Вкладки інтерфейсу -->
        <TabControl Grid.Row="1" Margin="8">
            <TabItem Header="⚡ Швидке озвучення (Alt+Q)">
                <ContentControl Content="{Binding QuickTtsVm}" />
            </TabItem>
            <TabItem Header="📖 Читач книг і документів">
                <ContentControl Content="{Binding ReaderVm}" />
            </TabItem>
            <TabItem Header="🎮 Голоси персонажів (WIP)">
                <ContentControl Content="{Binding CharacterVoiceVm}" />
            </TabItem>
            <TabItem Header="⚙️ Налаштування">
                <ContentControl Content="{Binding SettingsVm}" />
            </TabItem>
        </TabControl>

        <!-- Нижня закріплена панель відтворення (Mini-Player) -->
        <Border Grid.Row="2" Background="{DynamicResource SystemChromeMediumColor}" Padding="12,8">
            <!-- Кнопки Play, Pause, Stop, регулятор гучності -->
        </Border>
    </Grid>
</Window>
```

---

## 7. Специфіка роботи під Wayland (Linux)

Оскільки Wayland навмисно ізолює вікна одне від одного:
1. **SharpHook (`libuiohook`)**:
   - Перехоплення глобальних клавіш (`Alt+Q`) працює у фоновому потоці через `TaskPoolGlobalHook`.
   - Для емуляції натискань `Ctrl+C` користувач повинен мати доступ до `/dev/uinput` (додавання до групи `input` виконано).
2. **TextCopy / Буфер обміну**:
   - `TextCopy` автоматично викликає системні утиліти `wl-paste` / `wl-copy`. Наявність `wl-clipboard` у системі покриває це на 100%.
3. **Затримки перед читанням буфера**:
   - Емуляція натискання клавіш — асинхронна подія. Між відправкою події `Key.C` та читанням з буфера необхідно витримати затримку близько `80-120 мс` (`await Task.Delay(100)`), інакше буде прочитано старий вміст буфера.

---

## 8. План наступних дій з реалізації

1. **Крок 1**: Встановити NuGet-пакет аудіо-движка (`ManagedBass`).
2. **Крок 2**: Створити DTO та інтерфейси в каталозі `Core/`.
3. **Крок 3**: Реалізувати `TtsApiClient` для зв'язку з `server.py` та протестувати виклики `/api/voices` і `/api/synthesize`.
4. **Крок 4**: Реалізувати `BassAudioPlayerService` для програвання звукових масивів `byte[]`.
5. **Крок 5**: Реалізувати `SharpHookGlobalHotkeyService` для реакції на `Alt+Q`.
6. **Крок 6**: Зверстати View та ViewModels для кожної з 4-х вкладок.