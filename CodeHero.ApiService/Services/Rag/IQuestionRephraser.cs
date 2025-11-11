using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

public interface IQuestionRephraser
{
    Task<string> RephraseAsync(ChatRequest request, CancellationToken ct = default);
}