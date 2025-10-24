using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodeHero.Web.Services;

/// <summary>
/// Uses Azure AI Foundry (Azure OpenAI) audio transcriptions endpoint against a deployment
/// such as 'gpt-4o-transcribe-diarize' to convert WAV bytes to text.
/// </summary>
public sealed class FoundryTranscribeService
{
    private readonly string _endpoint;
    private readonly string _key;
    private readonly string _deployment;
    private readonly string _apiVersion;
    private readonly IHttpClientFactory? _httpClientFactory;

    public FoundryTranscribeService(IConfiguration config, IHttpClientFactory? httpClientFactory = null)
    {
        _endpoint = (config["AzureAI:Foundry:Endpoint"] ?? string.Empty).TrimEnd('/');
        _key = config["AzureAI:Foundry:Key"] ?? string.Empty;
        _deployment = config["AzureAI:Foundry:TranscribeDeployment"] ?? "gpt-4o-transcribe-diarize";
        // Preview api-version commonly used by audio transcriptions for Azure OpenAI; allow override
        _apiVersion = config["AzureAI:Foundry:ApiVersion"] ?? "2024-08-01-preview";
        _httpClientFactory = httpClientFactory;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_endpoint) && !string.IsNullOrWhiteSpace(_key);

    public async Task<string> TranscribeAsync(byte[] audioWav, string locale = "en-US", CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Azure Foundry not configured. Set AzureAI:Foundry:Endpoint, Key, and TranscribeDeployment.");

        var url = $"{_endpoint}/openai/deployments/{_deployment}/audio/transcriptions?api-version={_apiVersion}";

        using var client = _httpClientFactory?.CreateClient() ?? new HttpClient();
        client.DefaultRequestHeaders.Add("api-key", _key);

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(audioWav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "audio.wav");
        // Optional parameters supported by some deployments
        content.Add(new StringContent(locale, Encoding.UTF8), "language");
        content.Add(new StringContent("true", Encoding.UTF8), "diarize");
        content.Add(new StringContent("json", Encoding.UTF8), "response_format");

        using var resp = await client.PostAsync(url, content, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Foundry STT failed: {(int)resp.StatusCode} {resp.ReasonPhrase} - {body}");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("text", out var textEl))
                return textEl.GetString() ?? string.Empty;
            if (doc.RootElement.TryGetProperty("transcript", out var trEl))
                return trEl.GetString() ?? string.Empty;
        }
        catch
        {
            // fall through and return raw
        }
        return body;
    }
}