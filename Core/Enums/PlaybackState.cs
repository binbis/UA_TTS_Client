#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Стан відтворення аудіо в застосунку.
/// </summary>
/// <remarks>
/// Це власний доменний enum: він навмисно не залежить від <c>ManagedBass.PlaybackState</c>,
/// щоб шар <c>Core</c> залишався вільним від зовнішніх бібліотек.
/// </remarks>
public enum PlaybackState
{
    /// <summary>Нічого не завантажено, відтворення ще не починалося.</summary>
    Idle,

    /// <summary>Аудіо відтворюється.</summary>
    Playing,

    /// <summary>Відтворення призупинено; позиція збережена.</summary>
    Paused,

    /// <summary>Відтворення зупинено користувачем або трек дослухано до кінця.</summary>
    Stopped,

    /// <summary>Сталася помилка аудіопідсистеми (наприклад, не вдалося ініціалізувати пристрій).</summary>
    Error
}
