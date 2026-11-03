# Configuration

> **Purpose:** the settings file: its location, format, fields and default values.
> **Audience:** users and developers.
> **Source of truth:** `Core/Models/AppConfig.cs`, `Infrastructure/Storage/JsonSettingsService.cs`, `ViewModels/Tabs/SettingsViewModel.cs`.
> **Verified:** 2026-11-03.

## Location

| Platform | Path |
|---|---|
| Linux | `~/.config/UA_TTS_Client/config.json` |
| Windows | `%APPDATA%\UA_TTS_Client\config.json` |

The folder is taken from `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`.

## Format

- JSON with camelCase property names, indented.
- Enums are stored as strings, for example `"chunkingMode": "Paragraphs"` and `"exportFormat": "Mp4"`.
- A field that is missing gets its default value. Older files keep working.
- An empty or unreadable file makes the client start with the defaults and write a warning to the log. The next save overwrites the file.
- Saving is atomic: the data is written to a temporary file in the same folder, which then replaces the original.
- The client writes the file only when you click **«Зберегти»** (Save) in the Settings tab.

## Fields

| JSON key | Type | Default | Meaning |
|---|---|---|---|
| `serverUrl` | string | `http://127.0.0.1:5000` | Server base address. Applied after a restart. |
| `selectedVoice` | string or null | `null` | Voice name from `GET /api/voices`. `null` means the server's first voice. |
| `speed` | number | `1.0` | Speech speed, sent to the server. Settings range 0.5–2.0, step 0.1. |
| `volume` | number | `1.0` | Playback volume, 0.0–1.0. Settings step 0.1. |
| `hotkey` | string | `Alt+Q` | Stored and shown in Settings, but the hook always uses `Alt+Q`. |
| `autoSynthesizeOnHotkey` | bool | `true` | Start speech automatically after `Alt+Q`. |
| `activateOnHotkey` | bool | `false` | Activate the window and switch to Quick TTS after `Alt+Q`. |
| `chunkingMode` | enum | `Paragraphs` | `Paragraphs`, `Sentences` or `MaxLength` (the UI label is «До 250 символів (розумне пакування)»). |
| `batchSize` | int | `5` | Fragments per batch, 1–10. Values outside the range are clamped when playback starts. |
| `pauseAfterEachBatch` | bool | `true` | Stop after each batch. `false` plays all batches one after another. |
| `exportFormat` | enum | `Mp4` | `Mp4` or `Wav`. |

The maximum fragment length, 250 characters, is not stored in the file. It is the constant `TextChunkingOptions.DefaultMaxChunkLength`.

## Example

```json
{
  "serverUrl": "http://127.0.0.1:5000",
  "selectedVoice": "Вероніка Дорош",
  "speed": 1.0,
  "volume": 1.0,
  "hotkey": "Alt+Q",
  "autoSynthesizeOnHotkey": true,
  "activateOnHotkey": false,
  "chunkingMode": "Paragraphs",
  "batchSize": 5,
  "pauseAfterEachBatch": true,
  "exportFormat": "Mp4"
}
```
