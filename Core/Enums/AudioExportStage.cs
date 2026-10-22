#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Етапи експорту озвученого тексту у файл.
/// </summary>
public enum AudioExportStage
{
    /// <summary>Послідовний синтез фрагментів тексту.</summary>
    Synthesizing,

    /// <summary>Склеювання WAV-фрагментів в один потік.</summary>
    Concatenating,

    /// <summary>Конвертація WAV у вибраний формат (MP4).</summary>
    Converting,

    /// <summary>Запис результату на диск.</summary>
    Writing
}
