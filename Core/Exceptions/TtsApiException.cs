#nullable enable

using System;

namespace UA_TTS_Client.Core.Exceptions;

/// <summary>
/// Доменний виняток, що описує помилку взаємодії з TTS-сервером.
/// </summary>
/// <remarks>
/// ViewModel-шар не повинен бачити <see cref="HttpRequestException"/> або
/// <see cref="TaskCanceledException"/>: усі збої транспорту перетворюються на цей тип.
/// </remarks>
public sealed class TtsApiException : Exception
{
    /// <summary>HTTP-код відповіді сервера або <see langword="null"/>, якщо з'єднання не відбулося.</summary>
    public int? StatusCode { get; }

    /// <summary>Ознака того, що сервер недоступний (мережева помилка або таймаут).</summary>
    public bool IsConnectionError => StatusCode is null;

    /// <summary>
    /// Створює виняток для помилки з'єднання (сервер недоступний, таймаут).
    /// </summary>
    /// <param name="message">Локалізоване повідомлення про помилку.</param>
    /// <param name="innerException">Внутрішній виняток транспорту.</param>
    public TtsApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Створює виняток для помилки, яку повернув сервер із певним HTTP-кодом.
    /// </summary>
    /// <param name="message">Повідомлення про помилку (з тіла відповіді, якщо вдалося його розпізнати).</param>
    /// <param name="statusCode">HTTP-код відповіді.</param>
    /// <param name="innerException">Внутрішній виняток, якщо є.</param>
    public TtsApiException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
