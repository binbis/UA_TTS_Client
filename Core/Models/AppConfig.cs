#nullable enable

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
}
