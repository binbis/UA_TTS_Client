#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Infrastructure.Api;

/// <summary>
/// HTTP-клієнт TTS-сервера (<c>Flask + StyleTTS2</c>).
/// </summary>
/// <remarks>
/// Клієнт розрахований на типізовану реєстрацію:
/// <c>services.AddHttpClient&lt;ITtsApiClient, TtsApiClient&gt;(c =&gt; c.BaseAddress = ...)</c>.
/// Помилки транспорту перетворюються на <see cref="TtsApiException"/>, а тіло помилки
/// сервера читається як JSON <c>{"error": "..."}</c> або як plain text.
/// </remarks>
public sealed class TtsApiClient : ITtsApiClient
{
    private static readonly TimeSpan MinimumTimeout = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<TtsApiClient> _logger;

    /// <summary>
    /// Створює клієнт поверх налаштованого <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="httpClient">HTTP-клієнт із заданою базовою адресою сервера.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    public TtsApiClient(HttpClient httpClient, ILogger<TtsApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Генерація великого тексту може тривати довго; 100 с за замовчуванням замало.
        if (_httpClient.Timeout < MinimumTimeout)
        {
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/voices", content: null, cancellationToken)
            .ConfigureAwait(false);

        var voices = await ReadJsonAsync<List<string>>(response, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Отримано {Count} голосів із сервера", voices.Count);
        return voices;
    }

    /// <inheritdoc />
    public async Task<ServerHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Get, "api/health", content: null, cancellationToken)
                .ConfigureAwait(false);

            var health = await ReadJsonAsync<ServerHealth>(response, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Стан сервера: {Status}, пристрій {Device}, голосів {Voices}",
                health.Status, health.Device ?? "?", health.Voices);
            return health;
        }
        catch (TtsApiException ex) when (ex.StatusCode == 404)
        {
            // Старий сервер без /api/health: перевіряємо доступність через /api/voices.
            _logger.LogInformation("Ендпоінт /api/health відсутній — перевіряю доступність через /api/voices");
            var voices = await GetVoicesAsync(cancellationToken).ConfigureAwait(false);
            return new ServerHealth
            {
                Status = "ok",
                Voices = voices.Count
            };
        }
    }

    /// <inheritdoc />
    public async Task<string> VerbalizeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var request = new VerbalizeRequestDto { Text = text };
        using var response = await SendAsync(HttpMethod.Post, "api/verbalize",
                JsonContent.Create(request, options: SerializerOptions), cancellationToken)
            .ConfigureAwait(false);

        var result = await ReadJsonAsync<TextResponseDto>(response, cancellationToken).ConfigureAwait(false);
        return result.Text ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<byte[]> SynthesizeAsync(TtsRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Text);

        using var response = await SendAsync(HttpMethod.Post, "api/synthesize",
                JsonContent.Create(request, options: SerializerOptions), cancellationToken)
            .ConfigureAwait(false);

        var audio = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (audio.Length == 0)
        {
            throw new TtsApiException("Сервер повернув порожній аудіофайл.", (int)response.StatusCode);
        }

        _logger.LogDebug("Синтезовано {Bytes} байт аудіо для голосу {Voice}", audio.Length, request.Voice ?? "(типового)");
        return audio;
    }

    /// <inheritdoc />
    public async Task<string> ParseFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", fileName);

        using var response = await SendAsync(HttpMethod.Post, "api/parse_file", form, cancellationToken)
            .ConfigureAwait(false);

        var result = await ReadJsonAsync<TextResponseDto>(response, cancellationToken).ConfigureAwait(false);
        return result.Text ?? string.Empty;
    }

    /// <summary>
    /// Надсилає запит і перетворює неуспішну відповідь на <see cref="TtsApiException"/>.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativePath,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativePath) { Content = content };

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Сервер TTS недоступний ({Method} {Path})", method, relativePath);
            throw new TtsApiException(
                "Сервер TTS недоступний. Перевірте, чи його запущено, та правильність адреси.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Сервер TTS не відповів вчасно ({Method} {Path})", method, relativePath);
            throw new TtsApiException("Сервер TTS не відповів вчасно. Спробуйте коротший текст.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var statusCode = (int)response.StatusCode;
        var fallback = $"Сервер повернув помилку {statusCode} ({response.ReasonPhrase}).";

        var message = await ReadErrorMessageAsync(response, cancellationToken).ConfigureAwait(false);
        response.Dispose();

        _logger.LogWarning("Помилка сервера {StatusCode}: {Message}", statusCode, message ?? fallback);
        throw new TtsApiException(message ?? fallback, statusCode);
    }

    /// <summary>
    /// Читає тіло помилки: спершу намагається розібрати JSON <c>{"error"}</c>,
    /// інакше повертає plain text (сервер історично відповідав саме так).
    /// </summary>
    /// <returns>Текст помилки або <see langword="null"/>, якщо тіло непридатне.</returns>
    private static async Task<string?> ReadErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or ObjectDisposedException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        body = body.Trim();
        if (body.StartsWith('{'))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<ApiErrorResponse>(body, SerializerOptions);
                if (!string.IsNullOrWhiteSpace(dto?.Error))
                {
                    return dto.Error;
                }
            }
            catch (JsonException)
            {
                // Не JSON — впадемо в обробку plain text нижче.
            }
        }

        // Plain text (наприклад, "No file"); HTML-сторінки помилок не показуємо користувачу.
        if (body.StartsWith('<'))
        {
            return null;
        }

        return body.Length <= 500 ? body : body[..500];
    }

    /// <summary>
    /// Десеріалізує JSON-відповідь або кидає <see cref="TtsApiException"/>.
    /// </summary>
    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var value = await JsonSerializer
                .DeserializeAsync<T>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return value ?? throw new TtsApiException("Сервер повернув порожню відповідь.", (int)response.StatusCode);
        }
        catch (JsonException ex)
        {
            throw new TtsApiException("Сервер повернув некоректну відповідь (очікувався JSON).", (int)response.StatusCode, ex);
        }
    }
}
