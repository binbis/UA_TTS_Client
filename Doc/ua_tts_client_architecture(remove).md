# Архітектура та Дизайн UA_TTS_Client (.NET 10 / Avalonia UI)

## 1. Загальний огляд та Архітектурні принципи

Застосунок будується на принципах **Clean Architecture** та **MVVM** (Model-View-ViewModel), адаптованих для десктопних додатків з використанням DI-контейнера `Microsoft.Extensions.DependencyInjection` і бібліотеки `CommunityToolkit.Mvvm`.

### Ключові принципи:
1. **Platform Independence (Кросплатформність):**
   - Усі шляхи формуються виключно через `Path.Combine` або `Path.Join`.
   - Конфігураційні та тимчасові файли зберігаються у системних директоріях згідно з конвенціями ОС (`Environment.SpecialFolder.ApplicationData` для Windows та `XDG_CONFIG_HOME` / `~/.config` для Linux).
   - Специфічні для ОС речі (імітація введення клавіатури, робота з буфером обміну, аудіо-рушій) виносяться за інтерфейси-абстракції з різними або гібридними реалізаціями.
2. **SOLID & Inversion of Control (IoC):**
   - Бізнес-логіка, робота з мережею (TTS Server API) та відтворення звуку ізольовані від інтерфейсу користувача (UI).
   - ViewModels не мають прямих залежностей від низькорівневих API, а працюють лише з інтерфейсами (`ITtsApiClient`, `IAudioPlayerService`, `IClipboardService` тощо).
3. **Модульність та масштабованість (Extensibility):**
   - Архітектура підтримує додавання нових джерел вхідного тексту: буфер обміну, файли (PDF/DOCX), субтитри YouTube (через Data API v3), локальний HTTP/WebSocket мікро-сервер для розширення браузера.

---

## 2. Структура директорій (Files-Tree)

