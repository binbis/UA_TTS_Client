#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Стан доступності TTS-сервера.
/// </summary>
public enum ServerStatus
{
    /// <summary>Перевірка ще не виконувалася.</summary>
    Unknown,

    /// <summary>Сервер відповідає на запити.</summary>
    Online,

    /// <summary>Сервер не відповідає (мережа, порт або процес не запущено).</summary>
    Offline,

    /// <summary>Сервер відповів, але з помилкою (некоректна відповідь, 5xx тощо).</summary>
    Error
}
