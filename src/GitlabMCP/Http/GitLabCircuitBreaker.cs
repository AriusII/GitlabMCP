namespace GitlabMCP.Http;

/// <summary>
///     Deliberately not Polly (DEC-021/aspnet-config-di research: <c>Microsoft.Extensions.Http.Resilience</c>'s AOT
///     status is an open upstream issue, dotnet/extensions#4622) — a handful of <see cref="Interlocked" /> ops.
/// </summary>
public sealed class GitLabCircuitBreaker(int failureThreshold = 5, int breakDurationSeconds = 30)
{
    private int _consecutiveFailures;
    private long _openUntilTicks;

    public bool IsOpen => Environment.TickCount64 < Interlocked.Read(ref _openUntilTicks);

    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }

    public void RecordFailure()
    {
        if (Interlocked.Increment(ref _consecutiveFailures) < failureThreshold) return;
        Interlocked.Exchange(ref _openUntilTicks, Environment.TickCount64 + breakDurationSeconds * 1000L);
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }
}