# Покроковий Roadmap реалізації UA_TTS_Client

Цей документ визначає оптимальну черговість створення файлів і тестування компонентів застосунку в JetBrains Rider.

Стратегія побудови спирається на принцип **Inward Dependencies (Clean Architecture)**: ми рухаємося від ядра (Core), яке не залежить ні від чого, через інфраструктуру (Infrastructure) до бізнес-логіки представлення (ViewModels) і верстки (Views), завершуючи це збіркою в Composition Root (`App.axaml.cs`).

---

## 🗺 Огляд етапів розробки

```
[0. Залежності .csproj] 
       ↓
[1. Core: Енуми, DTO, Інтерфейси] 
       ↓
[2. Інфраструктура збереження та API (Settings + HTTP Client)] 
       ↓
[3. Інфраструктура аудіо та системи (BASS + SharpHook + Clipboard)] 
       ↓
[4. ViewModels: Бізнес-логіка вкладок і плеєра] 
       ↓
[5. XAML Views: Розмітка вікон та елементів керування] 
       ↓
[6. Точка входу та DI (App.axaml.cs / Program.cs)] 
       ↓
[7. Модульні тести та перевірка на Linux/Windows]
```

---

## Етап 0: Конфігурація проєкту та NuGet пакети

**Мета:** Налаштувати `.csproj`, щоб Rider мав усі генератори коду та необхідні бібліотеки перед написанням першого C#-файлу.

### Файли для редагування:
1. `UA_TTS_Client.csproj`

### Необхідні пакети (PackageReferences):
* `Avalonia` (актуальна версія 11.x)
* `Avalonia.Themes.Fluent`
* `Avalonia.Fonts.Inter`
* `CommunityToolkit.Mvvm` (Source Generators)
* `ManagedBass` (робота з аудіо)
* `SharpHook` (глобальні хуки)
* `TextCopy` (робота з буфером під Wayland/Win32)
* `Microsoft.Extensions.DependencyInjection`
* `Microsoft.Extensions.Http`
* `System.Text.Json`

**Контрольна точка (Check):** Проєкт успішно збирається в Rider (`Ctrl+F9` або `Build Solution`) без помилок відновлення пакетів.

---

## Етап 1: Фундамент (Core Layer)

**Мета:** Створити контракти та моделі даних. Цей шар не має зовнішніх залежностей від GUI чи платформи і компілюється найшвидше.

### 1.1. Переліки (`Core/Enums/`):
* `PlaybackState.cs` (`Playing`, `Paused`, `Stopped`, `Buffering`)
* `ExportFormat.cs` (`Wav`, `Mp3`, `Ogg`)

### 1.2. Моделі та DTO (`Core/Models/`):
* `AppConfig.cs` (URL сервера, дефолтний голос, гаряча клавіша, гучність, швидкість)
* `TtsRequestDto.cs` (текст, назва голосу, швидкість мовлення)
* `VerbalizeRequestDto.cs` (текст для нормалізації чисел/абревіатур)
* `CharacterVoicePreset.cs` (ідентифікатор, назва пресету, параметри)

### 1.3. Контракти інтерфейсів (`Core/Interfaces/`):
* `ISettingsService.cs` (завантаження/збереження конфігурації)
* `ITtsApiClient.cs` (виклики до Flask: Ping, GetVoices, Synthesize)
* `IAudioPlayerService.cs` (керування потоковим звуком через BASS)
* `IClipboardAutomationService.cs` (захоплення виділеного тексту через буфер)
* `IGlobalHotkeyService.cs` (реєстрація глобальної комбінації `Alt+Q`)
* `IDocumentParserService.cs` (витягування тексту з файлів)

**Контрольна точка (Check):** Усі інтерфейси задокументовані XML-коментарями солов'їною мовою; жодних циклических залежностей.

---

## Етап 2: Збереження налаштувань та HTTP API

**Мета:** Реалізувати збереження конфігурації на диску та зв'язок із бекендом на Python.

### Файли для реалізації:
1. `Infrastructure/Storage/JsonSettingsService.cs`:
   * Кросплатформне розміщення у `Environment.SpecialFolder.ApplicationData` (`~/.config/UA_TTS_Client` на Linux, `%APPDATA%\UA_TTS_Client` на Windows).
   * Асинхронне читання/запис через `System.Text.Json`.
2. `Infrastructure/Api/TtsApiClient.cs`:
   * Робота через типізований `HttpClient`.
   * Перевірка доступності сервера (`/api/ping` або `/health`).
   * Отримання списку доступних `.pt` голосів (`/api/voices`).
   * Відправка запиту на синтез (`/api/synthesize`) та отримання `byte[]` звуку.

**Контрольна точка (Check):** Можна написати перший консольний або модульний тест, що перевіряє пінг локального Python-сервера (`python server.py`).

---

## Етап 3: Аудіо-рушій та Системна автоматизація

**Мета:** Додати можливість відтворювати звук з RAM-пам'яті та взаємодіяти з віконною системою (Wayland/Windows).

### Файли для реалізації:
1. `Infrastructure/Audio/BassAudioPlayerService.cs`:
   * Ініціалізація `Bass.Init()`.
   * Відтворення з `byte[]` через `GCHandle` та `Bass.CreateStream` з прапорцем вивільнення пам'яті.
   * Підписка на `BASS_ChannelSetSync` для відстеження кінця треку (`PlaybackEnded`).
   * Методи `Pause()`, `Resume()`, `Stop()`, зміна гучності.
