# Text chunking

> **Purpose:** how text is split into fragments before synthesis: the inputs, the three modes and the exact rules.
> **Audience:** developers and AI agents.
> **Source of truth:** `Infrastructure/Text/SentenceAwareTextChunker.cs`, `Core/Enums/TextChunkingMode.cs`, `Core/Models/TextChunkingOptions.cs`.
> **Verified:** 2026-11-03.

## Inputs and outputs

- Interface: `ITextChunker.Split(string text, TextChunkingOptions options)` returns `IReadOnlyList<TextFragment>`.
- `TextFragment(Index, Text)`: `Index` starts at 1 and has no gaps.
- `TextChunkingOptions.Mode`: `Paragraphs` (the default value of the options record and of the config), `Sentences`, `MaxLength`.
- `TextChunkingOptions.MaxChunkLength`: 250 by default (`DefaultMaxChunkLength`). A value of 0 or less falls back to 250.
- Empty or whitespace-only text returns an empty list. No exception is thrown.

| Mode (`TextChunkingMode`) | UI label (Settings) |
|---|---|
| `Paragraphs` | «Абзаци» |
| `Sentences` | «Речення» |
| `MaxLength` | «До 250 символів (розумне пакування)» |

## Pipeline

1. **Normalize.** CRLF becomes LF. Runs of line breaks collapse into one. Every other whitespace character becomes a space, and runs of spaces collapse into one. The text is trimmed.
2. **Paragraphs.** The normalized text is split at line breaks. Every non-empty line is a paragraph.
3. **Sentences.** A paragraph is split after `.`, `!`, `?` or `…` when the next token starts with an uppercase letter (Cyrillic `А–Я`, `І`, `Ї`, `Є`, `Ґ`, or Latin `A–Z`) or a digit. An opening quote or bracket (`«`, `"`, `'`, `(`, `[`) before that letter is allowed. A lowercase letter after a period does not start a new sentence.
4. **Abbreviation guard.** A split is suppressed after these abbreviations (matched case-insensitively, as whole words): `т.д.`, `т.п.`, `і т.ін.`, `р.`, `м.`, `вул.`, `ім.`, `проф.`, `акад.`, `див.`, `напр.`. A single uppercase letter followed by a period (an initial, such as `І.` or `J.`) also does not end a sentence.
5. **Mode rules** (below).
6. **Merge short fragments** (not in `Sentences` mode). A fragment shorter than 20 characters is appended to the previous fragment if the result fits into `MaxChunkLength`.
7. **Finish.** Each fragment is trimmed, empty fragments are removed, and indexes are assigned from 1.

## Mode rules

| Mode | Rule |
|---|---|
| `Paragraphs` | A paragraph that fits the limit is one fragment. A longer paragraph is packed by sentences (see packing). |
| `Sentences` | One sentence is one fragment. A sentence longer than the limit is hard-split. Short sentences are not merged. |
| `MaxLength` | Inside each paragraph, consecutive sentences are packed while they fit the limit. A sentence longer than the limit is hard-split. Packing does not cross paragraph boundaries; the only exception is the short-fragment merge (step 6). |

**Packing.** The next sentence is appended to the current fragment with one space between them, if the result stays within the limit. Otherwise the current fragment is closed and a new one starts. A sentence longer than the limit is handled by hard split, after the current fragment is closed.

**Hard split.** While the remaining text is longer than the limit, the cut is placed at the last space, comma, semicolon or colon found between `limit / 2` and `limit`, searching backward from the limit. If there is none, the text is cut exactly at the limit. Each piece is trimmed.

## Practical consequences

- A fragment is never longer than the limit, except when a hard split cannot find a separator. A cut then happens at the limit, which may be in the middle of a word.
- In `Paragraphs` and `MaxLength` a very short fragment can be joined with the previous one, so the fragment count can be lower than the count of paragraphs.
- Text without any punctuation is split only by spaces, commas, semicolons and colons.
- The chunker does not change the text, except for whitespace normalization and trimming.
