using System.Text;

namespace CodeHero.Web.Services;

/// <summary>
/// Fallback speech service used when Azure Speech is not configured.
/// Produces silent WAV for TTS and returns empty string for STT.
/// </summary>
public sealed class NullSpeechService : ISpeechService
{
    public Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default)
    {
        // 1s of silence: 16-bit PCM mono 16kHz
        var sampleRate = 16000;
        var channels = 1;
        var bitsPerSample = 16;
        var durationSeconds = 1;
        var dataSize = sampleRate * channels * (bitsPerSample / 8) * durationSeconds;
        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        // RIFF header
        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + dataSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // PCM chunk size
        bw.Write((short)1); // PCM format
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        bw.Write((short)(channels * (bitsPerSample / 8))); // block align
        bw.Write((short)bitsPerSample);
        // data chunk
        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(dataSize);
        // silence
        ms.Position = 44;
        ms.Write(new byte[dataSize], 0, dataSize);
        return Task.FromResult(ms.ToArray());
    }

    public Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        return Task.FromResult(string.Empty);
    }
}
