#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Infrastructure.Storage;

/// <summary>
/// Зберігає налаштування у JSON-файл у каталозі конфігурації користувача.
/// </summary>
/// <remarks>
/// Шлях за замовчуванням: <c>~/.config/UA_TTS_Client/config.json</c> (Linux) або
/// <c>%APPDATA%\UA_TTS_Client\config.json</c> (Windows). Запис атомарний: спочатку
/// дані пишуться в тимчасовий файл у тому ж каталозі, потім перейменовуються.
/// </remarks>
public sealed class JsonSettingsService : ISettingsService
{
    private const string AppFolderName = "UA_TTS_Client";
    private const string ConfigFileName = "config.json";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ILogger<JsonSettingsService> _logger;
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Створює сервіс, що працює зі стандартним шляхом конфігурації.
    /// </summary>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> дорівнює <see langword="null"/>.</exception>
    public JsonSettingsService(ILogger<JsonSettingsService> logger)
        : this(logger, GetDefaultFilePath())
    {
    }

    /// <summary>
    /// Створює сервіс із явно заданим шляхом до файлу (використовується в тестах).
    /// </summary>
    /// <param name="logger">Логер.</param>
    /// <param name="filePath">Повний шлях до JSON-файлу конфігурації.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> порожній.</exception>
    public JsonSettingsService(ILogger<JsonSettingsService> logger, string filePath)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _logger = logger;
        _filePath = filePath;
    }

    /// <summary>
    /// Повертає стандартний шлях до файлу конфігурації для поточної платформи.
    /// </summary>
    /// <returns>Повний шлях <c>.../UA_TTS_Client/config.json</c>.</returns>
    public static string GetDefaultFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppFolderName, ConfigFileName);
    }

    /// <inheritdoc />
    public async Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("Файл конфігурації {Path} відсутній — використовуються значення за замовчуванням", _filePath);
                return new AppConfig();
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions);

            if (config is null)
            {
                _logger.LogWarning("Файл конфігурації {Path} порожній — використовуються значення за замовчуванням", _filePath);
                return new AppConfig();
            }

            _logger.LogDebug("Конфігурацію завантажено з {Path}", _filePath);
            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Не вдалося прочитати конфігурацію {Path} — використовуються значення за замовчуванням", _filePath);
            return new AppConfig();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, SerializerOptions);
            var tempPath = string.Concat(_filePath, ".", Guid.NewGuid().ToString("N"), ".tmp");

            try
            {
                await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Не вдалося видалити тимчасовий файл {Path}", tempPath);
                    }
                }
            }

            _logger.LogDebug("Конфігурацію збережено в {Path}", _filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Не вдалося зберегти конфігурацію в {Path}", _filePath);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }
}
