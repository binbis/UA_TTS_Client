# Extension points: how to add things

> **Purpose:** step-by-step recipes for the usual changes. Follow the layer rules in `architecture/Overview.md`.
> **Audience:** developers and AI agents.
> **Source of truth:** the code layout in `architecture/Project_Structure.md`.
> **Verified:** 2026-11-12.

## New service

1. Interface in `Core/Interfaces/I<Name>.cs`, with XML documentation in English.
2. Models and enums in `Core/Models` or `Core/Enums`. Use `record` for DTOs.
3. Implementation in the matching folder of `Infrastructure/`. No Avalonia types there.
4. Registration in `App.axaml.cs`: `services.AddSingleton<I<Name>, <Name>>();`.
5. If a ViewModel needs it for the designer, add a preview-only fake in `ViewModels/Design/DesignData.cs`.

## New setting

1. A property with a default value in `Core/Models/AppConfig.cs`. Old config files must still load.
2. In `ViewModels/Tabs/SettingsViewModel.cs`: a `[ObservableProperty]`, its assignment in `ApplyConfig`, and its value in `SaveAsync`.
3. A control in `Views/Tabs/SettingsView.axaml`. For a list of options, use an option record (as `ChunkingModeOption` does).
4. If the value is used during playback or the hotkey, read it from `SettingsViewModel` at that moment. Do not cache it.
5. Add the value to `DesignData.PreviewConfig` if the designer needs it.
6. Document it: `user/Configuration.md`, and `user/User_Guide.md` if it is visible in the UI.

## New tab

1. `ViewModels/Tabs/<Name>ViewModel.cs` as `public partial class <Name>ViewModel : ViewModelBase`.
2. `Views/Tabs/<Name>View.axaml` (and `.cs`) with `x:DataType="vm:<Name>ViewModel"`. `ViewLocator` finds the view by name.
3. Registration in `App.axaml.cs` as a singleton, and a property on `MainWindowViewModel`.
4. A `TabItem` in `Views/MainWindow.axaml`. Check the tab index constant `QuickTtsTabIndex` (0) in `MainWindowViewModel` if the order changes.
5. A design-time instance in `DesignData`, if the view needs one.
6. Update `architecture/Project_Structure.md` and `user/User_Guide.md`.

## New server endpoint (client side)

1. A method in `ITtsApiClient` with a `CancellationToken`.
2. The implementation in `Infrastructure/Api/TtsApiClient.cs`. Use `SendAsync` so that errors are handled the same way, then `ReadJsonAsync` or a byte read.
3. A fake in `PreviewTtsApiClient` (`DesignData.cs`).
4. The description in `server/API.md`: request, response, errors, and a `curl` example.

## New server endpoint (server side)

1. A route in `Server_py/server.py`.
2. JSON errors in the form `{"error": "<text>"}` with an HTTP status code, as the other routes do.
3. Keep the CORS handler; do not change its headers.
4. Then follow the client-side steps above.

## New export format

1. A value in `Core/Enums/AudioExportFormat.cs`.
2. The conversion in `AudioExportService.ExportAsync`. Today only MP3 is converted, through `ITtsApiClient.ConvertAsync`. A format that the server converts needs the server to support it (`server/API.md`).
3. An option record `ExportFormatOption` in `SettingsViewModel`, and the file type in `Views/Services/AvaloniaFileSaveDialogService.cs` (`CreateFileType`).
4. Update `user/User_Guide.md`, `user/Configuration.md`, and `server/API.md` if the server changes.

## New chunking mode

1. A value in `Core/Enums/TextChunkingMode.cs`.
2. A branch in `SentenceAwareTextChunker.Split` (the `switch` on `options.Mode`), and, if the mode must skip the merge step, the condition in `Split`.
3. An option in `SettingsViewModel` (`ChunkingModeOption`) with a UI label.
4. Update `architecture/Text_Chunking.md` and `user/User_Guide.md`.

## Changing the hotkey

The hotkey is fixed in `Infrastructure/Automation/SharpHookGlobalHotkeyService.cs` (Alt + Q). `AppConfig.Hotkey` is stored and shown in Settings, but the hook does not read it. Making it configurable means changing the key tracking in the hook and the Settings UI, and then updating `user/Configuration.md` and `user/User_Guide.md`.
