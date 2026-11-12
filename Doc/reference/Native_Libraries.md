# Native libraries: verified APIs and behavior

> **Purpose:** the real APIs and the verified behavior of ManagedBass, SharpHook, TextCopy and wl-clipboard as this project uses them. Read it before writing code against these packages: they have very little documentation.
> **Audience:** developers and AI agents.
> **Source of truth:** `Infrastructure/Audio/BassAudioPlayerService.cs`, `Infrastructure/Automation/*.cs`, and runs on the reference machine.
> **Verified:** 2026-11-12.

## ManagedBass 4.0.2 (BASS 2.4.18.3)

Only the static class `ManagedBass.Bass` exists. There are no `Playback` or `Channel` classes. Methods used by the project:

```csharp
Bass.Init(int device, int freq, DeviceInitFlags flags, IntPtr win, IntPtr clsid);
Bass.Free();
Bass.CreateStream(byte[] memory, long offset, long length, BassFlags flags);
Bass.ChannelPlay(int handle, bool restart);
Bass.ChannelPause(int handle);              // a toggle; there is no "PlaybackPaused" attribute
Bass.ChannelStop(int handle);
Bass.ChannelSetAttribute(int handle, ChannelAttribute.Volume, float value);
Bass.ChannelSetSync(int handle, SyncFlags.End, 0, SyncProcedure proc, IntPtr user);
Bass.ChannelIsActive(int handle);           // PlaybackState: Stopped, Playing, Stalled, Paused
Bass.StreamFree(int handle);
Bass.LastError;                             // Errors enum
Bass.Volume;                                // global, double
```

Verified rules:

- **Device.** `Bass.NoSoundDevice == 0` is the "No sound" device, and `Bass.DefaultDevice == -1` is the default. On Linux, `Bass.Init(0, …)` gives no sound and no PipeWire stream. The project calls `Bass.Init(Bass.DefaultDevice, 44100, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero)`; the frequency is the constant `DefaultOutputFrequency`.
- **Stream from memory.** `CreateStream(byte[], …)` exists. `BassFlags` has no memory flag. The array must stay alive until playback ends, so the service keeps it in a field.
- **Pause and resume.** `ChannelPause(handle)` is used to pause. `ChannelPlay(handle, false)` resumes.
- **SyncProcedure.** The delegate is `(int syncHandle, int channel, int data, IntPtr user)`. The first argument is the sync handle, not the channel. Compare `channel` with the stream handle. A mistake here produces no error: the automatic stop simply never fires.
- **Natural end.** The `SyncFlags.End` callback runs at the natural end of a stream. `BassAudioPlayerService` then sets the state to `Stopped` and raises `TrackEnded`. `Stop()` and replacing the stream do not raise `TrackEnded`.
- **Native library.** The NuGet package does not contain native binaries. Linux needs `/usr/lib/libbass.so` (the AUR package `libbass`; the `bass` package in the `extra` repository is a game). Windows needs `bass.dll` next to the executable.
- **Disposal.** `Bass.Free()` is called when the service is disposed.
- **Name conflict.** `ManagedBass.PlaybackState` conflicts with the project's `PlaybackState`. `BassAudioPlayerService.cs` uses the alias `using PlaybackState = UA_TTS_Client.Core.Enums.PlaybackState;`.

To check an API that is not documented here, create a small console project outside the repository that references `ManagedBass` 4.0.2, and list the public static members of `ManagedBass.Bass` with reflection.

## SharpHook 8.0.0: global hook and key simulation

```csharp
var hook = new TaskPoolGlobalHook(parallelismLevel: 1, UioHookProvider.Instance);
hook.KeyPressed += (_, e) => { /* e.Data.KeyCode, e.IsEventSimulated */ };
var task = hook.RunAsync();                 // returns at once; completes after Stop()
hook.Stop(); hook.Dispose();

var simulator = SharpHook.Simulation.EventSimulator.Create("UA_TTS_Client", UioHookProvider.Instance);
simulator.Sequence().AddKeyPress(KeyCode.VcLeftControl) /* ... */ .Simulate();
```

- `KeyboardHookEventArgs` is in the namespace `SharpHook`. `KeyboardEventData` is in `SharpHook.Data`.
- Events: `KeyPressed`, `KeyReleased`, `KeyTyped`, `HookEnabled`, `HookDisabled`. `RunAsync(GlobalHookType, bool useBackgroundThread)`.
- `KeyboardEventData` does not report the state of the modifier keys. The project tracks Alt itself.
- `EventSimulator.Create` on Linux opens the uinput devices, so the user needs access to `/dev/uinput` (the `input` group). The creation is expensive, so the project creates the simulator lazily.
- Simulated events come back to the hook with `IsEventSimulated == true`. The project ignores them.
- `SimulateKeyStroke(params KeyCode[])` is an extension method in `SharpHook.Simulation`.

### The Alt+Q hotkey (`SharpHookGlobalHotkeyService`)

- Both Alt keys and `Q` are tracked. Pressing `Q` while Alt is held arms the hotkey. Auto-repeat of `Q` does not make it fire more than once.
- The hotkey is raised only after **both** keys are released. Otherwise the simulated `Ctrl+C` would reach the application as `Ctrl+Alt+C`.
- The event is raised on the thread pool. `StopAsync` waits up to 3 seconds for the hook task.
- If the hook task fails, the error is logged. The application keeps running without the hotkey.

### Selection capture (`WaylandClipboardAutomationService`)

One capture:

1. Read the current clipboard text. This is the baseline.
2. Release `VcLeftAlt` and `VcRightAlt`, press and release `VcC` with `VcLeftControl` held. The sequence is sent with `Sequence().Simulate()`.
3. Poll the clipboard every 50 ms, for at most 900 ms, until the text differs from the baseline. If it never differs, the result is an empty string. Old text is never returned as the capture. This includes a selection that equals the current clipboard text, which cannot be told apart from an unchanged clipboard.

Reading the clipboard:

- **Linux:** `wl-paste --no-newline`, with a 2-second timeout. The service uses it only when `WAYLAND_DISPLAY` is set. Otherwise it logs a warning and returns an empty string. X11 is not supported.
- **Windows:** `TextCopy` 6.2.1, `ClipboardService.GetTextAsync`. This is not called on Linux.

There is no fallback: if SharpHook cannot initialize or the simulation fails, the capture returns an empty string.

Timing: there is no fixed delay after `Ctrl+C`. The clipboard is polled every 50 ms and read again until it changes. This is the rule in `Code_Standards_Prompt.md` (section 1).

Terminal limitation: terminals copy with `Ctrl+Shift+C`. In a terminal, `Ctrl+C` sends SIGINT. Capture from a terminal is not supported.

## TextCopy 6.2.1

Used only on Windows, through `ClipboardService.GetTextAsync`. Any exception other than cancellation returns an empty string. It has not been run on Windows.

## wl-clipboard (`wl-paste`)

Install with `sudo pacman -S wl-clipboard`. `wl-paste` reads the Wayland clipboard; `--no-newline` removes the trailing newline. On the reference machine a 1312-byte selection was read correctly through the full path: selection, `Ctrl+C`, `wl-paste`.
