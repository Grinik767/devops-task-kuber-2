using System.Diagnostics;

namespace HomeworkApp.Services;

public sealed class ProbeState
{
    private readonly Stopwatch startedAt = Stopwatch.StartNew();
    private int dead;
    private int notReady;

    public bool IsLive => startedAt.Elapsed >= TimeSpan.FromSeconds(10)
        && Volatile.Read(ref dead) == 0;

    public bool IsReady => startedAt.Elapsed >= TimeSpan.FromSeconds(10)
        && Volatile.Read(ref notReady) == 0;

    public void FailLiveness() => Interlocked.Exchange(ref dead, 1);

    public void DisableReadiness() => Interlocked.Exchange(ref notReady, 1);

    public void RestoreReadiness() => Interlocked.Exchange(ref notReady, 0);
}
