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
        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_region))
            throw new InvalidOperationException("Azure Speech not configured. Set AzureAI:Speech:Key and AzureAI:Speech:Region.");

        var config = SpeechConfig.FromSubscription(_key, _region);
        config.SpeechSynthesisVoiceName = voiceName;
        // Use default output to get audio bytes directly from result.AudioData (RIFF WAV PCM by default)
        using var synthesizer = new SpeechSynthesizer(config);
        using var result = await synthesizer.SpeakTextAsync(text).ConfigureAwait(false);
        if (result.Reason != ResultReason.SynthesizingAudioCompleted)
        {
            throw new InvalidOperationException($"Synthesis failed: {result.Reason}");
        }
        return result.AudioData;
    }

    public async Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_region))
            throw new InvalidOperationException("Azure Speech not configured. Set AzureAI:Speech:Key and AzureAI:Speech:Region.");

        var config = SpeechConfig.FromSubscription(_key, _region);
        config.SpeechRecognitionLanguage = locale;

        // Push the WAV bytes (including RIFF header) into a PushAudioInputStream so the service can auto-detect format
        var push = AudioInputStream.CreatePushStream();
        push.Write(audioWav);
        push.Close();
        using var audioConfig = AudioConfig.FromStreamInput(push);
        using var recognizer = new SpeechRecognizer(config, audioConfig);
        var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
        if (result.Reason == ResultReason.RecognizedSpeech)
            return result.Text;
        throw new InvalidOperationException($"Transcription failed: {result.Reason}");
    }
}
