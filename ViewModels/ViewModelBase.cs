#nullable enable

using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UA_TTS_Client.ViewModels;

/// <summary>
/// Базовий клас усіх ViewModel застосунку.
/// </summary>
/// <remarks>
/// Запам'ятовує <see cref="SynchronizationContext"/> UI-потоку під час створення, щоб
/// ViewModel міг безпечно оновлювати властивості з фонових потоків (наприклад, із подій BASS).
/// </remarks>
public abstract class ViewModelBase : ObservableObject
{
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    /// <summary>
    /// Виконує дію в UI-потоці.
    /// </summary>
    /// <param name="action">Дія для виконання.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> дорівнює <see langword="null"/>.</exception>
    protected void OnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
        {
            action();
            return;
        }

        _uiContext.Post(static state => ((Action)state!)(), action);
    }

    /// <summary>
    /// Повертає правильну українську форму слова «символ» для заданої кількості.
    /// </summary>
    /// <param name="count">Кількість символів.</param>
    /// <returns><c>символ</c>, <c>символи</c> або <c>символів</c>.</returns>
    protected static string SymbolsWord(int count)
    {
        var lastTwo = count % 100;
        if (lastTwo is >= 11 and <= 14)
        {
            return "символів";
        }

        return (count % 10) switch
        {
            1 => "символ",
            2 or 3 or 4 => "символи",
            _ => "символів"
        };
    }
}
