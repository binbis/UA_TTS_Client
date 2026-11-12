# Roadmap and backlog

> **Purpose:** open items, out-of-scope decisions and known future work. This is the only place for the backlog. Plans for individual tasks are not stored in the repository.
> **Audience:** developers and AI agents who choose the next task.
> **Source of truth:** this file for the backlog. Facts about current behavior live in the owner documents (see `Doc/README.md`).
> **Verified:** 2026-11-12.

## Out of scope (decided)

| Item | Status | Where the current state is described |
|---|---|---|
| Test project (xUnit, FluentAssertions, NSubstitute) | Not created. Verification is manual scenarios and temporary harnesses outside the repository | `development/Manual_Test_Scenarios.md`, `Code_Standards_Prompt.md` (section 5, for the future) |
| Reader tab (text from files) | Placeholder. `ITtsApiClient.ParseFileAsync` and `POST /api/parse_file` exist, but the UI does not use them yet | `ViewModels/Tabs/ReaderViewModel.cs`, `server/API.md` |
| Character Voice tab | Placeholder, no functionality | `ViewModels/Tabs/CharacterVoiceViewModel.cs` |
| Hotkey editor | Not implemented. `AppConfig.Hotkey` is stored but the hook always uses `Alt+Q` | `user/Configuration.md`, `development/Extension_Points.md` |
| Server or API changes | Limited to the speech text: verbalization and the order of dictionary rules in `server.py`. No new endpoints; the client uses the existing endpoints only | `server/Server.md`, `server/API.md` |
| Video track in audio export | Not planned. The MP3 export contains audio only | `architecture/Overview.md` |
| Automatic continuation of batches | Available as the `pauseAfterEachBatch = false` setting. No further automation planned | `architecture/Flows.md` |
| Currency and magnitude abbreviations in the dictionary (`грн`, `млн`, `млрд`) | Applied after verbalization, so the verbalizer reads them with the number. Moving them before verbalization gives a wrong reading, for example `40,50 грн` is read as a decimal | `server/Server.md` |

## Open items

1. **Windows 11 runtime.** Not verified: the TextCopy clipboard path, `bass.dll` next to the executable, the CUDA build of torch on Windows.
2. **Long texts in export.** The whole WAV is kept in memory (about 48 KB of audio per second). Streaming to a temporary file is a possible optimization.
3. **Server truncation.** The server cuts each part at 500 tokens and writes a console warning. Splitting the part further is still open (`server/Server.md`).
4. **Dictionary errors.** An invalid `dictionary.json` only prints a console message and the dictionary is ignored. A clearer signal would help (`server/Server.md`).
5. **Identical clipboard text.** A selection that equals the current clipboard text is not detected, so the capture returns nothing (`user/Troubleshooting.md`). Options: clear the clipboard before the copy, which replaces the user's clipboard content, or copy a marker first. Decide before implementing.
6. **Naming.** `WaylandClipboardAutomationService` also runs on Windows. A rename is optional.
7. **ydotool fallback.** Kept and documented as optional. Re-test `Alt+Q` in a live session before relying on it.
8. **Verbalizer errors.** The known reading errors are listed in `server/Server.md`: ranges with «з», compound ordinals, `4а`, `Гб`, phone numbers with a leading zero. Fix each one by a rule in the text preparation, or by a dictionary entry for a word (not for currency or magnitude abbreviations, see the table above).
9. **Hotkey configuration.** Make the hotkey configurable when the hotkey editor is needed. The hook logic must read the key from the settings.
