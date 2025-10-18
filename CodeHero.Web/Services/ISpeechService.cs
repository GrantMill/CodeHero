namespace CodeHero.Web.Services;

public interface ISpeechService
{
    Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default);
    Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default);
}