```text
UA_TTS_Client/
├── App.axaml
├── App.axaml.cs                    # Налаштування DI-контейнера, запуск головного вікна
├── app.manifest                    # Маніфест для Windows (DPI awareness, UAC)
├── Program.cs                      # Точка входу Avalonia, AppBuilder
├── UA_TTS_Client.csproj            # Конфігурація проєкту та NuGet-залежностей
│
├── Assets/                         # Ресурси: іконки, стилі, шрифти
│   ├── avalonia-logo.ico
│   └── Icons/
│
├── Common/                         # Спільні утиліти, розширення та константи
│   ├── Constants/
│   │   └── AppConstants.cs
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs
│   └── Helpers/
│       ├── PathHelper.cs           # Кросплатформні шляхи (AppData/XDG)
│       └── TextChunker.cs          # Алгоритми розумного розбиття тексту
│
├── Models/                         # Доменні моделі (Entity/Value Objects)
│   ├── AudioChunk.cs               # Модель сегмента аудіо
│   ├── BookDocument.cs             # Модель завантаженого документа
│   ├── CharacterVoiceProfile.cs    # Профіль персонажа (для ігрової озвучки)
│   ├── Config/
│   │   ├── AppSettings.cs          # Загальні налаштування
│   │   ├── ServerSettings.cs       # URL, таймаути Flask-сервера
│   │   ├── PlaybackSettings.cs     # Швидкість, гучність, обраний голос
│   │   └── HotkeySettings.cs       # Комбінації клавіш
│   └── Enums/
│       ├── PlaybackState.cs        # Idle, Playing, Paused, Buffering
│       └── ExportAudioFormat.cs    # Wav, Mp3, Ogg
│
├── DTOs/                           # Data Transfer Objects для HTTP API (System.Text.Json)
│   ├── Requests/
│   │   ├── SynthesizeRequestDto.cs
│   │   └── VerbalizeRequestDto.cs
│   └── Responses/
│       ├── VerbalizeResponseDto.cs
│       └── ErrorResponseDto.cs
│
├── Services/                       # Шар бізнес-логіки та інтеграцій
│   ├── Abstractions/               # Інтерфейси (Контракти)
│   │   ├── ITtsApiClient.cs        # Взаємодія з Flask-сервером
│   │   ├── IAudioPlayerService.cs  # Кросплатформне відтворення аудіо (Play/Pause/Stop)
│   │   ├── IClipboardService.cs    # Читання/запис буфера обміну
│   │   ├── IInputSimulationService.cs # Імітація натискання клавіш (ydotool / SharpHook)
│   │   ├── IGlobalHotkeyService.cs # Глобальне перехоплення гарячих клавіш
│   │   ├── ISettingsStorageService.cs # Збереження та завантаження налаштувань (JSON)
│   │   ├── ITextExtractionService.cs  # Витягування тексту (PDF, DOCX, ODT, TXT)
│   │   └── IYouTubeSubtitlesService.cs # (Майбутній) Отримання субтитрів через API
│   │
│   ├── Implementations/            # Конкретні реалізації інтерфейсів
│   │   ├── TtsApiClient.cs         # Реалізація через HttpClient та IHttpClientFactory
│   │   ├── AudioPlayerService.cs   # Аудіоплеєр
│   │   ├── ClipboardService.cs     # Обгортка навколо TextCopy / wl-clipboard
│   │   ├── InputSimulationService.cs # SharpHook (Win) / ydotool CLI (Linux Wayland)
│   │   ├── GlobalHotkeyService.cs  # SharpHook EventSimulator / TaskPoolGlobalHook
│   │   ├── JsonSettingsStorageService.cs
│   │   └── ServerTextExtractionService.cs # Виклик ендпоінта /api/parse_file
│   │
│   └── WebBridge/                  # (Майбутнє розширення для браузера)
│       └── LocalExtensionListener.cs # Локальний HttpListener/WebSocket для прийому виділеного тексту
│
├── ViewModels/                     # Презентаційний шар (Mvvm Community Toolkit)
│   ├── ViewModelBase.cs            # Базовий клас (ObservableObject)
│   ├── MainWindowViewModel.cs      # Контейнер для вкладок, статус сервера, індикатори
│   ├── QuickSpeechViewModel.cs     # Вкладка 1: Повсякденна озвучка тексту з буфера
│   ├── BookReaderViewModel.cs      # Вкладка 2: Озвучування файлів, черга, прогрес, експорт
│   ├── GameDubbingViewModel.cs     # Вкладка 3: Персонажі ігор, репліки, пресети
│   └── SettingsViewModel.cs        # Вкладка 4: Повні налаштування застосунку
│
└── Views/                          # UI шар (XAML)
    ├── MainWindow.axaml
    ├── MainWindow.axaml.cs
    └── Tabs/
        ├── QuickSpeechView.axaml
        ├── QuickSpeechView.axaml.cs
        ├── BookReaderView.axaml
        ├── BookReaderView.axaml.cs
        ├── GameDubbingView.axaml
        ├── GameDubbingView.axaml.cs
        ├── SettingsView.axaml
        └── SettingsView.axaml.cs
```

---

## 3. Деталізація Шару Абстракцій (Сервіси)

### 3.1. Клієнт взаємодії з Flask API (`ITtsApiClient`)
Відповідає за спілкування з вашим `server.py`:
- `GetAvailableVoicesAsync(CancellationToken ct)` $\rightarrow$ `GET /api/voices`
- `SynthesizeAsync(SynthesizeRequestDto request, CancellationToken ct)` $\rightarrow$ `POST /api/synthesize` (повертає потік байтів `Stream` або `byte[]` для WAV)
- `VerbalizeAsync(string text, CancellationToken ct)` $\rightarrow$ `POST /api/verbalize`
- `ParseDocumentAsync(string filePath, CancellationToken ct)` $\rightarrow$ `POST /api/parse_file` (multipart/form-data)
- `ConvertAudioAsync(Stream wavStream, ExportAudioFormat format, CancellationToken ct)` $\rightarrow$ `POST /api/convert`
- `CheckHealthAsync(CancellationToken ct)` $\rightarrow$ перевірка доступності сервера

