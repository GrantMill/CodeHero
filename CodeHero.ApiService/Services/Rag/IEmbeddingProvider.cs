using System.Threading;
using System.Threading.Tasks;

namespace CodeHero.ApiService.Services.Rag;

/// <summary>
/// Abstraction for embedding providers. Implementations produce an embedding vector for an input string
/// or return null when embedding is unavailable.
/// </summary>
public interface IEmbeddingProvider
{
    /// <summary>
    /// Returns an embedding vector or null when embedding is not configured or fails.
    /// </summary>
    Task<float[]?> GetEmbeddingAsync(string input, CancellationToken ct = default);
}
