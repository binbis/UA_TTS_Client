# Code standards and task prompt

> **Purpose:** the prompt template for code-generation tasks, and the normative coding standards of this project.
> **Audience:** developers and AI agents who write or review code.
> **Source of truth:** this file for coding standards. Facts about the project are in the owner documents listed in `Doc/README.md`.
> **Verified:** 2026-11-03 against the code.

## How to use the prompt

1. Read `AGENTS.md`, then the owner document of the area you change. `Doc/README.md` lists them.
2. Replace the placeholders below and send the whole file together with the task.
3. Expect the answer to contain only the changed or new files, or the changed fragments that must be replaced.

**Project:** UA_TTS_Client, an Avalonia 12 / .NET 10 desktop client (MVVM) for a local Ukrainian TTS server (`Server_py/`, Flask and StyleTTS2).
**Targets:** Linux (EndeavourOS/Arch, Wayland + PipeWire), tested on the reference machine; Windows 11, not yet run.

Context: [WHERE WE ARE AND WHERE WE START FROM, BACKGROUND]

Goal: [THE GOAL OF THE CHANGE]

Task: [DESCRIBE THE SPECIFIC TASK]

Important notes:
- [SPECIFIC CLARIFICATIONS OR CONSTRAINTS]
- Return only the changed or new files, or the changed code fragments that must be replaced. Do not repeat unchanged files unless a full file is needed.
- If the task conflicts with these standards, say so before you write code.

When performing the task, strictly follow these standards.

## Technology stack

Versions are taken from `UA_TTS_Client.csproj`.

| Area | Choice |
|---|---|
| Runtime | .NET 10 (`net10.0`), C# 13 or newer |
| UI | Avalonia 12.1.4 with compiled bindings (`x:DataType`). No WinForms or WPF. |
| MVVM | CommunityToolkit.Mvvm 8.4.2: `[ObservableProperty]`, `[RelayCommand]` |
| Audio | ManagedBass 4.0.2 over the native BASS library: `libbass.so` on Linux, `bass.dll` on Windows |
| Global hotkey and key simulation | SharpHook 8.0.0: `TaskPoolGlobalHook`, `EventSimulator` |
| Clipboard | `wl-paste` (wl-clipboard) on Linux under Wayland; TextCopy 6.2.1 on Windows only |
| HTTP | `Microsoft.Extensions.Http`: typed `HttpClient` |
| DI and logging | `Microsoft.Extensions.DependencyInjection` and `Microsoft.Extensions.Logging` 10.0.13 |
| Serialization | `System.Text.Json` |
| Tests | None at present. xUnit, FluentAssertions and NSubstitute are the planned frameworks (section 5). |

## 1. Architecture and cross-platform rules

- **Layers.** `Core` holds interfaces, models and enums, and has no reference to Avalonia, HTTP, BASS or SharpHook. `Infrastructure` implements the `Core` interfaces. ViewModels use `Core` only. Views use ViewModels only. See `Doc/architecture/Overview.md`.
- **Principles.** SOLID, DRY, KISS and YAGNI. Do not add an abstraction for a single use unless it serves a test or a second platform.
- **Dependency injection.** Constructor injection only. Services and ViewModels are registered in `App.axaml.cs`. No service locator and no static access to services.
- **Typed HttpClient.** `services.AddHttpClient<ITtsApiClient, TtsApiClient>(...)` with a timeout of at least 120 seconds; the project uses 5 minutes. Transport errors become `TtsApiException` before a ViewModel sees them.
- **Paths.** Build paths with `Path.Combine`, starting from `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`. Linux is case-sensitive.
- **Serialization.** `System.Text.Json` with `JsonSerializerDefaults.Web`: camelCase property names, which match the server's JSON keys. The config file is indented and stores enums as strings (`JsonStringEnumConverter`).
- **Audio from memory.** Play `byte[]` or `MemoryStream` data. Do not write temporary `.wav` files for playback.
- **Native resources.** Free each BASS stream with `Bass.StreamFree` and call `Bass.Free` on disposal. A class that owns native handles implements `IDisposable` and releases them in `Dispose`.
- **Clipboard after `Ctrl+C` (Wayland).** Do not read the clipboard once after a fixed delay. Poll it every 50 ms, for at most 900 ms, until the content differs from the value read before the copy. Return an empty string if it never differs, and never return the previous content. Consequently, a selection that equals the current clipboard text is not detected. See `Doc/reference/Native_Libraries.md`.
- **X11** is not supported. Do not add an X11 fallback.
- **Synthesis requests** to the server are sequential, one at a time. Parallel requests overload the GPU.
- **Hotkey order.** Capture, then activate the window, then synthesize. See `Doc/architecture/Flows.md`.

