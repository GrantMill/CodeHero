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

    // Cap TTS response to5MB by default
    private const long MaxTtsResponseBytes = 5L * 1024 * 1024;

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

        // SynthesizeAsync: use named client "tts" and stream response headers-first
        var client = _httpFactory.CreateClient("tts");
        using var request = new HttpRequestMessage(HttpMethod.Post, "tts")
        {
            Content = new StringContent(text ?? string.Empty, Encoding.UTF8, "text/plain")
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/wav"));

        using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        // Stream and cap response size
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read == 0) break;
            total += read;
            if (total > MaxTtsResponseBytes)
            {
                throw new InvalidOperationException($"TTS response exceeded limit ({MaxTtsResponseBytes} bytes)");
            }
            await ms.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return ms.ToArray();
    }

    public async Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        // TranscribeAsync: use named client "stt" and ensure success before reading body
        var client = _httpFactory.CreateClient("stt");
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(audioWav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "audio.wav");
        form.Add(new StringContent(locale), "language");
        using var resp = await client.PostAsync("stt", form, ct);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(ct);
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
