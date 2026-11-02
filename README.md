# UA_TTS_Client

Cross-platform desktop client for a **local** StyleTTS2 TTS server: speech synthesis of Ukrainian texts with 31 voices, capture of selected text, and a global `Alt+Q` hotkey.

Target OS: **Linux (EndeavourOS/Arch, Wayland + PipeWire)** and **Windows 11**.

| Part | Stack | Directory |
|---|---|---|
| Speech synthesis server | Python, Flask, StyleTTS2, CUDA | `Server_py/` |
| Client (window, player, hook) | .NET 10, Avalonia 12, CommunityToolkit.Mvvm, ManagedBass | repository root |

---

## Requirements

**Server**

- Python 3.10+ (tested on 3.14) and a GPU with CUDA. Without CUDA the server runs on `cpu`, but synthesis is noticeably slower.
- Dependencies: `Server_py/requirements.txt`.

**Client**

- .NET SDK 10.
- **Linux:**
  - the system BASS library: `yay -S libbass` (the AUR package is **`libbass`**; `bass` from the `extra` repository is the game «Beneath A Steel Sky», not a library);
  - `sudo pacman -S wl-clipboard` — for clipboard access (`wl-paste` is enough);
  - the user must be in the `input` group (`sudo usermod -aG input $USER` + log out and back in), and the `ydotool` daemon must run: `systemctl --user enable --now ydotool` — for keyboard simulation.
- **Windows:** the `ManagedBass` NuGet package contains only the managed wrapper, so `bass.dll` must sit next to the binary (BASS 2.4, un4seen.com). This repository has been tested only on Linux; the Windows build has not been tested.

---

## Starting the server

`server.py` uses relative paths (`voices/*.pt`, `dictionary.json`), so **it can only be started from the `Server_py/` directory**. Starting it from the repository root silently yields 0 voices and an empty dictionary.

### Linux

```bash
./Server_py/run_server.sh        # the script changes into Server_py/ itself and uses venv/
```

The first launch loads the model (~911 MB from the HuggingFace cache) — the first request takes ≈45 s. Check:

```bash
curl -sS http://127.0.0.1:5000/api/health
# {"device":"cuda","model":"patriotyk/styletts2_ukrainian_multispeaker","status":"ok","voices":31}
```

### Windows

```bat
cd Server_py
python -m venv venv
venv\Scripts\activate
python -m pip install --upgrade pip
python -m pip install -r requirements.txt
python server.py
```

Full list of dependencies and the CUDA driver: `Server_py/requirements.txt`.

---

## Starting the client

The server must be running **before** the client starts (otherwise the status bar shows «Офлайн» (Offline), but the app does not crash — the status is checked every 15 s).

```bash
dotnet build    # 0 warnings / 0 errors
dotnet run      # or open UA_TTS_Client.sln in Rider
```

---

## 📖 How to use

### Status bar (top of the window)

A green dot means the server is online (red means offline). The text next to it shows the compute device and the number of voices, e.g. `Онлайн · cuda · 31 голосів` (Online · cuda · 31 voices). Hovering over the text shows details; the **«Перевірити»** (Check) button forces a status refresh.

### Quick TTS tab — quick speech

1. Paste the text into the field, or click **«Захопити виділене»** (Capture selection) — the service simulates `Ctrl+C` in the active window and reads the system clipboard.
2. Click **«Озвучити»** (Speak): the text is split into fragments (paragraphs / sentences / up to 250 characters — the mode is set in the settings), synthesis runs sequentially, and the mini player switches to the `Playing` state.
3. Playback runs in **batches of 5 fragments** (batch size is configurable, 1–10). Below the buttons you see the position («Фрагмент 2 з 5 · пакет 1 з 3») (Fragment 2 of 5 · batch 1 of 3) and the start of the current fragment. If **«Пауза після кожного пакета»** (Pause after each batch) is enabled (default), the sound stops after each batch — press **▶** in the mini player or the **«Наступний пакет (X з Y)»** (Next batch (X of Y)) button. Otherwise all batches play one after another without manual intervention.
4. **«Скасувати»** (Cancel) interrupts long synthesis; errors are shown as text, not as an exception.
5. **«Зберегти аудіо…»** (Save audio…) saves the **entire** text from the field into one file: **MP4** (AAC audio without video, default) or **WAV** (24 kHz, mono, 16-bit PCM). The format is chosen in the settings («Формат збереження») (Save format) and stored in `config.json`. Before export, playback stops, progress is shown («Експорт: синтез 3 із 12…») (Export: synthesis 3 of 12…), and **«Скасувати експорт»** (Cancel export) interrupts the operation — no file is created in that case.

