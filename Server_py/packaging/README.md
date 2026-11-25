# Server packaging (PyInstaller, onedir)

The server is distributed as a ready folder `UA_TTS_Server/` with an
executable inside (`UA_TTS_Server` on Linux, `UA_TTS_Server.exe` on Windows).
The user needs no Python. The Python interpreter and all dependencies,
including the Torch CUDA libraries, are inside `_internal/`.

> The model (~911 MB) is **not** in the package. On the first start the
> server downloads it to `hf/` inside the package folder. Later starts work
> offline. The log is written to `logs/server.log` in the same folder.

## Layout of the distributed folder

```
UA_TTS_Server/               # next to the client publish
├── UA_TTS_Server            # Linux ELF / Windows exe (double-click to run)
├── _internal/               # Python + torch + ffmpeg, everything frozen
├── voices/*.pt              # user data: add voices here, restart the server
├── dictionary.json          # user data: pronunciation rules, restart to reload
├── hf/                      # created on the first start: the downloaded model
└── logs/server.log          # created on the first start: server log
```

Place `UA_TTS_Server/` next to the client publish (the client finds the
server at `<appdir>/UA_TTS_Server/UA_TTS_Server(.exe)` by default).
The folder must be in a writable location (not Program Files): the server
writes the model cache (`hf/`), logs (`logs/`) and any new voices there.

## Build (same commands as GitHub Actions)

Build machine requirements: Python **3.12** (as in CI; a newer Python with a
matching CUDA Torch build, e.g. 3.14, also works), Git (some dependencies
install from GitHub). All steps run from `Server_py/`.

> If you already have a working server venv (e.g. `Server_py/venv`), you can
> build from it directly: install `pyinstaller imageio-ffmpeg` into it,
> then run the `pyinstaller`, `cp` and `--check` commands below.

### Linux

```bash
python3.12 -m venv .build-venv
.build-venv/bin/pip install --upgrade pip
.build-venv/bin/pip install torch --index-url https://download.pytorch.org/whl/cu128
.build-venv/bin/pip install -r requirements.txt pyinstaller imageio-ffmpeg
.build-venv/bin/pyinstaller packaging/UA_TTS_Server.spec
cp -r voices dictionary.json dist/UA_TTS_Server/
./dist/UA_TTS_Server/UA_TTS_Server --check
# Zip the folder the same way CI does (name of the downloadable artifact):
(cd dist && zip -qr ../UA_TTS_Server-linux-x86_64.zip UA_TTS_Server)
```

### Windows (PowerShell)

```powershell
cd Server_py
py -3.12 -m venv .build-venv
.build-venv\Scripts\pip install --upgrade pip
.build-venv\Scripts\pip install torch --index-url https://download.pytorch.org/whl/cu128
.build-venv\Scripts\pip install -r requirements.txt pyinstaller imageio-ffmpeg
.build-venv\Scripts\pyinstaller packaging\UA_TTS_Server.spec
Copy-Item -Recurse voices, dictionary.json dist\UA_TTS_Server\
.\dist\UA_TTS_Server\UA_TTS_Server.exe --check
# Zip the folder the same way CI does (name of the downloadable artifact):
Compress-Archive -Path dist\UA_TTS_Server -DestinationPath UA_TTS_Server-windows-x64.zip
```

Notes:

- Torch is installed **first** from the PyTorch CUDA index, otherwise pip
  pulls the CPU-only wheel from PyPI (on Windows this is the usual trap).
- `dist/`, `.build-venv/` and `build/` are not committed to git.
- `--check` verifies that the critical imports work and exits without
  loading the model (used as the CI smoke test).

## Run the packaged server

- Double-click the executable: a console with logs opens; closing it stops
  the server. Ready when `GET http://127.0.0.1:5000/api/health` answers.
- Or start it from a terminal — same logs, and a file copy at
  `logs/server.log` inside the package folder (`UA_TTS_Server/logs/`).
- Options: `--log-file PATH`, `--log-level DEBUG|INFO|WARNING|ERROR`,
  `--port PORT`, `--check`.
