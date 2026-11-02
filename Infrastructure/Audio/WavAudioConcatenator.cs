#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UA_TTS_Client.Infrastructure.Audio;

/// <summary>
/// Concatenates WAV fragments (24 kHz, mono, 16-bit PCM) into a single WAV file.
/// </summary>
/// <remarks>
/// <para>The class is synchronous and does not depend on BASS, HTTP, or UI: the input is WAV bytes,
/// the output is a new valid RIFF file.</para>
/// <para>No extra pauses are inserted between fragments: the export sounds the same
/// as sequential playback.</para>
/// </remarks>
public static class WavAudioConcatenator
{
    private const int ExpectedSampleRate = 24000;
    private const short PcmAudioFormat = 1;
    private const short ExpectedChannels = 1;
    private const short ExpectedBitsPerSample = 16;
    private const int HeaderSize = 44;

    /// <summary>
    /// Concatenates fragments into a single WAV file, preserving their order.
    /// </summary>
    /// <param name="parts">WAV fragments of the same format.</param>
    /// <returns>Contents of the combined WAV file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="parts"/> is empty.</exception>
    /// <exception cref="InvalidDataException">A fragment is not 24 kHz/mono/16-bit PCM WAV.</exception>
    public static byte[] Concat(IReadOnlyList<byte[]> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new ArgumentException("Немає фрагментів для склеювання.", nameof(parts));
        }

        var dataChunks = new byte[parts.Count][];
        long totalData = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            var data = ExtractDataChunk(parts[i], i);
            dataChunks[i] = data;
            totalData += data.Length;
        }

        var dataSize = checked((int)totalData);

        using var output = new MemoryStream(dataSize + HeaderSize);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(dataSize + HeaderSize - 8); // RIFF size = 36 + data size
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                                                                  // fmt chunk size
        writer.Write(PcmAudioFormat);
        writer.Write(ExpectedChannels);
        writer.Write(ExpectedSampleRate);
        writer.Write(ExpectedSampleRate * ExpectedChannels * ExpectedBitsPerSample / 8);   // bytes per second
        writer.Write((short)(ExpectedChannels * ExpectedBitsPerSample / 8));               // block alignment
        writer.Write(ExpectedBitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        foreach (var data in dataChunks)
        {
            writer.Write(data);
        }

        writer.Flush();
        return output.ToArray();
    }

    /// <summary>
    /// Extracts and validates the PCM data of a single WAV fragment.
    /// </summary>
    /// <param name="wav">WAV file contents.</param>
    /// <param name="partIndex">Fragment index (for error reporting).</param>
    /// <returns>Bytes of the <c>data</c> chunk.</returns>
    /// <exception cref="InvalidDataException">The header is missing or the format is not the expected one.</exception>
    private static byte[] ExtractDataChunk(byte[] wav, int partIndex)
    {
        ArgumentNullException.ThrowIfNull(wav);

        if (wav.Length < 12 || !HasTag(wav, 0, "RIFF") || !HasTag(wav, 8, "WAVE"))
        {
            throw new InvalidDataException($"Фрагмент {partIndex + 1}: немає заголовка RIFF/WAVE.");
        }

        byte[]? data = null;
        var formatChecked = false;
        var offset = 12;

        while (offset + 8 <= wav.Length)
        {
            var chunkId = ReadTag(wav, offset);
            var chunkSize = BitConverter.ToInt32(wav, offset + 4);
            var contentStart = offset + 8;

            if (chunkSize < 0 || contentStart + chunkSize > wav.Length)
            {
                throw new InvalidDataException($"Фрагмент {partIndex + 1}: чанк «{chunkId}» пошкоджено.");
            }

            if (chunkId == "fmt ")
            {
                ValidateFormat(wav.AsSpan(contentStart, chunkSize), partIndex);
                formatChecked = true;
            }
            else if (chunkId == "data")
            {
                data = wav[contentStart..(contentStart + chunkSize)];
            }

            // Chunks are aligned to an even number of bytes.
            offset = contentStart + chunkSize + (chunkSize % 2);
        }

        if (!formatChecked)
        {
            throw new InvalidDataException($"Фрагмент {partIndex + 1}: відсутній чанк fmt.");
        }

        if (data is null)
        {
            throw new InvalidDataException($"Фрагмент {partIndex + 1}: відсутній чанк data.");
        }

        return data;
    }

    /// <summary>Checks that the <c>fmt</c> chunk describes 24 kHz/mono/16-bit PCM.</summary>
    private static void ValidateFormat(ReadOnlySpan<byte> fmt, int partIndex)
    {
        if (fmt.Length < 16)
        {
            throw new InvalidDataException($"Фрагмент {partIndex + 1}: чанк fmt закороткий.");
        }

        var audioFormat = BitConverter.ToInt16(fmt[..2]);
        var channels = BitConverter.ToInt16(fmt[2..4]);
        var sampleRate = BitConverter.ToInt32(fmt[4..8]);
        var bitsPerSample = BitConverter.ToInt16(fmt[14..16]);

        if (audioFormat != PcmAudioFormat || channels != ExpectedChannels ||
            sampleRate != ExpectedSampleRate || bitsPerSample != ExpectedBitsPerSample)
        {
            throw new InvalidDataException(
                $"Фрагмент {partIndex + 1}: очікується WAV 24 кГц, моно, 16-bit PCM; " +
                $"отримано format={audioFormat}, channels={channels}, rate={sampleRate}, bits={bitsPerSample}.");
        }
    }

    /// <summary>Checks the 4-byte tag at the specified offset.</summary>
    private static bool HasTag(byte[] wav, int offset, string tag) => ReadTag(wav, offset) == tag;

    /// <summary>Reads a 4-byte ASCII tag.</summary>
    private static string ReadTag(byte[] wav, int offset) => Encoding.ASCII.GetString(wav, offset, 4);
}
