using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.Web.Services;

/// <summary>
/// Uses a local Whisper container for STT (POST /stt) and a local TTS container for synthesis (POST /tts).
/// Base addresses are read from configuration: Speech:Endpoint and Tts:Endpoint.
/// </summary>
public sealed class WhisperAndHttpTtsSpeechService : ISpeechService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly string _sttBase;
    private readonly string _ttsBase;
    private readonly NullSpeechService _nullTts;

    public WhisperAndHttpTtsSpeechService(IHttpClientFactory httpFactory, IConfiguration config, NullSpeechService nullTts)
    {
        _httpFactory = httpFactory;
        _sttBase = config["Speech:Endpoint"] ?? "http://localhost:18000";
        _ttsBase = config["Tts:Endpoint"] ?? string.Empty;
        _nullTts = nullTts;
    }

    public async Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_ttsBase))
        {
            return await _nullTts.SynthesizeAsync(text, voiceName, style, role, ct);
        }

        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_ttsBase);
        using var content = new StringContent(text ?? string.Empty, Encoding.UTF8, "text/plain");
        using var resp = await client.PostAsync("tts", content, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        var client = _httpFactory.CreateClient();
        client.BaseAddress = new Uri(_sttBase);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audioWav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "audio.wav");
        form.Add(new StringContent(locale), "language");
        using var resp = await client.PostAsync("stt", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("text", out var t))
                return t.GetString() ?? string.Empty;
        }
        catch { }
        return body;
    }
}
