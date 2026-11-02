#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using UA_TTS_Client.ViewModels;

namespace UA_TTS_Client;

/// <summary>
/// Finds the View for a ViewModel by naming convention: <c>ViewModels.Tabs.FooViewModel</c>
/// is mapped to <c>Views.Tabs.FooView</c>.
/// </summary>
/// <remarks>
/// The main window is handled separately: by convention, <c>MainWindowViewModel</c> would yield
/// <c>MainWindowView</c>, which does not exist, so <c>MainWindow</c> is also tried.
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
    /// Generates the possible fully qualified View names in priority order.
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

        // MainWindowViewModel -> Views.MainWindowView, but the real class is Views.MainWindow.
        if (viewName.EndsWith("WindowView", StringComparison.Ordinal))
        {
            yield return viewName[..^"View".Length];
        }
    }
}
