# Architecture overview

> **Purpose:** what the system is, its components, layers, threads, platforms and the key design choices.
> **Audience:** developers and AI agents.
> **Source of truth:** the code: `Core/`, `Infrastructure/`, `ViewModels/`, `Views/`, `App.axaml.cs`, `Server_py/server.py`.
> **Verified:** 2026-11-12.

## What it is

Two programs that work together on one machine:

- **Server** (`Server_py/`): Python, Flask, StyleTTS2 model, CUDA. It turns Ukrainian text into 24 kHz mono WAV, converts numbers to words and converts WAV to other formats. See `server/Server.md`.
- **Client** (repository root): .NET 10, Avalonia 12, MVVM with CommunityToolkit.Mvvm, ManagedBass for playback, SharpHook for the global hotkey. It captures selected text, splits it into fragments, plays them in batches and exports audio.

The client talks to the server over HTTP (default `http://127.0.0.1:5000`). See `server/API.md`.

```text
User ──Alt+Q / UI──▶ Client (Avalonia)
                      ├── ViewModels ──▶ Core interfaces ◀── Infrastructure (HTTP, BASS, hook, clipboard, JSON)
                      └── HTTP ──▶ Server (Flask) ──▶ StyleTTS2 (GPU)
```

## Layers

| Layer | Directory | Contents | May depend on |
|---|---|---|---|
| Domain | `Core/` | enums, DTOs, exceptions, service interfaces | nothing (no Avalonia, HttpClient, BASS, SharpHook) |
| Infrastructure | `Infrastructure/` | implementations of the `Core` interfaces | `Core`, external libraries |
| ViewModels | `ViewModels/` | MVVM logic, commands, state | `Core`, `ILogger<T>` |
| Views | `Views/` | XAML and code-behind | `ViewModels` |
| Composition root | `App.axaml.cs` | DI registration, window creation, lifecycle | all layers |
| UI services | `Views/Services/` | services that need Avalonia (the save dialog) | Avalonia, `Core` |

Rules:

- Constructor injection only. Registration only in `App.axaml.cs`.
- `Core` depends on nothing outside .NET.
- Transport errors become `TtsApiException` before a ViewModel sees them.
- Paths are built with `Path.Combine` and `Environment.SpecialFolder.ApplicationData`.
- Audio is played from memory (`byte[]`); no temporary WAV files.

## Components

| Component | Responsibility |
|---|---|
| `TtsApiClient` | typed `HttpClient`: voices, health, synthesize, convert, verbalize, parse file |
| `SentenceAwareTextChunker` | splits text into fragments (see `architecture/Text_Chunking.md`) |
| `FragmentPlaybackService` | synthesizes fragments one step ahead and plays them in batches |
| `BassAudioPlayerService` | BASS playback from memory; pause, resume, stop, volume; natural-end event |
| `AudioExportService` | synthesizes all fragments, concatenates WAV, converts to MP3, writes atomically |
| `WavAudioConcatenator` | validates 24 kHz mono 16-bit WAV fragments and concatenates them |
| `SharpHookGlobalHotkeyService` | global `Alt+Q`, raised after both keys are released |
| `WaylandClipboardAutomationService` | simulates `Ctrl+C` and reads the clipboard (`wl-paste` on Linux, TextCopy on Windows) |
| `JsonSettingsService` | loads and atomically saves `config.json` |
| `MainWindowViewModel` | tabs, server status, hotkey flow, mini player wiring |
| `QuickTtsViewModel` | text field, capture, synthesis, export |
| `SettingsViewModel` | editing and saving the settings |
| `PlaybackControlViewModel` | mini player state, queue counters, pause, resume, stop |

## Threads

- **UI thread:** ViewModels are created on it and their bound properties are changed on it.
- **BASS callbacks:** the end-of-track callback runs on a native thread. `BassAudioPlayerService` raises `TrackEnded` and `StateChanged` there; ViewModels marshal the changes with `OnUiThread`.
- **Hotkey:** `SharpHookGlobalHotkeyService` raises `HotkeyPressed` on a thread-pool thread; `MainWindowViewModel` marshals it to the UI thread.
- **Network:** all calls are asynchronous and take a `CancellationToken`.
- **Synthesis:** requests are sequential, one at a time, so the GPU is not overloaded.

## Platforms

| Aspect | Linux (Wayland + PipeWire) | Windows 11 |
|---|---|---|
| Clipboard | `wl-paste --no-newline` (`wl-clipboard`). X11 is not supported. | TextCopy (Win32 clipboard) |
| Keyboard simulation | SharpHook `EventSimulator` (uinput) | SharpHook `EventSimulator` |
| Audio | `libbass.so` (AUR `libbass`), output through PipeWire | `bass.dll` next to the executable |
| Configuration | `~/.config/UA_TTS_Client/config.json` | `%APPDATA%\UA_TTS_Client\config.json` |
| Verification | works on the reference machine | build only; runtime not verified |

## Key design choices

These describe the current behavior and the reasons for it.

- **Chunking is on the client.** The server accepts one text per request. The client splits the text and sends sequential requests. The server does not know about fragments.
- **Auto-speech after `Alt+Q` is on by default; window activation is off by default.** The default flow is "select, press, hear it" without changing focus.
- **The default chunking mode is «Абзаци» (paragraphs).** Each non-empty line is a paragraph; long paragraphs are split by sentence.
- **Batches.** Fragments play in batches of N (default 5). By default playback pauses after each batch and waits for the user.
- **X11 is not supported.** On Linux the clipboard is read only through `wl-paste`. The X11 fallback was removed. TextCopy is used on Windows only.
- **Export covers the whole text,** not only the parts that were played. Playback stops before the export starts, so the export does not compete with the queue for the GPU.
- **MP3 is produced by the server.** The client sends the concatenated WAV to `POST /api/convert`. WAV needs no server call.
- **No test project.** Verification is done with manual scenarios (`development/Manual_Test_Scenarios.md`) and temporary harnesses outside the repository.
- **Reader and Character Voice are placeholders.** They are not part of the current scope (`Roadmap.md`).

## Technical glossary

| Term | Meaning |
|---|---|
| Fragment | A piece of text (`TextFragment`) that is synthesized by one request |
| Batch | A group of up to `BatchSize` fragments that play before a pause |
| Queue | All fragments of one text, handled by `FragmentPlaybackService` |
| Capture | A simulated `Ctrl+C` followed by reading the clipboard |
| Synthesis | `POST /api/synthesize` for one fragment; the response is WAV bytes |
| Export | Synthesis of all fragments, concatenation, optional conversion, atomic write |

For the UI glossary see `user/User_Guide.md`.
