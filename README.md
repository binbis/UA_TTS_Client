# UA_TTS_Client

Desktop client for a **local** Ukrainian text-to-speech server (StyleTTS2). Select text in any application and press `Alt+Q`: the text is captured, split into fragments and read aloud in batches. You can also paste text into the Quick TTS tab and save the whole text as an MP3 or WAV file.

| Part | Stack | Location |
|---|---|---|
| TTS server | Python, Flask, StyleTTS2, CUDA | `Server_py/` |
| Desktop client | .NET 10, Avalonia 12, MVVM, ManagedBass | repository root |

Platforms: Linux (EndeavourOS/Arch, Wayland + PipeWire) is tested on the reference machine. Windows 11 is supported by the code, but it has not been run.

## Quick start

1. Install the requirements for your platform: [Installation](Doc/user/Installation.md).
2. Start the server from the `Server_py/` folder. On Linux:

   ```bash
   ./Server_py/run_server.sh
   ```

   The first start takes about 45 seconds.
3. Build and run the client:

   ```bash
   dotnet build
   dotnet run
   ```
4. Select text in any window and press `Alt+Q`. Or open the **Quick TTS** tab, paste the text and click **«Озвучити»** (Speak).

## Features

- Global hotkey `Alt+Q`: captures the selection and starts the speech.
- Text is split into fragments by paragraph, by sentence, or up to 250 characters. Fragments play in batches of 1–10.
- Pause after each batch (the default), or continuous playback.
- Export of the whole text to MP3 (the default) or WAV.
- 31 Ukrainian voices, with speed and volume controls.

## Documentation

- For users: [Installation](Doc/user/Installation.md) · [User guide](Doc/user/User_Guide.md) · [Configuration](Doc/user/Configuration.md) · [Troubleshooting](Doc/user/Troubleshooting.md)
- For developers: [Documentation index](Doc/README.md) · [Architecture](Doc/architecture/Overview.md) · [Server](Doc/server/Server.md) · [Server API](Doc/server/API.md)
- For AI agents: [AGENTS.md](AGENTS.md)

## Known limitations

- Window activation after `Alt+Q` depends on the compositor. On Wayland it may be refused.
- MP3 export needs a working ffmpeg on the server. WAV export always works.
- Terminals copy with `Ctrl+Shift+C`. Capture from a terminal is not supported.
- The Windows runtime has not been verified. See [Troubleshooting](Doc/user/Troubleshooting.md).
