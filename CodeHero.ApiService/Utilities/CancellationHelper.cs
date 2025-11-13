using System.Net.Http;

namespace CodeHero.ApiService.Utilities;

public static class CancellationHelper
{
    // Create a linked CTS with timeout and return the CTS and an associated id.
    public static (CancellationTokenSource Cts, string Id) CreateLinkedCtsWithId(CancellationToken parent, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
        cts.CancelAfter(timeout);
        var id = Guid.NewGuid().ToString("D");
        return (cts, id);
    }

    public static bool TryGetCancelId(HttpRequestMessage? request, out string? id)
    {
        id = null;
        if (request is null) return false;
#if NET6_0_OR_GREATER
        var key = new System.Net.Http.HttpRequestOptionsKey<string>("CancelId");
        if (request.Options.TryGetValue(key, out var v))
        {
            id = v;
            return true;
        }
#endif
        return false;
    }
}
