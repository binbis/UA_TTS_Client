#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Infrastructure.Text;

/// <summary>
/// Splits text into fragments, respecting paragraph and sentence boundaries.
/// </summary>
/// <remarks>
/// <para>Modes: paragraphs, sentences, or packing sentences up to 250 characters.</para>
/// <para>A sentence is not cut in the middle if it fits within the limit.
/// An overly long sentence is cut at the last space/comma before the limit.</para>
/// </remarks>
public sealed partial class SentenceAwareTextChunker : ITextChunker
{
    /// <summary>Fragments shorter than this length are merged into their neighbors when possible.</summary>
    private const int MinFragmentLength = 20;

    /// <summary>Abbreviations after which a period does not end a sentence.</summary>
    private static readonly string[] Abbreviations =
    [
        "т.д.", "т.п.", "і т.ін.", "р.", "м.", "вул.", "ім.", "проф.", "акад.", "див.", "напр."
    ];

    /// <inheritdoc />
    public IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var maxLength = options.MaxChunkLength > 0
            ? options.MaxChunkLength
            : TextChunkingOptions.DefaultMaxChunkLength;

        var normalized = Normalize(text);
        if (normalized.Length == 0)
        {
            return [];
        }

        var paragraphs = SplitParagraphs(normalized);
        var rawFragments = options.Mode switch
        {
            TextChunkingMode.Paragraphs => BuildParagraphFragments(paragraphs, maxLength),
            TextChunkingMode.Sentences => BuildSentenceFragments(paragraphs, maxLength),
            _ => BuildMaxLengthFragments(paragraphs, maxLength)
        };

        // In the "Речення" (Sentences) mode, short sentences are not merged: one fragment = one sentence.
        var merged = options.Mode == TextChunkingMode.Sentences
            ? rawFragments
            : MergeShortFragments(rawFragments, maxLength);

