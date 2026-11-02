### .NET 10.0 (C#, single codebase: Linux EndeavourOS/Arch + Windows 11):

Technology stack:
- Target OS: Linux (EndeavourOS / Arch Linux, Wayland + PipeWire) and Windows 11 (WASAPI)
- Development environment: JetBrains Rider
- Platform: .NET 10.0 (C# 13+)
- UI framework: Avalonia UI (XAML, Compiled Bindings)
- Architectural pattern: MVVM (`CommunityToolkit.Mvvm`)
- Audio: `ManagedBass` (playback from RAM / streams without saving to disk)
- Global hooks and automation: `SharpHook` (uiohook); clipboard — `wl-paste` on Linux, `TextCopy` (Win32 Clipboard) on Windows
- HTTP/API client: `Microsoft.Extensions.Http` (typed `HttpClient` with `IHttpClientFactory`)
- DI container: `Microsoft.Extensions.DependencyInjection`
- Serialization: `System.Text.Json`
- Testing: `xUnit`, `FluentAssertions`, `NSubstitute`

Context: [WHERE WE ARE AND WHERE WE START FROM, BACKGROUND]

Goal: [GLOBAL GOAL OF THE UPDATE OR FEATURE]

Task: [DESCRIBE YOUR SPECIFIC TASK FOR THE LLM HERE]

Important notes:
- [SOME SPECIFIC CLARIFICATIONS OR CONSTRAINTS]
- In the answer I expect only the changed or newly created files/code fragments that must be replaced, without unnecessary duplication of entire files, unless there is a need for it.

When performing the task, strictly follow these engineering and architectural standards:

1. Architectural approaches, cross-platform support and Clean Code:

- Cross-platform paths: the code MUST work reliably on both Linux and Windows 11. For configuration files use `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` (`~/.config/UA_TTS_Client` on Linux, `%APPDATA%\UA_TTS_Client` on Windows). Always use `Path.Combine` or `Path.Join`, and remember that Linux is case-sensitive.
- Design principles: follow SOLID, DRY, KISS and YAGNI. Keep clean interfaces in `Core/Interfaces`, separate from third-party libraries in `Infrastructure/`.
- Dependency injection (DI): all services and ViewModels are registered in `App.axaml.cs` via `ServiceCollection`. Dependencies are passed exclusively through constructors (Constructor Injection).
- Typed HttpClient: to call the Python server, register a typed client `services.AddHttpClient<ITtsApiClient, TtsApiClient>(...)` with an increased timeout (at least 120 seconds), given the heavy audio model generation.
- Memory and sound management: audio playback must happen directly from a memory stream (`byte[]` / `MemoryStream`), without mandatory saving of temporary `.wav` files to disk. All unmanaged BASS resources must be released correctly (`BASS_StreamFree`), implementing `IDisposable`.
- Wayland and clipboard specifics: when simulating a key combination (`Ctrl+C` via uinput/SharpHook) to copy selected text, always account for an asynchronous delay (`await Task.Delay(80..120)`), so that the Wayland compositor has time to update the clipboard before it is read via `wl-paste`. X11 is not supported; on Windows `TextCopy` is used.

2. JetBrains Rider code standards and modern C# (.NET 10):

- Rider code style: use file-scoped namespaces (e.g. `namespace UA_TTS_Client.Core.Interfaces;`) to avoid unnecessary indentation nesting.
- Rider source generators: when working with `CommunityToolkit.Mvvm`, ALL ViewModel classes MUST be declared as `public partial class`, so that the Roslyn generators and the Rider analyzer build dependencies correctly without inspection errors.
- Compiled Bindings in Avalonia: in ALL XAML files (`Views`, `UserControls`) you MUST declare `x:DataType="vm:ViewModelName"`. This lets Rider perform full compile-time checking of data bindings and provides intelligent autocompletion.
- Modern C# features: use primary constructors for dependency injection in classes, collection expressions `[]`, pattern matching, record types for DTO models, switch expressions, and `required` properties where appropriate.
- Reliability: `#nullable enable` must be enabled throughout the code. No uncontrolled `NullReferenceException`s.
- Asynchrony: all I/O operations, network calls and audio work must be purely asynchronous (`async/await`), with `CancellationToken` passed through.
- Error handling and logging: use safe Guard Clauses and structured logging via `ILogger<T>`.

3. Libraries and ecosystem:

- UI: Avalonia UI (no Windows-specific WinForms/WPF dependencies).
- MVVM: `CommunityToolkit.Mvvm` (attributes `[ObservableProperty]`, `[RelayCommand]`, inheritance from `ObservableObject`).
- Audio: `ManagedBass` (support for cross-platform native libraries: `libbass.so` on Arch and `bass.dll` on Windows 11).
- Global hotkeys: `SharpHook` via a background service (`TaskPoolGlobalHook`).
- Clipboard: `wl-paste` (`wl-clipboard`) on Linux Wayland; `TextCopy` — Windows only.
- Serialization: `System.Text.Json` with indentation settings and key-name casing that matches the server.

4. Documentation and comments:

- At the beginning of every file, add a short descriptive block in JSDoc style (`/** ... */`) that explains the module's role in the system.
- Add thorough XML documentation (`/// <summary>`, `<param>`, `<returns>`) to all public interfaces, classes and methods.
- For interface implementations use the `<inheritdoc />` tag.
- The language of documentation and comments is English only.

5. Automated testing (autotests; deliberately not created in the current iteration 2):

- Framework (for the future): xUnit + FluentAssertions + NSubstitute.
- Coverage: unit tests for the business logic of services, parsers and ViewModels.
- Isolation: all external interactions (disk access, HTTP requests to Flask, audio player calls, clipboard) must be behind interfaces and mocked via NSubstitute.
- Pattern: a clear AAA structure (Arrange, Act, Assert), separated by blank lines.
- Naming: `MethodName_InputConditions_ExpectedResult`.
- Check both successful scenarios and edge cases (null, no connection to the backend, cancellation via CancellationToken).

Generate clean, cross-platform, tested and maintainable code that is ready for JetBrains Rider.