### 3.2. Кросплатформне відтворення звуку (`IAudioPlayerService`)
Avalonia не містить вбудованого аудіо-движка. Для Linux (PipeWire/PulseAudio) та Windows 11 потрібне стабільне кросплатформне рішення:
- **Варіанти реалізації:**
  1. Бібліотека `NetCoreAudio` (проста, відтворює тимчасові файли або потоки через системні бекенди).
  2. Бібліотека `ManagedBass` або `Silk.NET.OpenAL` (надає повний контроль над буферизацією сирих байтів у пам'яті без запису на диск).
- **Контракт інтерфейсу:**
  ```csharp
  public interface IAudioPlayerService : IDisposable
  {
      PlaybackState State { get; }
      event EventHandler<PlaybackState> StateChanged;
      event EventHandler PlaybackCompleted;
      
      Task PlayAsync(Stream audioStream, CancellationToken ct = default);
      Task PauseAsync();
      Task ResumeAsync();
      Task StopAsync();
      void SetVolume(float volume); // 0.0f - 1.0f (або до 2.0f)
  }
  ```

### 3.3. Кросплатформне захоплення та симуляція введення (Wayland vs Windows)
У Wayland клієнтські вікна не мають доступу до глобальних подій введення інших вікон через вимоги безпеки композитора.
- **Глобальні гарячі клавіші (`IGlobalHotkeyService`):**
  - Використовує бібліотеку `SharpHook` (`TaskPoolGlobalHook`). Оскільки ви вже додали свого користувача до групи `input` і надали доступ до `/dev/uinput`, `libuiohook` коректно зчитує системні події клавіатури безпосередньо з пристроїв введення ядра.
- **Імітація комбінацій клавіш (`IInputSimulationService`):**
  - Для швидкого копіювання виділеного тексту в інших програмах (імітація `Ctrl+C`):
    - **Windows:** Виклик `EventSimulator` з бібліотеки `SharpHook`.
    - **Linux (Wayland):** Якщо композитор блокує синтетичні події через звичайні хуки, сервіс прозоро перемикається на запуск демона `ydotoold` / команду `ydotool key 29:1 46:1 46:0 29:0` (Ctrl+C).
- **Отримання виділеного тексту (`IClipboardService`):**
  - Читання буфера здійснюється через `TextCopy`. На Linux як резервний надійний варіант для Wayland використовується прямий пайп до системної утиліти `wl-paste` (яка встановлена у вашій системі).

### 3.4. Розумне розбиття та обробка черги (`TextChunker` / `SpeechQueueManager`)
- У Python-сервері вже є `split_to_parts`, але надсилати весь 500-сторінковий документ одним запитом на синтез нераціонально.
- `SpeechQueueManager` бере розбитий на речення/фрази текст і синтезує його **пакетно (pipeline streaming)**:
  - Поки грає сегмент $N$, сегмент $N+1$ вже генерується сервером у фоновому потоці.
  - Це ліквідує паузи між реченнями та дозволяє миттєво почати прослуховування великої книги.

---

## 4. Організація ViewModel та Табів (MVVM)

Кожна вкладка — це повністю незалежний модуль (UserControl + ViewModel), який взаємодіє зі спільними сервісами через DI:

```
[ MainWindowViewModel ]
    ├── QuickSpeechViewModel  (Tab 1: Буфер обміну, Alt+Q, швидкий синтез)
    ├── BookReaderViewModel   (Tab 2: Відкриття PDF/DOCX, список глав, черга читання, експорт MP3)
    ├── GameDubbingViewModel  (Tab 3: Каталог реплік, зв'язка "Персонаж -> Голос .pt", експорт паків)
    └── SettingsViewModel     (Tab 4: IP сервера, гарячі клавіші, дефолтний голос, гучність)
```

### 1. `QuickSpeechViewModel` (Повсякденне використання):
- Слухає подію натискання гарячої клавіші (наприклад, `Alt+Q`).
- Алгоритм:
  1. Зберегти поточний вміст буфера (опціонально).
  2. Викликати `IInputSimulationService.SimulateCopyAsync()` (`Ctrl+C`).
  3. Почекати 50-100 мс для оновлення системного буфера.
  4. Зчитати текст через `IClipboardService`.
  5. Якщо текст не порожній — викликати синтез або поставити відтворення на паузу (toggle-режим).
  6. Передати аудіопотік у `IAudioPlayerService`.

### 2. `BookReaderViewModel` (Книги та файли):
- Кнопка "Відкрити файл": використовує Avalonia `StorageProvider` для кросплатформного вибору файлів (PDF, DOCX, ODT, TXT).
- Відправляє файл на серверний ендпоінт `/api/parse_file`.
- Відображає текст у зручному редакторі з розбиттям на параграфи.
- Підсвічує речення, яке озвучується в поточний момент.
- Кнопки "Зберегти WAV" та "Експорт MP3" (викликають `/api/convert`).

### 3. `GameDubbingViewModel` (Озвучка ігор / Персонажі):
- Модель даних: `GameProject` $\rightarrow$ Список `CharacterVoiceProfile` (ім'я персонажа, закріплений голос `.pt`, модифікатор швидкості/пітчу).
- Таблиця реплік: `[ID, Персонаж, Оригінальний текст, Переклад/Адаптація, Статус генерації]`.
- Пакетна генерація аудіофайлів з іменуванням під формати ігор (наприклад, `npc_merchant_01.wav`).

### 4. `SettingsViewModel` (Налаштування):
- URL сервера (наприклад, `http://127.0.0.1:5000`) з кнопкою "Перевірити з'єднання".
- Вибір голосу за замовчуванням (випадаючий список, що завантажується динамічно через `/api/voices`).
- Слайдери швидкості ($0.5$ – $2.0$) та гучності ($0.1$ – $2.0$).
- Чекбокси: "Вербалізація чисел", "Авто-переклад українською", "Розумні фрагменти".
- Конфігурація шорткатів.

---

## 5. Впровадження залежностей (DI Bootstrapping)

Конфігурація сервісів зосереджується у класі `App.axaml.cs`. Завдяки цьому жоден клас не створює залежності вручну.

### Зразок конфігурації контейнера:

```csharp
public static class ServiceConfiguration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // 1. HttpClient для роботи з Flask API
        services.AddHttpClient<ITtsApiClient, TtsApiClient>((serviceProvider, client) =>
        {
            var settingsService = serviceProvider.GetRequiredService<ISettingsStorageService>();
            var settings = settingsService.CurrentSettings;
            client.BaseAddress = new Uri(settings.Server.BaseUrl);
            client.Timeout = TimeSpan.FromMinutes(10); // Для важких файлів
        });

        // 2. Системні та апаратні сервіси (Singletons)
        services.AddSingleton<ISettingsStorageService, JsonSettingsStorageService>();
        services.AddSingleton<IAudioPlayerService, AudioPlayerService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        
        // 3. Платформозалежні сервіси введення
        services.AddSingleton<IInputSimulationService, InputSimulationService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();

        // 4. Логіка витягування тексту та черги
        services.AddTransient<ITextExtractionService, ServerTextExtractionService>();

        // 5. Реєстрація ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<QuickSpeechViewModel>();
        services.AddTransient<BookReaderViewModel>();
        services.AddTransient<GameDubbingViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
```

---

## 6. Стратегія кросплатформності (Linux Wayland & Windows 11)

| Функціонал | Linux (EndeavourOS / Wayland) | Windows 11 |
| :--- | :--- | :--- |
| **Збереження конфігів** | `~/.config/ua_tts_client/settings.json` | `%APPDATA%\UA_TTS_Client\settings.json` |
| **Глобальні шорткати** | `SharpHook` (через `/dev/uinput` та групу `input`) | `SharpHook` (Windows Low-Level Hooks) |
| **Симуляція Ctrl+C** | `ydotool key 29:1 46:1 46:0 29:0` (якщо Wayland блокує synthetic hooks) | `SharpHook.EventSimulator` |
| **Буфер обміну** | `TextCopy` (з резервом на команду `wl-paste`) | `TextCopy` (Win32 Clipboard API) |
| **Файлові роздільники** | `Path.Combine(...)` | `Path.Combine(...)` |
| **Вибір файлів у UI** | XDG Desktop Portals (через Avalonia `StorageProvider`) | Native Common Item Dialog (через Avalonia) |

---

## 7. Модуль розширення для браузера та YouTube API (Future-Proof Design)

Щоб легко під'єднати браузерне розширення для отримання субтитрів YouTube та їх озвучки, закладаються два архітектурні шлюзи:

1. **Локальний веб-міст (`LocalExtensionListener`):**
   - У фоні застосунок може слухати порт на локалхості (наприклад, `http://127.0.0.1:51234/inject-text`).
   - Розширення Chrome/Firefox перехоплює активні субтитри YouTube плеєра або виділений текст на веб-сторінці й відправляє легкий `POST`-запит.
   - Застосунок перехоплює подію і відправляє текст у `SpeechQueueManager` відповідної вкладки.
2. **Сервіс субтитрів (`IYouTubeSubtitlesService`):**
   - Інтерфейс із методом `GetTrackAsync(string videoId, string lang = "uk")`.
   - Завантажує таймкоди та текст субтитрів безпосередньо через YouTube Data API v3 або з відкритих доріжок.
   - Зберігає таймкоди, щоб `IAudioPlayerService` міг синхронізувати голос із відео.