#nullable enable

using System;

namespace UA_TTS_Client.Core.Exceptions;

/// <summary>
/// Domain exception describing an error in interaction with the TTS server.
/// </summary>
/// <remarks>
/// The ViewModel layer must not see <see cref="HttpRequestException"/> or
/// <see cref="TaskCanceledException"/>: all transport failures are converted to this type.
/// </remarks>
public sealed class TtsApiException : Exception
{
    /// <summary>HTTP status code of the server response, or <see langword="null"/> if no connection was established.</summary>
    public int? StatusCode { get; }

    /// <summary>Indicates that the server is unavailable (network error or timeout).</summary>
    public bool IsConnectionError => StatusCode is null;

    /// <summary>
    /// Creates an exception for a connection error (server unavailable, timeout).
    /// </summary>
    /// <param name="message">Localized error message.</param>
    /// <param name="innerException">Inner transport exception.</param>
    public TtsApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates an exception for an error that the server returned with a specific HTTP status code.
    /// </summary>
    /// <param name="message">Error message (from the response body, if it could be parsed).</param>
    /// <param name="statusCode">HTTP status code of the response.</param>
    /// <param name="innerException">Inner exception, if any.</param>
    public TtsApiException(string message, int statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