## 2. C# and Rider

- **Namespaces.** File-scoped namespaces: `namespace UA_TTS_Client.Core.Interfaces;`.
- **Usings.** The project has no implicit usings. Add every `using` directive explicitly.
- **Nullability.** `<Nullable>enable</Nullable>` is set in the csproj. New files start with `#nullable enable`. Do not use `null!` to silence the compiler, and do not let a `NullReferenceException` escape.
- **ViewModels.** A ViewModel that uses the source generators is a `public partial class`. Write `[ObservableProperty] public partial string Foo { get; set; }`. This form is correct; do not rewrite it as a plain property. Commands use `[RelayCommand]`.
- **XAML.** Every View and UserControl declares `x:DataType="vm:<Name>ViewModel"` for compiled bindings. `App.axaml` has no data context and is the exception.
- **Design-time data.** Set the designer's DataContext in code-behind (`if (Design.IsDesignMode) DataContext = DesignData.<Name>;`), not with `Design.DataContext` in XAML. Rider's analyzer reports an error for the XAML form.
- **Constructors and guards.** Services use explicit constructors that validate their arguments. In new code prefer `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace` and `ObjectDisposedException.ThrowIf`. Primary constructors are for records and small immutable types.
- **Records.** DTOs, options and value types are `sealed record`s. Use positional parameters for small values (`TextFragment`) and `required` for mandatory members.
- **Expressions.** Collection expressions `[]`, switch expressions and pattern matching are welcome where they make the code shorter. A constant regular expression uses `[GeneratedRegex]`.
- **Asynchrony.** Every I/O, network and audio operation is asynchronous. The last parameter is `CancellationToken cancellationToken = default`, and it is passed on. Wait with `Task.Delay(…, cancellationToken)`, never with `Thread.Sleep`. Infrastructure awaits with `ConfigureAwait(false)`. ViewModels do not use it, because they need the UI context.
- **Threads.** BASS callbacks and hotkey events arrive on other threads. Change bound properties only on the UI thread, through `ViewModelBase.OnUiThread`. Protect shared state with `lock`, and mark flags that are read across threads `volatile`.
- **Logging.** Use `ILogger<T>` with message templates: `_logger.LogWarning("Не вдалося прочитати конфігурацію {Path}", path);`. Never use string interpolation in log calls.

## 3. Naming and layout

- Interfaces start with `I`: `ITtsApiClient`. Implementations are named by their role: `BassAudioPlayerService`, `SentenceAwareTextChunker`.
- ViewModels end with `ViewModel` and views with `View`: `QuickTtsViewModel`, `QuickTtsView`. Option records for ComboBoxes end with `Option`: `ChunkingModeOption`.
- Private fields are `_camelCase`, and `readonly` where possible.
- Async methods end with `Async`.
- Event arguments end with `EventArgs`: `FragmentStartedEventArgs`.
- Folders follow the layers. The code map is in `Doc/architecture/Project_Structure.md`.

## 4. Documentation and comments

- Every public type and every public member has XML documentation: `<summary>`, and `<param>`, `<returns>` and `<exception>` where they apply.
- Interface implementations use `<inheritdoc />`.
- C# files have no file headers and no JSDoc blocks. The role of a module is described in the `<summary>` of its public type.
- Comments are in English. They explain why, not what. Do not repeat the code in comments.
- Ukrainian appears in the code only as product text: UI labels, status and error messages, log messages, voice names and dictionary data. A comment may quote a Ukrainian label, such as `"Озвучити"`, but the comment itself is English.
- When behavior changes, update the owner document listed in `Doc/README.md` in the same change.

## 5. Testing and verification

- There is no test project at present; see `Doc/Roadmap.md`. Verify a change with `dotnet build` (0 warnings, 0 errors), with the scenarios in `Doc/development/Manual_Test_Scenarios.md`, and with temporary harnesses outside the repository.
- When a test project is introduced, use xUnit, FluentAssertions and NSubstitute:
  - put disk access, HTTP, audio, clipboard and the hotkey behind interfaces, and replace them with NSubstitute substitutes;
  - structure every test as Arrange, Act and Assert, separated by blank lines;
  - name tests `MethodName_InputCondition_ExpectedResult`;
  - cover the success path, null input, a missing server connection, and cancellation through `CancellationToken`.

Generate clean, cross-platform and maintainable code that builds without warnings and works in JetBrains Rider.
