#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using UA_TTS_Client.ViewModels;

namespace UA_TTS_Client;

/// <summary>
/// Знаходить View для ViewModel за конвенцією імен: <c>ViewModels.Tabs.FooViewModel</c>
/// перетворюється на <c>Views.Tabs.FooView</c>.
/// </summary>
/// <remarks>
/// Окремо обробляється головне вікно: <c>MainWindowViewModel</c> за конвенцією дав би
/// <c>MainWindowView</c>, якого не існує, тому додатково пробується <c>MainWindow</c>.
/// </remarks>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    /// <inheritdoc />
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var viewModelType = param.GetType();

        foreach (var candidate in EnumerateViewTypeNames(viewModelType))
        {
            var viewType = Type.GetType(candidate);
            if (viewType is not null && typeof(Control).IsAssignableFrom(viewType))
            {
                return (Control)Activator.CreateInstance(viewType)!;
            }
        }

        return new TextBlock { Text = "Not Found: " + viewModelType.FullName };
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is ViewModelBase;

    /// <summary>
    /// Генерує можливі повні імена View у порядку пріоритету.
    /// </summary>
    private static IEnumerable<string> EnumerateViewTypeNames(Type viewModelType)
    {
        var fullName = viewModelType.FullName;
        if (fullName is null)
        {
            yield break;
        }

        var viewName = fullName
            .Replace(".ViewModels", ".Views", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        yield return viewName;

        // MainWindowViewModel -> Views.MainWindowView, але реальний клас — Views.MainWindow.
        if (viewName.EndsWith("WindowView", StringComparison.Ordinal))
        {
            yield return viewName[..^"View".Length];
        }
    }
}
