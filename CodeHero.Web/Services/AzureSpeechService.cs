using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Configuration;

namespace CodeHero.Web.Services;

public sealed class AzureSpeechService : ISpeechService
{
    private readonly string _key;
    private readonly string _region;

    public AzureSpeechService(IConfiguration config)
    {
        _key = config["AzureAI:Speech:Key"] ?? string.Empty;
        _region = config["AzureAI:Speech:Region"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_region))
        {
            // Service remains usable, but will throw when invoked without config
        }
    }

    public async Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default)
    {
        var config = SpeechConfig.FromSubscription(_key, _region);
        config.SpeechSynthesisVoiceName = voiceName;
        using var audioStream = AudioOutputStream.CreatePullStream();
        using var audioConfig = AudioConfig.FromStreamOutput(audioStream);
        using var synthesizer = new SpeechSynthesizer(config, audioConfig);
        using var result = await synthesizer.SpeakTextAsync(text).ConfigureAwait(false);
        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            throw new InvalidOperationException($"Synthesis failed: {result.Reason}");
        }
        // The SDK writes to the underlying stream; we can instead use the built-in GetAudioData (not present), so re-run with pull
        using var mem = new MemoryStream();
        // Fallback: synthesize to file stream
        var bytes = result.AudioData;
        return bytes;
    }

    public async Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        var config = SpeechConfig.FromSubscription(_key, _region);
        config.SpeechRecognitionLanguage = locale;
        using var audioInput = AudioConfig.FromStreamInput(AudioInputStream.CreatePushStream());
        using var recognizer = new SpeechRecognizer(config, audioInput);
        // Simple one-shot recognize from WAV bytes by pushing into the stream
        var push = AudioInputStream.CreatePushStream();
        push.Write(audioWav);
        push.Close();
        using var rec = new SpeechRecognizer(config, AudioConfig.FromStreamInput(push));
        var result = await rec.RecognizeOnceAsync().ConfigureAwait(false);
        if (result.Reason == ResultReason.RecognizedSpeech)
            return result.Text;
        throw new InvalidOperationException($"Transcription failed: {result.Reason}");
    }
}
