namespace CodeHero.Web.Services;

public sealed record ChatTurn(string User, string Assistant);
public sealed record ChatRequest(string ChatInput, IReadOnlyList<ChatTurn> ChatHistory);

public sealed record SearchRequest(string StandaloneQuestion, int TopK = 2);
public sealed record SearchHit(string Content, string Source, double Score);
public sealed record SearchResponse(IReadOnlyList<SearchHit> Results);

public sealed record AnswerRequest(
    string ChatInput,
    IReadOnlyList<ChatTurn> ChatHistory,
    IReadOnlyList<SearchHit> Contexts);

public sealed record AnswerResponse(string Output, string? Reasoning, string Confidence);