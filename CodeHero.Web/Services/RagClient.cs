using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeHero.Web.Services;

public sealed class RagClient
{
    private readonly HttpClient _http;
    public DiagnosticAnswerResponse? LastDiagnostic { get; set; }

    private static readonly Regex ClarifyPattern = new(@"\b(clarify|clarification|more details|could you|please provide|what do you mean|can you explain)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RagClient(HttpClient http, NavigationManager nav)
    {
        _http = http;
        // Ensure BaseAddress so relative '/api/..' URIs succeed
        if (_http.BaseAddress is null)
        {
            // nav.BaseUri is absolute (e.g., https://localhost:5001/)
            _http.BaseAddress = new Uri(nav.BaseUri, UriKind.Absolute);
        }
    }

    public async Task<AnswerResponse> AskAsync(string chatInput, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        var diag = await AskDiagnosticAsync(chatInput, history, ct);
        LastDiagnostic = diag;

        if (diag.RephraseNeedsClarification && diag.RephraseRaw is not null)
        {
            // Return the clarification as assistant output so UI can show and await user reply
            return new AnswerResponse(diag.RephraseRaw, null, "low");
        }

        if (diag.ParsedAnswer is not null)
            return diag.ParsedAnswer;

        var fallbackText = diag.AnswerRaw ?? "";
        return new AnswerResponse(fallbackText, null, diag.AnswerStatus >= 200 && diag.AnswerStatus < 300 ? "medium" : "low");
    }

    private HttpClient CreateDirectClient()
    {
        var handler = new System.Net.Http.SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            Expect100ContinueTimeout = TimeSpan.Zero,
            UseProxy = false
        };
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        if (_http.BaseAddress is not null)
            client.BaseAddress = _http.BaseAddress;
        client.DefaultRequestVersion = System.Net.HttpVersion.Version11;
        return client;
    }

    public async Task<DiagnosticAnswerResponse> AskDiagnosticAsync(string chatInput, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        var corr = Guid.NewGuid().ToString("D");

        string rephraseRaw = string.Empty;
        int rephraseStatus = 0;
        string searchRaw = string.Empty;
        int searchStatus = 0;
        string answerRaw = string.Empty;
        int answerStatus = 0;
        AnswerResponse? parsedAnswer = null;

        using var client = CreateDirectClient();

        // 1) Rephrase
        try
        {
            using var rephraseReq = new HttpRequestMessage(HttpMethod.Post, "api/chat/rephrase")
            {
                Content = JsonContent.Create(new ChatRequest(chatInput, history))
            };
            rephraseReq.Headers.Remove("X-Correlation-Id");
            rephraseReq.Headers.Add("X-Correlation-Id", corr);

            using var rephraseResp = await client.SendAsync(rephraseReq, ct);
            rephraseStatus = (int)rephraseResp.StatusCode;
            rephraseRaw = await rephraseResp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            rephraseRaw = ex.ToString();
            rephraseStatus = 0;
        }

        // If rephrase looks like a clarification, stop here and return diagnostic indicating clarification
        var needsClarify = !string.IsNullOrWhiteSpace(rephraseRaw) && ClarifyPattern.IsMatch(rephraseRaw);
        if (needsClarify)
        {
            var diagClar = new DiagnosticAnswerResponse(
                RephraseRaw: rephraseRaw,
                RephraseStatus: rephraseStatus,
                SearchRaw: string.Empty,
                SearchStatus: 0,
                AnswerRaw: string.Empty,
                AnswerStatus: 0,
                ParsedAnswer: null,
                AnswerRequestJson: null,
                RephraseNeedsClarification: true
            );

            LastDiagnostic = diagClar;
            return diagClar;
        }

        // 2) Search
        try
        {
            using var searchReq = new HttpRequestMessage(HttpMethod.Post, "api/search/hybrid")
            {
                Content = JsonContent.Create(new SearchRequest(rephraseRaw, TopK: 2))
            };
            searchReq.Headers.Remove("X-Correlation-Id");
            searchReq.Headers.Add("X-Correlation-Id", corr);

            using var searchResp = await client.SendAsync(searchReq, ct);
            searchStatus = (int)searchResp.StatusCode;
            searchRaw = await searchResp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            searchRaw = ex.ToString();
            searchStatus = 0;
        }

        SearchResponse? searchParsed = null;
        try { searchParsed = JsonSerializer.Deserialize<SearchResponse>(searchRaw, new JsonSerializerOptions(JsonSerializerDefaults.Web)); } catch { }

        // 3) Answer
        var answerRequestObj = new AnswerRequest(chatInput, history, searchParsed?.Results ?? Array.Empty<SearchHit>());
        try
        {
            using var answerReq = new HttpRequestMessage(HttpMethod.Post, "api/chat/answer")
            {
                Content = JsonContent.Create(answerRequestObj)
            };
            answerReq.Headers.Remove("X-Correlation-Id");
            answerReq.Headers.Add("X-Correlation-Id", corr);

            using var answerResp = await client.SendAsync(answerReq, ct);
            answerStatus = (int)answerResp.StatusCode;
            answerRaw = await answerResp.Content.ReadAsStringAsync(ct);

            try { parsedAnswer = JsonSerializer.Deserialize<AnswerResponse>(answerRaw, new JsonSerializerOptions(JsonSerializerDefaults.Web)); } catch { parsedAnswer = null; }
        }
        catch (Exception ex)
        {
            answerRaw = ex.ToString();
            answerStatus = 0;
            parsedAnswer = null;
        }

        var diag = new DiagnosticAnswerResponse(
            RephraseRaw: rephraseRaw,
            RephraseStatus: rephraseStatus,
            SearchRaw: searchRaw,
            SearchStatus: searchStatus,
            AnswerRaw: answerRaw,
            AnswerStatus: answerStatus,
            ParsedAnswer: parsedAnswer,
            AnswerRequestJson: JsonSerializer.Serialize(answerRequestObj, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            RephraseNeedsClarification: false
        );

        LastDiagnostic = diag;
        return diag;
    }
}