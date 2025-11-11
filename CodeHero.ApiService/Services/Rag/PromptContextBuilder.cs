using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

public static class PromptContextBuilder
{
    public static string Build(IReadOnlyList<SearchHit> hits)
    {
        static string Fmt(SearchHit h) => $"Content: {h.Content}\nSource: {h.Source}";
        return string.Join("\n\n", hits.Select(Fmt));
    }
}