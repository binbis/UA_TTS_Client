### .NET 10.0 (C#, Єдина кодова база: Linux EndeavourOS/Arch + Windows 11):

Технічний стек:
- ОС цільові: Linux (EndeavourOS / Arch Linux, Wayland + PipeWire) та Windows 11 (WASAPI)
- Середовище розробки: JetBrains Rider
- Платформа: .NET 10.0 (C# 13+)
- UI фреймворк: Avalonia UI (XAML, Compiled Bindings)
- Архітектурний патерн: MVVM (`CommunityToolkit.Mvvm`)
- Робота зі звуком: `ManagedBass` (відтворення з оперативної пам'яті / Streams без збереження на диск)
- Глобальні хуки та автоматизація: `SharpHook` (uiohook); буфер обміну — `wl-paste` на Linux, `TextCopy` (Win32 Clipboard) на Windows
- HTTP/API клієнт: `Microsoft.Extensions.Http` (Typed `HttpClient` з `IHttpClientFactory`)
- DI-контейнер: `Microsoft.Extensions.DependencyInjection`
- Серіалізація: `System.Text.Json`
- Тестування: `xUnit`, `FluentAssertions`, `NSubstitute`

Контекст: [РОЗУМІННЯ ДЕ МИ ТА З ЧОГО СТАРТУЄМО, ПРЕДІСТОРІЯ]

Мета: [ГЛОБАЛЬНА МЕТА ОНОВЛЕННЯ ЧИ СТВОРЕННЯ ФІЧІ]

Завдання: [ОПИШИ ТУТ СВОЄ КОНКРЕТНЕ ЗАВДАННЯ ДЛЯ LLM]

Важливо зауважити:
- [ЯКІСЬ КОНКРЕТНІ УТОЧНЕННЯ АБО ОБМЕЖЕННЯ]
- У відповіді очікую лише змінені або новостворені файли/фрагменти коду, які треба замінити, без надлишкового дублювання всього файлу, якщо в цьому немає потреби.

При виконанні завдання суворо дотримуйся наступних інженерних та архітектурних стандартів:

1. Архітектурні підходи, Кросплатформність та Clean Code:

- Кросплатформні шляхи: Код ПОВИНЕН гарантовано працювати як на Linux, так і на Windows 11. Для конфігураційних файлів використовуй `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` (`~/.config/UA_TTS_Client` на Linux, `%APPDATA%\UA_TTS_Client` на Windows). Завжди використовуй `Path.Combine` або `Path.Join`, пам'ятай про чутливість Linux до регістру (case-sensitivity).
- Принципи проєктування: Дотримуйся SOLID, DRY, KISS та YAGNI. Відокремлюй чисті інтерфейси в `Core/Interfaces` від сторонніх бібліотек в `Infrastructure/`.
- Впровадження залежностей (DI): Всі сервіси та ViewModel реєструються в `App.axaml.cs` через `ServiceCollection`. Залежності передаються виключно через конструктори (Constructor Injection).
- Typed HttpClient: Для звернень до Python-сервера реєструй типізований клієнт `services.AddHttpClient<ITtsApiClient, TtsApiClient>(...)` зі збільшеним таймаутом (від 120 секунд) з огляду на генерацію важких аудіомоделей.
- Керування пам'яттю та звуком: Відтворення аудіо має відбуватися безпосередньо з потоку пам'яті (`byte[]` / `MemoryStream`) без обов'язкового збереження тимчасових `.wav` файлів на диск. Усі некеровані ресурси BASS мають коректно звільнятися (`BASS_StreamFree`) з реалізацією `IDisposable`.
- Специфіка Wayland та буфера обміну: При емуляції комбінації клавіш (`Ctrl+C` через uinput/SharpHook) для копіювання виділеного тексту обов'язково передбачай асинхронну затримку (`await Task.Delay(80..120)`), щоб композитор Wayland встиг оновити буфер обміну перед читанням через `wl-paste`. X11 не підтримується; на Windows використовується `TextCopy`.

2. Стандарти коду JetBrains Rider та сучасний C# (.NET 10):

- Стиль коду Rider: Використовуй файлові простори імен (`file-scoped namespaces`, наприклад: `namespace UA_TTS_Client.Core.Interfaces;`) для запобігання зайвої вкладеності відступів.
- Source Generators у Rider: При роботі з `CommunityToolkit.Mvvm` усі класи ViewModel ОБОВ'ЯЗКОВО оголошуй як `public partial class`, щоб Roslyn-генератори та аналізатор Rider коректно будували залежності без помилок інспекції.
- Compiled Bindings в Avalonia: У всіх XAML-файлах (`Views`, `UserControls`) обов'язково декларуй `x:DataType="vm:НазваViewModel"`. Це дозволяє Rider проводити повну компіляційну перевірку зв'язування даних та забезпечує інтелектуальне автодоповнення.
- Сучасні фічі C#: Використовуй primary constructors для впровадження залежностей у класах, collection expressions `[]`, pattern matching, record types для DTO моделей, вирази switch та `required` властивості, де це доречно.
- Надійність: У всьому коді має бути увімкнено `#nullable enable`. Жодних неконтрольованих `NullReferenceException`.
- Асинхронність: Усі операції I/O, виклики мережі та робота з аудіо повинні бути суто асинхронними (`async/await`) з передачею `CancellationToken`.
- Обробка помилок та логування: Використовуй безпечні Guard Clauses та структуроване логування через `ILogger<T>`.

3. Бібліотеки та Екосистема:

- UI: Avalonia UI (без Windows-специфічних WinForms/WPF залежностей).
- MVVM: `CommunityToolkit.Mvvm` (атрибути `[ObservableProperty]`, `[RelayCommand]`, наслідування від `ObservableObject`).
- Аудіо: `ManagedBass` (підтримка кросплатформних бібліотек `libbass.so` на Arch та `bass.dll` на Windows 11).
- Глобальні гарячі клавіші: `SharpHook` через фоновий сервіс (`TaskPoolGlobalHook`).
- Буфер обміну: `wl-paste` (`wl-clipboard`) на Linux Wayland; `TextCopy` — лише Windows.
- Серіалізація: `System.Text.Json` із налаштуваннями відступів та відповідністю регістру ключів сервера.

4. Документування та Коментарі:

- На початку кожного файлу додай короткий описовий блок у стилі JSDoc (`/** ... */`), який пояснює роль модуля в системі.
- Додай вичерпну XML-документацію (`/// <summary>`, `<param>`, `<returns>`) до всіх публічних інтерфейсів, класів та методів.
- Для реалізацій інтерфейсів використовуй тег `<inheritdoc />`.
- Мова документації та коментарів — виключно Українська.

5. Автоматизоване тестування (Autotests; у поточній ітерації 2 свідомо не створюються):

- Фреймворк (на майбутнє): xUnit + FluentAssertions + NSubstitute.
- Покриття: Модульні тести для бізнес-логіки сервісів, парсерів та ViewModels.
- Ізоляція: Усі зовнішні взаємодії (робота з диском, HTTP запити до Flask, виклики аудіоплеєра, буфер обміну) мають бути закриті інтерфейсами та замокані через NSubstitute.
- Патерн: чітка структура AAA (Arrange, Act, Assert), відокремлена порожніми рядками.
- Іменування: `НазваМетоду_ВхідніУмови_ОчікуванийРезультат`.
- Перевіряй як успішні сценарії, так і граничні випадки (null, відсутність з'єднання з бекендом, скасування через CancellationToken).

Згенеруй чистий, кросплатформний, протестований та готовий до підтримки код для JetBrains Rider.
