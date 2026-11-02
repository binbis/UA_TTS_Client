#nullable enable

using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace UA_TTS_Client.ViewModels;

/// <summary>
/// Base class for all application ViewModels.
/// </summary>
/// <remarks>
/// Stores the UI thread's <see cref="SynchronizationContext"/> at creation time so that
/// a ViewModel can safely update properties from background threads (for example, from BASS events).
/// </remarks>
public abstract class ViewModelBase : ObservableObject
{
    private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;

    /// <summary>
    /// Executes an action on the UI thread.
    /// </summary>
    /// <param name="action">Action to execute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
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
    /// Returns the correct Ukrainian form of the word "символ" (symbol) for the given count.
    /// </summary>
    /// <param name="count">Number of symbols.</param>
    /// <returns><c>символ</c>, <c>символи</c>, or <c>символів</c>.</returns>
    protected static string SymbolsWord(int count) => PluralWord(count, "символ", "символи", "символів");

    /// <summary>
    /// Selects the correct Ukrainian noun form for the given count.
    /// </summary>
    /// <param name="count">Number of items.</param>
    /// <param name="one">Form for 1, 21, 31… (for example, <c>фрагмент</c>).</param>
    /// <param name="few">Form for 2–4, 22–24… (for example, <c>фрагменти</c>).</param>
    /// <param name="many">Form for 0, 5–20, 11–14… (for example, <c>фрагментів</c>).</param>
    /// <returns>The matching noun form.</returns>
    protected static string PluralWord(int count, string one, string few, string many)
    {
        var lastTwo = count % 100;
        if (lastTwo is >= 11 and <= 14)
        {
            return many;
        }

        return (count % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }
}
