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
using UA_TTS_Client.Views.Services;

namespace UA_TTS_Client;

/// <summary>
/// Composition root of the application: all services are registered here and the main window is created.
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

            // Alt+Q: show and activate the window after a successful text capture.
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
    /// Registers all application dependencies.
    /// </summary>
    /// <remarks>
    /// The configuration is loaded synchronously before the container is built: the server
    /// address is required for <see cref="HttpClient.BaseAddress"/>. Changing the address
    /// in the settings tab takes effect after a restart.
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
        services.AddSingleton<IAudioExportService, AudioExportService>();
        services.AddSingleton<IFileSaveDialogService, AvaloniaFileSaveDialogService>();

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
    /// Loads the configuration synchronously at startup.
    /// </summary>
    /// <returns>The saved config, or the default values.</returns>
    private static AppConfig LoadStartupConfig()
    {
        try
        {
            var settingsService = new JsonSettingsService(NullLogger<JsonSettingsService>.Instance);
            return settingsService.LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            // A corrupted or inaccessible file must not prevent startup.
            return new AppConfig();
        }
    }
}
