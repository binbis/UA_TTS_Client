#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UA_TTS_Client.Infrastructure.Audio;

/// <summary>
/// Склеює WAV-фрагменти (24 кГц, моно, 16-bit PCM) в один WAV-файл.
/// </summary>
/// <remarks>
/// <para>Клас синхронний і не залежить від BASS, HTTP чи UI: на вхід — байти WAV,
/// на вихід — новий коректний RIFF-файл.</para>
/// <para>Додаткових пауз між фрагментами не вставляється: експорт звучить так само,
/// як послідовне відтворення.</para>
/// </remarks>
public static class WavAudioConcatenator
{
    private const int ExpectedSampleRate = 24000;
    private const short PcmAudioFormat = 1;
    private const short ExpectedChannels = 1;
    private const short ExpectedBitsPerSample = 16;
    private const int HeaderSize = 44;

    /// <summary>
    /// Склеює фрагменти в один WAV-файл, зберігаючи їх порядок.
    /// </summary>
    /// <param name="parts">WAV-фрагменти однакового формату.</param>
    /// <returns>Вміст об'єднаного WAV-файлу.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> дорівнює <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="parts"/> порожній.</exception>
    /// <exception cref="InvalidDataException">Фрагмент не є WAV 24 кГц/моно/16-bit PCM.</exception>
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
        writer.Write(dataSize + HeaderSize - 8); // розмір RIFF = 36 + розмір data
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                                                                  // розмір fmt-чанка
        writer.Write(PcmAudioFormat);
        writer.Write(ExpectedChannels);
        writer.Write(ExpectedSampleRate);
        writer.Write(ExpectedSampleRate * ExpectedChannels * ExpectedBitsPerSample / 8);   // байтів за секунду
        writer.Write((short)(ExpectedChannels * ExpectedBitsPerSample / 8));               // вирівнювання блоку
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
    /// Витягує та перевіряє PCM-дані одного WAV-фрагмента.
    /// </summary>
    /// <param name="wav">Вміст WAV-файлу.</param>
    /// <param name="partIndex">Індекс фрагмента (для повідомлення про помилку).</param>
    /// <returns>Байти чанка <c>data</c>.</returns>
    /// <exception cref="InvalidDataException">Заголовок відсутній або формат не той, що очікується.</exception>
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

            // Чанки вирівняні на парну кількість байтів.
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

    /// <summary>Перевіряє, що <c>fmt</c>-чанк описує PCM 24 кГц/моно/16-bit.</summary>
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

    /// <summary>Перевіряє 4-байтовий тег за вказаним зсувом.</summary>
    private static bool HasTag(byte[] wav, int offset, string tag) => ReadTag(wav, offset) == tag;

    /// <summary>Читає 4-байтовий ASCII-тег.</summary>
    private static string ReadTag(byte[] wav, int offset) => Encoding.ASCII.GetString(wav, offset, 4);
}
