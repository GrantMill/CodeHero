using System.Net.Http.Headers;
using System.Text.Json;

namespace CodeHero.Web.Services;

/// <summary>
/// STT over HTTP against a local Whisper container exposing POST /stt.
/// TTS returns a short silent WAV via NullSpeechService.
/// </summary>
public sealed class WhisperClientSpeechService : ISpeechService
{
    private readonly HttpClient _http;
    private readonly NullSpeechService _ttsFallback;

    public WhisperClientSpeechService(HttpClient http, NullSpeechService ttsFallback)
    {
        _http = http;
        _ttsFallback = ttsFallback;
    }

    public Task<byte[]> SynthesizeAsync(string text, string voiceName, string style = "general", string role = "default", CancellationToken ct = default)
        => _ttsFallback.SynthesizeAsync(text, voiceName, style, role, ct);

    public async Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(audioWav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", "audio.wav");
        content.Add(new StringContent(locale), "language");
        using var resp = await _http.PostAsync("stt", content, ct);
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