        return ToTextFragments(merged);
    }

    /// <summary>
    /// Collapses whitespace, normalizes line breaks, and removes empty lines.
    /// </summary>
    private static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousWasNewline = false;

        for (var i = 0; i < text.Length; i++)
        {
            var symbol = text[i];

            if (symbol == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    continue;
                }

                symbol = '\n';
            }

            if (symbol == '\n')
            {
                if (previousWasNewline)
                {
                    continue;
                }

                builder.Append('\n');
                previousWasNewline = true;
                continue;
            }

            if (char.IsWhiteSpace(symbol))
            {
                builder.Append(' ');
                previousWasNewline = false;
                continue;
            }

            builder.Append(symbol);
            previousWasNewline = false;
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    /// <summary>Splits the normalized text into paragraphs.</summary>
    private static List<string> SplitParagraphs(string text)
    {
        var paragraphs = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var paragraph = raw.Trim();
            if (paragraph.Length > 0)
            {
                paragraphs.Add(paragraph);
            }
        }

        return paragraphs;
    }

    /// <summary>
    /// Splits a paragraph into sentences without breaking known abbreviations and initials.
    /// </summary>
    private static List<string> SplitSentences(string paragraph)
    {
        var parts = SentenceBoundaryRegex().Split(paragraph);
        var sentences = new List<string>(parts.Length);
        var current = new StringBuilder();

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (current.Length == 0)
            {
                current.Append(trimmed);
                continue;
            }

            if (EndsWithAbbreviation(current.ToString()))
            {
                current.Append(' ').Append(trimmed);
                continue;
            }

            sentences.Add(current.ToString().Trim());
            current.Clear();
            current.Append(trimmed);
        }

        if (current.Length > 0)
        {
            sentences.Add(current.ToString().Trim());
        }

        return sentences;
    }

    /// <summary>Whether the text ends with an abbreviation or an initial.</summary>
    private static bool EndsWithAbbreviation(string value)
    {
        var trimmed = value.TrimEnd();

        foreach (var abbreviation in Abbreviations)
        {
            if (!trimmed.EndsWith(abbreviation, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var start = trimmed.Length - abbreviation.Length;
            if (start == 0 || !char.IsLetter(trimmed[start - 1]))
            {
                return true;
            }
        }

        var lastSpace = trimmed.LastIndexOf(' ');
        var lastWord = lastSpace >= 0 ? trimmed[(lastSpace + 1)..] : trimmed;
        if (lastWord.Length != 2 || lastWord[1] != '.')
        {
            return false;
        }

        return lastWord[0] is (>= 'А' and <= 'Я') or 'І' or 'Ї' or 'Є' or 'Ґ'
            or (>= 'A' and <= 'Z');
    }

    /// <summary>Fragments by paragraph; overly long paragraphs are split by sentence.</summary>
    private static List<string> BuildParagraphFragments(List<string> paragraphs, int maxLength)
    {
        var result = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length <= maxLength)
            {
                result.Add(paragraph);
                continue;
            }

            result.AddRange(PackSentences(SplitSentences(paragraph), maxLength));
        }

        return result;
    }

    /// <summary>One sentence per fragment.</summary>
    private static List<string> BuildSentenceFragments(List<string> paragraphs, int maxLength)
    {
        var result = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            foreach (var sentence in SplitSentences(paragraph))
            {
                if (sentence.Length <= maxLength)
                {
                    result.Add(sentence);
                }
                else
                {
                    result.AddRange(HardSplit(sentence, maxLength));
                }
            }
        }

        return result;
    }

    /// <summary>Packs sentences into fragments up to the limit.</summary>
    private static List<string> BuildMaxLengthFragments(List<string> paragraphs, int maxLength)
    {
        var result = new List<string>();
        foreach (var paragraph in paragraphs)
        {
            result.AddRange(PackSentences(SplitSentences(paragraph), maxLength));
        }

        return result;
    }

    /// <summary>Adds sentences in sequence while they fit within the limit.</summary>
    private static List<string> PackSentences(List<string> sentences, int maxLength)
    {
        var result = new List<string>();
        var current = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (sentence.Length > maxLength)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }

                result.AddRange(HardSplit(sentence, maxLength));
                continue;
            }

            if (current.Length == 0)
            {
                current.Append(sentence);
                continue;
            }

            if (current.Length + 1 + sentence.Length <= maxLength)
            {
                current.Append(' ').Append(sentence);
                continue;
            }

            result.Add(current.ToString().Trim());
            current.Clear();
            current.Append(sentence);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }

    /// <summary>Cuts an overly long sentence at the last space/comma before the limit.</summary>
    private static List<string> HardSplit(string sentence, int maxLength)
    {
        var result = new List<string>();
        var remaining = sentence.Trim();

        while (remaining.Length > maxLength)
        {
            var cut = FindSplitPosition(remaining, maxLength);
            if (cut <= 0)
            {
                cut = maxLength;
            }

            var head = remaining[..cut].Trim();
            if (head.Length > 0)
            {
                result.Add(head);
            }

            remaining = remaining[cut..].Trim();
        }

        if (remaining.Length > 0)
        {
            result.Add(remaining);
        }

        return result;
    }

    /// <summary>Searches for a cut position, scanning backward from the limit toward its midpoint.</summary>
    private static int FindSplitPosition(string value, int maxLength)
    {
        var limit = Math.Min(maxLength, value.Length - 1);
        for (var i = limit; i > limit / 2; i--)
        {
            if (value[i] is ' ' or ',' or ';' or ':')
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Merges overly short fragments into the previous one if they fit.</summary>
    private static List<string> MergeShortFragments(List<string> fragments, int maxLength)
    {
        if (fragments.Count < 2)
        {
            return fragments;
        }

        var result = new List<string>(fragments.Count);
        foreach (var fragment in fragments)
        {
            if (result.Count > 0 &&
                fragment.Length < MinFragmentLength &&
                result[^1].Length + 1 + fragment.Length <= maxLength)
            {
                result[^1] = $"{result[^1]} {fragment}";
                continue;
            }

            result.Add(fragment);
        }

        return result;
    }

    /// <summary>Assigns sequential numbers to fragments, starting from 1.</summary>
    private static List<TextFragment> ToTextFragments(List<string> rawFragments)
    {
        var result = new List<TextFragment>(rawFragments.Count);
        var index = 1;

        foreach (var raw in rawFragments)
        {
            var text = raw.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            result.Add(new TextFragment(index++, text));
        }

        return result;
    }

    [GeneratedRegex(@"(?<=[.!?…])\s+(?=[«""'(\[]?[А-ЯІЇЄҐA-Z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBoundaryRegex();

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
