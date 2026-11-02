#nullable enable

namespace UA_TTS_Client.Core.Models;

/// <summary>
/// A text fragment that is synthesized and played as a separate unit.
/// </summary>
/// <param name="Index">Sequential number of the fragment, starting from 1.</param>
/// <param name="Text">Text of the fragment.</param>
public sealed record TextFragment(int Index, string Text);
