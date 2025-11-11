using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

public interface IRagAnswerService
{
    Task<AnswerResponse> AnswerAsync(AnswerRequest request, CancellationToken ct = default);
}