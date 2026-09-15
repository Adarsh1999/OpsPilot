using System.Threading.RateLimiting;

namespace OpsPilot.Web.Services;

public sealed class AnalysisRateLimitException() : Exception("Analysis limit reached. Wait a minute and try again.");

// Shared across circuits: HTTP rate limiting alone cannot protect server-side Blazor events.
public sealed class AnalysisGate : IDisposable
{
    private readonly FixedWindowRateLimiter requests = new(new()
    {
        PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
    });
    private readonly ConcurrencyLimiter concurrent = new(new() { PermitLimit = 2, QueueLimit = 0 });

    public RateLimitLease Acquire()
    {
        var active = concurrent.AttemptAcquire();
        if (!active.IsAcquired) { active.Dispose(); throw new AnalysisRateLimitException(); }
        using var request = requests.AttemptAcquire();
        if (request.IsAcquired) return active;
        active.Dispose();
        throw new AnalysisRateLimitException();
    }
    public void Dispose() { requests.Dispose(); concurrent.Dispose(); }
}
