# Installation

> **Purpose:** install and start the TTS server and the desktop client.
> **Audience:** users, and developers who run the application for the first time.
> **Source of truth:** `Server_py/requirements.txt`, `Server_py/run_server.sh`, `UA_TTS_Client.csproj`.
> **Verified:** 2026-11-12. Linux was run on the reference machine. Windows was not run.

## Requirements

| Component | Linux (EndeavourOS/Arch, Wayland + PipeWire) | Windows 11 |
|---|---|---|
| Server | Python (tested on 3.14), an NVIDIA GPU with CUDA recommended | Python (tested on 3.14), a CUDA build of PyTorch recommended |
| Server packages | `Server_py/requirements.txt`; Git, because some packages install from GitHub | the same |
| Client | .NET SDK 10 | .NET SDK 10 |
| BASS audio library | `libbass` from AUR: `yay -S libbass` | `bass.dll` next to the built executable (BASS 2.4, un4seen.com) |
| Clipboard | `wl-clipboard`: `sudo pacman -S wl-clipboard` | built in (TextCopy) |
| Keyboard simulation | write access to `/dev/uinput`: the user is in the `input` group | built in (SharpHook) |

Notes:

- The `bass` package in the `extra` repository is the game *Beneath A Steel Sky*, not the audio library. Install `libbass` from AUR.
- The `ManagedBass` NuGet package contains only the managed wrapper. The native library must be installed separately.
- `torch` is not pinned in `requirements.txt`. It is installed as a dependency of `styletts2-inference`. On Linux, the PyPI build pulls in the CUDA runtime packages; the reference machine has `torch 2.14.1+cu130`. On Windows the PyPI build is usually CPU-only. Install the CUDA build of PyTorch from the PyTorch website before installing the requirements. This has not been verified on Windows.

## Server on Linux

```bash
python3 -m venv Server_py/venv
Server_py/venv/bin/python -m pip install --upgrade pip
Server_py/venv/bin/python -m pip install -r Server_py/requirements.txt
./Server_py/run_server.sh
```

`run_server.sh` changes into `Server_py/` itself, so it can be started from the repository root. Do not start `server.py` by hand from the repository root: the voices and the dictionary would not load.

## Server on Windows

```bat
cd Server_py
python -m venv venv
venv\Scripts\activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python server.py
```

## First start and check

The first start loads the model `patriotyk/styletts2_ukrainian_multispeaker` (about 911 MB, from the Hugging Face cache). Loading takes about 45 seconds. Then check the server:

```bash
curl -sS http://127.0.0.1:5000/api/health
# {"status":"ok","device":"cuda","voices":31,"model":"patriotyk/styletts2_ukrainian_multispeaker"}
```

`device` is `cpu` when CUDA is not available. Synthesis then still works, but it is much slower. All endpoints are described in `server/API.md`.

## Client on Linux

```bash
yay -S libbass                         # native BASS library (AUR)
sudo pacman -S wl-clipboard            # wl-paste, for reading the clipboard
sudo usermod -aG input "$USER"         # uinput access for keyboard simulation; log out and back in
dotnet build
dotnet run                             # or open UA_TTS_Client.sln in Rider
```

## Client on Windows

1. Install the .NET SDK 10.
2. Put `bass.dll` next to the built executable.
3. Run `dotnet build` and `dotnet run`, or open `UA_TTS_Client.sln` in Rider.

The Windows build has not been run on a real system. The platform differences are listed in `architecture/Overview.md`.

## Start order

Start the server first, then the client. If the server is offline when the client starts, the status bar shows «Офлайн» (Offline). The client keeps running and checks the server every 15 seconds.

## Settings file

The settings file `config.json` is created on the first save. See `user/Configuration.md` for its location and fields.
