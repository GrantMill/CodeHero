namespace CodeHero.Web.Services;

public sealed class RagClient(HttpClient http)
{
    public async Task<AnswerResponse> AskAsync(string chatInput, IReadOnlyList<ChatTurn> history, CancellationToken ct = default)
    {
        // 1) Rephrase
        var rephraseResp = await http.PostAsJsonAsync("/api/chat/rephrase", new ChatRequest(chatInput, history), ct);
        rephraseResp.EnsureSuccessStatusCode();
        var standalone = await rephraseResp.Content.ReadAsStringAsync(ct);

        // 2) Search
        var searchHttpResp = await http.PostAsJsonAsync("/api/search/hybrid", new SearchRequest(standalone, TopK: 2), ct);
        searchHttpResp.EnsureSuccessStatusCode();
        var searchResp = await searchHttpResp.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken: ct) ?? new SearchResponse(Array.Empty<SearchHit>());

        // 3) Answer
        var answerHttpResp = await http.PostAsJsonAsync("/api/chat/answer", new AnswerRequest(chatInput, history, searchResp.Results), ct);
        answerHttpResp.EnsureSuccessStatusCode();
        var answer = await answerHttpResp.Content.ReadFromJsonAsync<AnswerResponse>(cancellationToken: ct);
        return answer!;
    }
}