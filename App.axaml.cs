#nullable enable

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;
using UA_TTS_Client.Infrastructure.Api;
using UA_TTS_Client.Infrastructure.Audio;
using UA_TTS_Client.Infrastructure.Automation;
using UA_TTS_Client.Infrastructure.Playback;
using UA_TTS_Client.Infrastructure.Storage;
using UA_TTS_Client.Infrastructure.Text;
using UA_TTS_Client.ViewModels;
using UA_TTS_Client.ViewModels.Components;
using UA_TTS_Client.ViewModels.Tabs;
using UA_TTS_Client.Views;

namespace UA_TTS_Client;

/// <summary>
/// Точка композиції застосунку: тут реєструються всі сервіси та створюється головне вікно.
/// </summary>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            var provider = services.BuildServiceProvider();
            var viewModel = provider.GetRequiredService<MainWindowViewModel>();

            var window = new MainWindow { DataContext = viewModel };
            window.Opened += async (_, _) => await viewModel.InitializeAsync();

            // Alt+Q: показати й активувати вікно після успішного захоплення тексту.
            viewModel.ActivateRequested += (_, _) =>
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Activate();
            };

            desktop.MainWindow = window;
            desktop.Exit += (_, _) =>
            {
                viewModel.Dispose();
                provider.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Реєструє всі залежності застосунку.
    /// </summary>
    /// <remarks>
    /// Конфігурація завантажується синхронно до побудови контейнера: адреса сервера потрібна
    /// для <see cref="HttpClient.BaseAddress"/>. Зміна адреси у вкладці налаштувань
    /// застосовується після перезапуску.
    /// </remarks>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder
            .AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            })
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));

        var startupConfig = LoadStartupConfig();
        services.AddSingleton(startupConfig);

        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IAudioPlayerService, BassAudioPlayerService>();
        services.AddSingleton<IClipboardAutomationService, WaylandClipboardAutomationService>();
        services.AddSingleton<IGlobalHotkeyService, SharpHookGlobalHotkeyService>();
        services.AddSingleton<ITextChunker, SentenceAwareTextChunker>();
        services.AddSingleton<IFragmentPlaybackService, FragmentPlaybackService>();

        var baseAddress = Uri.TryCreate(startupConfig.ServerUrl, UriKind.Absolute, out var parsed)
            ? parsed
            : new Uri(AppConfig.DefaultServerUrl, UriKind.Absolute);

        services.AddHttpClient<ITtsApiClient, TtsApiClient>(client =>
        {
            client.BaseAddress = baseAddress;
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddSingleton<PlaybackControlViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<QuickTtsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
    }

    /// <summary>
    /// Завантажує конфігурацію синхронно на старті.
    /// </summary>
    /// <returns>Збережений конфіг або значення за замовчуванням.</returns>
    private static AppConfig LoadStartupConfig()
    {
        try
        {
            var settingsService = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance);
            return settingsService.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // Пошкоджений або недоступний файл не повинен заважати запуску.
            return new AppConfig();
        }
    }
}
