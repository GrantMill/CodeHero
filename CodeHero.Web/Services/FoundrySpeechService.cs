namespace CodeHero.Web.Services;

/// <summary>
/// ISpeechService adapter that uses Azure Foundry for STT and returns silent WAV for TTS.
/// Useful when only transcription is needed and Azure Speech TTS is not configured.
/// </summary>
public sealed class FoundrySpeechService : ISpeechService
{
    private readonly FoundryTranscribeService _stt;
    private readonly NullSpeechService _ttsFallback;

    public FoundrySpeechService(FoundryTranscribeService stt, NullSpeechService ttsFallback)
    {
        _stt = stt;
        _ttsFallback = ttsFallback;
    }

    public Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default)
        => _ttsFallback.SynthesizeAsync(text, voiceName, style, role, ct);

    public Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
        => _stt.TranscribeAsync(audioWav, locale, ct);
}