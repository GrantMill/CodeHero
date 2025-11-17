namespace CodeHero.ApiService.Services.Rag;

internal sealed class NoopEmbeddingProvider : IEmbeddingProvider
{
    public Task<float[]?> GetEmbeddingAsync(string input, CancellationToken ct = default)
        => Task.FromResult<float[]?>(null);
}
