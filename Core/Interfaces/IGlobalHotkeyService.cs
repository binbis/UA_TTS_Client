#nullable enable

using System;
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Глобальна гаряча клавіша, що працює незалежно від активного вікна.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>Виникає, коли користувач натискає налаштовану гарячу клавішу (<c>Alt+Q</c>).</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Запускає перехоплення клавіатури.
    /// </summary>
    /// <remarks>
    /// Повторний виклик нічого не робить. Якщо хук не вдалося запустити, помилка
    /// записується в лог, а застосунок продовжує працювати без гарячої клавіші.
    /// </remarks>
    void Start();

    /// <summary>
    /// Зупиняє перехоплення клавіатури та звільняє ресурси хука.
    /// </summary>
    Task StopAsync();
}
