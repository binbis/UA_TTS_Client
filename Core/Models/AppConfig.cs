#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Application settings stored on disk in <c>config.json</c>.
/// </summary>
public sealed record AppConfig
{
    /// <summary>Default TTS server address.</summary>
    public const string DefaultServerUrl = "http://127.0.0.1:5000";

    /// <summary>Default hotkey.</summary>
    public const string DefaultHotkey = "Alt+Q";

    /// <summary>Base TTS server address, for example <c>http://127.0.0.1:5000</c>.</summary>
    public string ServerUrl { get; init; } = DefaultServerUrl;

    /// <summary>Name of the selected voice, or <see langword="null"/> if no voice has been selected yet.</summary>
    public string? SelectedVoice { get; init; }

    /// <summary>Speech speed passed to the server (1.0 is normal).</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Playback volume in the range from 0.0 to 1.0.</summary>
    public double Volume { get; init; } = 1.0;

    /// <summary>Hotkey for capturing the selected text.</summary>
    public string Hotkey { get; init; } = DefaultHotkey;

    /// <summary>Whether to start speech automatically after capturing text with <c>Alt+Q</c>.</summary>
    public bool AutoSynthesizeOnHotkey { get; init; } = true;

    /// <summary>Whether to activate the window and switch to Quick TTS after <c>Alt+Q</c>.</summary>
    public bool ActivateOnHotkey { get; init; }

    /// <summary>Text chunking mode: how text is split into fragments (default: paragraphs).</summary>
    public TextChunkingMode ChunkingMode { get; init; } = TextChunkingMode.Paragraphs;

    /// <summary>Number of fragments in a batch (from 1 to 10).</summary>
    public int BatchSize { get; init; } = SpeechQueueOptions.DefaultBatchSize;

    /// <summary>
    /// Whether to pause playback after each batch (default) or play all batches one after another.
    /// </summary>
    public bool PauseAfterEachBatch { get; init; } = true;

    /// <summary>Format for saving the spoken text.</summary>
    public AudioExportFormat ExportFormat { get; init; } = AudioExportFormat.Mp4;
}
