#nullable enable

using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// Налаштування застосунку, що зберігаються на диску у <c>config.json</c>.
/// </summary>
public sealed record AppConfig
{
    /// <summary>Адреса TTS-сервера за замовчуванням.</summary>
    public const string DefaultServerUrl = "http://127.0.0.1:5000";

    /// <summary>Гаряча клавіша за замовчуванням.</summary>
    public const string DefaultHotkey = "Alt+Q";

    /// <summary>Базова адреса TTS-сервера, наприклад <c>http://127.0.0.1:5000</c>.</summary>
    public string ServerUrl { get; init; } = DefaultServerUrl;

    /// <summary>Ім'я вибраного голосу або <see langword="null"/>, якщо голос ще не вибрано.</summary>
    public string? SelectedVoice { get; init; }

    /// <summary>Швидкість мовлення, що передається серверу (1.0 — звичайна).</summary>
    public double Speed { get; init; } = 1.0;

    /// <summary>Гучність відтворення в діапазоні від 0.0 до 1.0.</summary>
    public double Volume { get; init; } = 1.0;

    /// <summary>Гаряча клавіша захоплення виділеного тексту.</summary>
    public string Hotkey { get; init; } = DefaultHotkey;

    /// <summary>Чи запускати озвучення автоматично після захоплення тексту за <c>Alt+Q</c>.</summary>
    public bool AutoSynthesizeOnHotkey { get; init; } = true;

    /// <summary>Чи активувати вікно й перемикатися на Quick TTS після <c>Alt+Q</c>.</summary>
    public bool ActivateOnHotkey { get; init; }

    /// <summary>Режим поділу тексту на фрагменти (типово — абзаци).</summary>
    public TextChunkingMode ChunkingMode { get; init; } = TextChunkingMode.Paragraphs;

    /// <summary>Кількість фрагментів у пакеті (від 1 до 10).</summary>
    public int BatchSize { get; init; } = SpeechQueueOptions.DefaultBatchSize;

    /// <summary>
    /// Чи зупиняти відтворення після кожного пакета (типово) або грати всі пакети підряд.
    /// </summary>
    public bool PauseAfterEachBatch { get; init; } = true;

    /// <summary>Формат збереження озвученого тексту.</summary>
    public AudioExportFormat ExportFormat { get; init; } = AudioExportFormat.Mp4;
}