> **If MP4 is not created** (the server has no working `ffmpeg`), save as **WAV** — the client assembles this format itself, without conversion on the server.

**Global hotkey `Alt+Q`** does all of this from any window: it captures the selection, activates the window and switches to Quick TTS if needed, then immediately starts speech. The behavior is controlled by the toggles in the «Налаштування» (Settings) tab — «Озвучувати автоматично після Alt+Q» (Auto-speak after Alt+Q), enabled by default, and «Активувати вікно й переходити на Quick TTS» (Activate window and switch to Quick TTS), disabled by default — and by the `autoSynthesizeOnHotkey` / `activateOnHotkey` fields in `config.json`. It fires after **both** keys are released, so the simulated `Ctrl+C` does not get merged with the physical Alt.

> **Wayland:** window activation is best effort; the compositor may refuse to let an app take focus. Capture and speech still work.

> **Terminal limitation:** in terminals, copy is `Ctrl+Shift+C`, and `Ctrl+C` sends SIGINT. So copy the text from a terminal manually (`Ctrl+Shift+C`) and paste it into the Quick TTS field (`Ctrl+V`).

### Mini player (bottom of the window)

Play/Pause, stop, volume slider (step 0.1), the current state (`Idle / Playing / Paused / Stopped`), the name of the active voice, and the position in the queue («Фрагмент 2 з 5 · пакет 1 з 3»). The state is synchronized with the settings tab. When a batch of fragments completes, **▶** starts the next batch; **■** stops the whole queue and resets the counters.

### Settings tab

- **Server address** — a change takes effect **after restarting** the app (the address is fixed at startup for `HttpClient.BaseAddress`).
- **Voice** — choose from 31 voices; the «Оновити голоси» (Refresh voices) button re-reads `GET /api/voices`.
- **Speech rate** (0.5–2.0) and **volume** (0–1) — step 0.1; volume is synchronized with the mini player in both directions.
- **Text chunking** — split mode (paragraphs — default / sentences / up to 250 characters), «Фрагментів у пакеті» (Fragments per batch) (1–10, default 5), and «Пауза після кожного пакета» (Pause after each batch) (enabled by default; turn it off to play batches one after another without stopping).
- **Audio export** — save format (MP4 / WAV).
- **Hotkey** — currently informational only (`Alt+Q`); changing it will come in the next iteration. Checkboxes for auto-start and window activation after `Alt+Q`.
- **«Зберегти»** (Save) writes the config to `~/.config/UA_TTS_Client/config.json` (Linux) / `%APPDATA%\UA_TTS_Client\config.json` (Windows). UI changes take effect immediately; the button only writes them to disk. The server address is the exception: it applies after restart.

### Reader and Character Voice tabs

Placeholders for the next iterations: speech for books/documents and character voice sets.

---

## Project status

Working version after **Steps 0–9**. Iteration 2 is **completed**: **Steps 10–18** are done — documentation, removal of the X11 fallback, new settings, text chunking, the batch playback queue, auto-speech after `Alt+Q`, MP4/WAV audio export, UI toggles, fragment counters and sliders. Manual scenarios were confirmed by the user.

Known limitations: the Windows runtime is not verified (the `TextCopy` path is kept); MP4 export depends on `ffmpeg` on the server (fallback: WAV); window activation on Wayland is best effort; long texts are kept in memory during export.

Details, statuses and the journal are in **`Doc/Current_Work_Plan.md`**. Autotests are deliberately not created in this iteration (Step 9.1 cancelled).

## Documentation

| File | Contents |
|---|---|
| `Doc/Current_Work_Plan.md` | Canonical iteration 2 plan: what to do and in what order |
| `Doc/Project_Structure.md` | Code map: layers, files, DI, threads, extension points |
| `AGENTS.md` | Verified API facts and pitfalls (read before working) |
| `Doc/API.md` | Exact server endpoint contract with examples |
| `Doc/Code_Standards_Prompt.md` | Goals and code standards |
