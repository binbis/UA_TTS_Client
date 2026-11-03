# UI and DI: verified facts

> **Purpose:** rules and verified behavior of Avalonia 12, CommunityToolkit.Mvvm 8.4.2 and the DI setup of this project. Read it before editing XAML or ViewModels.
> **Audience:** developers and AI agents.
> **Source of truth:** `App.axaml.cs`, `ViewModels/*`, `Views/*`, `ViewLocator.cs`, `ViewModels/Design/DesignData.cs`, and runs on the reference machine.
> **Verified:** 2026-11-03.

## MVVM with CommunityToolkit.Mvvm

- Every ViewModel is a `public partial class`. Without `partial`, the source generators do not produce the `[ObservableProperty]` and `[RelayCommand]` members.
- `[ObservableProperty] public partial string Foo { get; set; }` is the correct form for Toolkit 8.x: the generator provides the backing field and the change notification. Do not "fix" it.
- `[RelayCommand]` on a method produces `XxxCommand`. `CanExecute = nameof(CanX)` links a predicate. `IncludeCancelCommand = true` adds a `CancelXxxCommand` that cancels the running method.
- `ViewModelBase` derives from `ObservableObject`. It is `abstract` and not `partial`, because it has no generated members.

## Threads

- ViewModels are created and used on the UI thread.
- BASS events (`TrackEnded`, `StateChanged`) and the hotkey event can be raised on other threads. Call `ViewModelBase.OnUiThread(Action)` before changing a bound property.
- Example: `MainWindowViewModel` marshals the hotkey with `OnUiThread(() => _ = HandleHotkeyAsync())`.

## Helpers in `ViewModelBase`

- `OnUiThread(Action action)`: runs the action on the UI thread.
- `PluralWord(int count, string one, string few, string many)`: the Ukrainian plural form for a count.
- `SymbolsWord(int count)`: `PluralWord` for «символ» (character).

## View ↔ ViewModel

- `ViewLocator` maps `UA_TTS_Client.ViewModels.X.FooViewModel` to `UA_TTS_Client.Views.X.FooView`, replacing `.ViewModels` with `.Views` and `ViewModel` with `View`. For `MainWindowViewModel` it also tries `Views.MainWindow`.
- The main window is created explicitly in `App.axaml.cs`. The locator provides the tab content: `ContentControl Content="{Binding QuickTts}"` and similar.
- Every View and UserControl XAML declares `x:DataType="vm:XxxViewModel"` for compiled bindings. `App.axaml` is the exception: it has no data context.
- Names are case-sensitive on Linux: `x:Class` and `x:DataType` must match the names in the `.cs` files exactly.

## Design-time data

- Rider's analyzer reports an error for `Design.DataContext="{x:Static …}"`. Set the design-time DataContext in code-behind instead:

```csharp
if (Design.IsDesignMode)
{
    DataContext = DesignData.QuickTts;
}
```

- `ViewModels/Design/DesignData.cs` contains preview-only implementations of the `Core` interfaces, such as `PreviewTtsApiClient` and `PreviewAudioPlayerService`. Production code does not use them.

## Dependency injection

- Constructor injection only. Services and ViewModels are registered as singletons in `App.axaml.cs` (`ServiceCollection`).
- `AddHttpClient<ITtsApiClient, TtsApiClient>` sets `BaseAddress` from `AppConfig.ServerUrl` and `Timeout` to 5 minutes. The `TtsApiClient` constructor raises a timeout below 2 minutes to 5 minutes.
- `IFileSaveDialogService` is implemented in `Views/Services/` because it needs Avalonia. `Infrastructure` must not reference Avalonia.
- On exit, `desktop.Exit` disposes `MainWindowViewModel` and the `ServiceProvider`.

## Avalonia 12 specifics (verified)

- `TextBox.Watermark` is obsolete. Use `PlaceholderText`.
- Scrollbars of a `TextBox`: `ScrollViewer.VerticalScrollBarVisibility="Auto"`.
- Conditional styling: `Classes.online="{Binding IsServerOnline}"` with the style `Ellipse.status-dot.online`. Styles defined in `Window.Styles` apply to child UserControls.
- Visibility from a string: `{Binding X, Converter={x:Static conv:StringConverters.IsNotNullOrEmpty}}`.
- `NumericUpDown.Value` is `decimal?` in Avalonia 12.1.4. Binding it to an `int` property works. `FormatString="0"` shows whole numbers.
- Speed and volume sliders: `TickFrequency="0.1"`, `IsSnapToTickEnabled="True"`, value format `StringFormat='{}{0:0.0}'`. Speed range 0.5–2.0, volume range 0–1.
- Option lists use records (`ChunkingModeOption(Mode, Title)`, `ExportFormatOption(Format, Title)`). `SelectedItem` binds to a nullable property, and the logic falls back to the defaults.
- A `StringFormat` inside a compiled binding is checked by `dotnet build`. If it fails, add a computed property instead.

## Build and analyzers

- `dotnet build` must report 0 warnings and 0 errors.
- Rider's XAML analyzer reports `Design.DataContext` issues. Use the code-behind approach above.
