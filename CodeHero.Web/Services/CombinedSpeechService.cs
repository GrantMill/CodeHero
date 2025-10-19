namespace CodeHero.Web.Services;

public sealed class CombinedSpeechService : ISpeechService
{
    private readonly AzureSpeechService _tts;
    private readonly FoundryTranscribeService _stt;

    public CombinedSpeechService(AzureSpeechService tts, FoundryTranscribeService stt)
    {
        _tts = tts;
        _stt = stt;
    }

    public Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default)
        => _tts.SynthesizeAsync(text, voiceName, style, role, ct);

    public Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
        => _stt.TranscribeAsync(audioWav, locale, ct);
}