2. `Infrastructure/Automation/WaylandClipboardAutomationService.cs`:
   * Використання `TextCopy.ClipboardService`.
   * Асинхронна затримка (`await Task.Delay(100)`) для коректної синхронізації з `wl-clipboard` / Wayland compositor.
3. `Infrastructure/Automation/SharpHookGlobalHotkeyService.cs`:
   * Підписка на `TaskPoolGlobalHook`.
   * Фільтрація комбінації `Alt + Q` та виклик події перехоплення.

**Контрольна точка (Check):** Перевірити завантаження `libbass.so` на Arch (`yay -S bass`) або `bass.dll` на Windows під час виклику ініціалізації.

---

## Етап 4: Презентаційна логіка (ViewModels)

**Мета:** Побудувати стан екранів за шаблоном MVVM за допомогою Source Generators.

### Файли для реалізації:
1. `ViewModels/ViewModelBase.cs` (базовий клас, наслідуваний від `ObservableObject`).
2. `ViewModels/Components/PlaybackControlViewModel.cs`:
   * Кнопки Play/Pause/Stop, слайдер гучності, поточний стан відтворення.
3. `ViewModels/Tabs/SettingsViewModel.cs`:
   * URL сервера, вибір голосу, кнопка тестування з'єднання, вибір швидкості мовлення.
4. `ViewModels/Tabs/QuickTtsViewModel.cs`:
   * Текстове поле для введення/перехоплення тексту, кнопки «Озвучити», «Очистити», статус перехоплення з Alt+Q.
5. `ViewModels/Tabs/ReaderViewModel.cs`:
   * Відкриття файлу книги, розбиття тексту на абзаци, перехід між реченнями, збереження прогресу.
6. `ViewModels/Tabs/CharacterVoiceViewModel.cs`:
   * Вибір пресетів персонажів, налаштування емоційного забарвлення.
7. `ViewModels/MainWindowViewModel.cs`:
   * Агрегація вкладок (Tabs), відстеження загального статусу бекенду (Online/Offline) з фоновим таймером перевірки.

**Контрольна точка (Check):** Перевірити в Rider, що класи оголошені як `public partial class` і генератор CommunityToolkit не видає помилок компіляції.

---

## Етап 5: Графічний інтерфейс (Avalonia Views / XAML)

**Мета:** Зверстати інтерфейс зі строгим зв'язуванням (`CompiledBinding`).

### Файли для реалізації:
1. `Views/Tabs/SettingsView.axaml` (+ `.cs`)
2. `Views/Tabs/QuickTtsView.axaml` (+ `.cs`)
3. `Views/Tabs/ReaderView.axaml` (+ `.cs`)
4. `Views/Tabs/CharacterVoiceView.axaml` (+ `.cs`)
5. `Views/MainWindow.axaml` (+ `.cs`):
   * Вкладки `TabControl`.
   * Верхній індикатор статусу підключення до сервера (зелена/червона крапка).
   * Нижня закріплена панель плеєра (`PlaybackControl`).

**Контрольна точка (Check):** Перевірити в кожному XAML наявність `x:DataType="vm:..."` для роботи автодоповнення Rider і валідації властивостей під час білду.

---

## Етап 6: Точка входу та реєстрація сервісів (DI Root)

**Мета:** Зібрати всі елементи системи воєдино через Dependency Injection.

### Файли для оновлення:
1. `ViewLocator.cs`:
   * Мапінг між ViewModels та відповідними UserControl/Window.
2. `App.axaml` (+ `.cs`):
   * Реєстрація `ServiceCollection`.
   * Реєстрація типізованого `HttpClient` для `TtsApiClient` з таймаутом 120+ секунд.
   * Реєстрація сервісів як `Singleton` (плеєр, налаштування, хуки).
   * Реєстрація ViewModels.
   * Ініціалізація `MainWindow` з передачею `DataContext`.
3. `Program.cs`:
   * Налаштування платформи Avalonia (використання шрифтів, Desktop lifetime).

**Контрольна точка (Check):** Застосунок запускається з Rider (`Ctrl+F5`), з'являється головне вікно, підвантажуються налаштування з конфіг-файлу, відображається статус зв'язку з сервером.

---

## Етап 7: Автоматизоване тестування та фінальна валідація

**Мета:** Забезпечити стабільність бізнес-логіки та захист від регресій.

### Створення тестового проєкту `UA_TTS_Client.Tests`:
1. `TtsApiClientTests.cs`:
   * Тестування обробки помилок за відсутності мережі (Offline mode).
   * Десеріалізація відповідей сервера.
2. `JsonSettingsServiceTests.cs`:
   * Збереження та відновлення значень за замовчуванням.
3. `QuickTtsViewModelTests.cs`:
   * Перевірка поведінки команди синтезу при порожньому тексті (Guard Clause).
   * Мокування `IAudioPlayerService` та `ITtsApiClient` через NSubstitute.

---

## Резюме: З чого починаємо прямо зараз?

Прямо зараз правильний перший крок — це **Крок 0 і Крок 1**:
1. Перевірити та оновити файл проєкту `UA_TTS_Client.csproj` (додати відсутні пакети для DI, BASS, SharpHook, TextCopy).
2. Створити директорії `Core/Enums`, `Core/Models`, `Core/Interfaces` та наповнити їх базовими контрактами.