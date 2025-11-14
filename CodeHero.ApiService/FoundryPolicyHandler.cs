using Polly;
using Polly.Extensions.Http;

namespace CodeHero.ApiService;

// Delegating handler that applies Polly policies per request by executing the inner handler within
// the configured policies. This avoids requiring AddPolicyHandler extension methods.
public class FoundryPolicyHandler : DelegatingHandler
{
    private readonly IAsyncPolicy<HttpResponseMessage> _policy;

    public FoundryPolicyHandler(IConfiguration cfg)
    {
        var attemptTimeout = cfg.GetValue<int?>("Resilience:FoundryAttemptTimeoutSeconds") ?? 120;
        var retryCount = cfg.GetValue<int?>("Resilience:FoundryRetryCount") ?? 3;
        var circuitFailures = cfg.GetValue<int?>("Resilience:FoundryCircuitFailures") ?? 5;
        var circuitDuration = cfg.GetValue<int?>("Resilience:FoundryCircuitDurationSeconds") ?? 60;

        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(retryCount, attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)));

        var circuit = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(circuitFailures, TimeSpan.FromSeconds(circuitDuration));

        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(attemptTimeout));

        _policy = Policy.WrapAsync(circuit, retry, timeout);
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return _policy.ExecuteAsync(ct => base.SendAsync(request, ct), cancellationToken);
    }
}