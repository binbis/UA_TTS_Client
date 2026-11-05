# Manual test scenarios

> **Purpose:** the checklist of manual verification for the client. The project has no test project by decision (`Roadmap.md`), so these scenarios are the regression check after a behavior change.
> **Audience:** developers and AI agents who verify a change.
> **Source of truth:** the behavior described in `architecture/Flows.md` and `user/User_Guide.md`.
> **Verified:** 2026-11-03. The scenarios were confirmed by the user on the reference machine when they were first written.

## How to use this checklist

1. Start the server from `Server_py/`, wait until `/api/health` answers, then start the client (`dotnet run`).
2. Run the scenarios that touch the changed area. After large changes, run all of them.
3. Scenarios 1–3 and 13 need a **physical** `Alt+Q` press: the hook ignores simulated events.
4. Keep the console output open: most failures are logged there.

## Scenarios

| # | Scenario | Expected result |
|---|---|---|
| 1 | `Alt+Q` in a browser, auto-speech on | Text is captured; the first batch plays without pressing «Озвучити» |
| 2 | `Alt+Q` with activation on | Text is captured first, then the window is active, the Quick TTS tab is open, sound plays |
| 3 | Activation off | The window does not take focus, but the text is captured and spoken |
| 4 | Empty selection | Status «Нічого не захоплено», the window does not activate |
| 5 | «Абзаци» (Paragraphs) mode | Fragments follow the paragraphs; long paragraphs are split further |
| 6 | «Речення» (Sentences) mode | One fragment is one sentence; no sentence is cut |
| 7 | «До 250 символів» (Up to 250) mode | No fragment is longer than 250 characters, except where no split is possible |
| 8 | Batch size 5, text of 12 fragments | Batch 1 (fragments 1–5) plays, then a pause; ▶ or «Наступний пакет» starts 6–10; then 11–12 |
| 9 | Batch size 1 and batch size 10 | Batch boundaries are correct; the counters stay in sync |
| 10 | Pause in the middle of a fragment | Sound stops; ▶ resumes from the same position |
| 11 | Pause between fragments | The next fragment does not start until ▶ |
| 12 | Stop during a batch | Sound and queue stop; the counters reset |
| 13 | New `Alt+Q` during playback | The previous queue stops; the new one starts |
| 14 | Sliders | Step 0.1; values such as `1.0` and `0.7`, never `0.30000000000000004` |
| 15 | No X11 tools on `PATH`, Wayland session | Capture through `wl-paste` works; no fallback to X11 is attempted |
| 16 | Old `config.json` without the new fields | Loads; the new fields have their default values |
| 17 | Restart | New settings are saved and applied |
| 18 | MP4 export (default) | After choosing a path, an `.mp4` is created that plays in a media player; the sound is the whole text |
| 19 | WAV export | A valid WAV is created (24 kHz, mono, 16-bit); the duration is close to the sum of the fragments |
| 20 | Export cancelled | No temporary or target file remains; status «Експорт скасовано» |
| 21 | Export during playback | Playback stops, the export starts, and the file is in place when it finishes |
| 22 | Export of a text with one fragment | MP4 and WAV are created correctly, with no empty headers |
| 23 | MP4 conversion fails (for example, no ffmpeg on the server) | The server error is shown; WAV export still works |
| 24 | «Озвучити» (Speak) with the text `У 2026 році ми пройшли 5 км. Курс 40,50 грн, знижка 50%.` | The numbers are spoken as words, for example «дві тисячі двадцять шостому році», «п'ять кілометрів», «сорок гривень п'ятдесят копійок», «п'ятдесят відсотків». No number is skipped |
| 25 | MP4 export of the text from scenario 24 | The sound of the file contains the same number words |

## Checks before a release

- `dotnet build` reports 0 warnings and 0 errors.
- The server log shows no `[!] Помилка словника` (dictionary error) at startup.
- The server console shows no `[!] Модель пропускає символи` line during scenarios 24–25.
- Settings: change a value, press «Зберегти», restart the client; the value is still there.
- A physical `Alt+Q` press works after a fresh start of the client.
- The documents in `Doc/` that describe the changed behavior are updated, and their `Verified` dates are refreshed.
