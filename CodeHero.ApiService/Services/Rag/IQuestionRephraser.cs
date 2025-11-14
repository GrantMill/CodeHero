using CodeHero.ApiService.Contracts;

namespace CodeHero.ApiService.Services.Rag;

/// <summary>
/// Describes a service that converts a follow-up user input and prior chat history into a single
/// standalone question grounded in the repository context, suitable for RAG.
/// </summary>
public interface IQuestionRephraser
{
    /// <summary>
    /// Rephrases the given <see cref="ChatRequest"/> into a self-contained question for downstream retrieval.
    /// </summary>
    /// <param name="request">The request containing the user follow-up and chat history.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The rephrased standalone question string.</returns>
    Task<string> RephraseAsync(ChatRequest request, CancellationToken ct = default);
}