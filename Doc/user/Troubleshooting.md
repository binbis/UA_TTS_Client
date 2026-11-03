# Troubleshooting and known limitations

> **Purpose:** typical problems with their causes and fixes, and the limits of the current version.
> **Audience:** users.
> **Source of truth:** `reference/Native_Libraries.md`, `server/Server.md`, `architecture/Flows.md`, and the code paths named below.
> **Verified:** 2026-11-03.

## The server is offline («Офлайн»)

- The server is not running, or it was started from a different folder. Start it from `Server_py/`: `./Server_py/run_server.sh` on Linux.
- The first start takes about 45 seconds while the model loads.
- Check the server: `curl -sS http://127.0.0.1:5000/api/health`.
- The address is wrong. Fix `serverUrl` in Settings and restart the client.

## Synthesis fails with a server error

The message shown is the server's error text. Common cases:

- `No voices loaded`: the `voices/` folder is empty or the voice files did not load. Check the server console.
- `No audio generated`: the text had nothing to pronounce.

## The server console shows «Помилка словника» (dictionary error)

The server reads `Server_py/dictionary.json` once at startup. If the JSON is invalid, the server prints `[!] Помилка словника` and applies no dictionary rules. The working copy of the file on the reference machine currently has a trailing comma after the entry `"гвт"`. Remove it, then restart the server. See `server/Server.md`.

## No sound on Linux

- Check that the BASS library is installed: `/usr/lib/libbass.so` must exist (AUR package `libbass`).
- The client plays through the default output device. With PipeWire, `pactl list sink-inputs` shows the client's stream while it plays.
- Check that the right output is the default device in PipeWire.

## Alt+Q captures nothing

- Nothing was selected in the active window.
- The active window is a terminal. Copy with `Ctrl+Shift+C`, then paste with `Ctrl+V` into the field.
- `wl-paste` is missing. Install `wl-clipboard`.
- The session is not Wayland, so `WAYLAND_DISPLAY` is not set. X11 is not supported.
- Keyboard simulation failed. The client falls back to `ydotool`. Check that the `ydotool` daemon runs and that the user is in the `input` group.
- The clipboard did not change within 900 ms. The client then returns empty text instead of the old clipboard content. The same happens when the selected text is identical to the text already in the clipboard, because the two cannot be told apart. Workaround: copy any other text first, then select the text and press `Alt+Q`.

## Alt+Q does nothing at all

- The global hook did not start. The console log should contain the message that the hotkey is active. If it does not, look for an error in the log.
- The user has no access to `/dev/uinput`. Add the user to the `input` group, then log in again.

## The window does not come to the front after Alt+Q

Window activation is best effort. On Wayland the compositor may refuse focus changes. Capture and speech still work.

## MP4 export fails

MP4 is converted on the server with ffmpeg. If ffmpeg does not work, the server returns an error, and the client shows its text. Save as **WAV** instead. WAV needs no conversion.

## Limits to know about

- The server address is read at startup. Changing it needs a restart.
- During export, the whole WAV is kept in memory. Very long texts need more memory.
- The server cuts each internal part at 500 tokens without a warning. The end of a very long sentence can be lost (`server/Server.md`).
- By default playback pauses after each batch. This is intentional. Turn off **«Пауза після кожного пакета»** to play all batches without pauses.
- The Reader and Character Voice tabs are placeholders.
- The hotkey cannot be changed yet.
- Windows 11 has not been run.

## Logs

The client writes its log to the console (the output of `dotnet run`). The server writes its log to its own console.
