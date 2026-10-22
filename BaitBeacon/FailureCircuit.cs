namespace BaitBeacon;

/// <summary>Frame-safe retry gate. Uses monotonic time, and reports only the transition to stopped.</summary>
public sealed class FailureCircuit(TimeSpan delay, Action<Exception>? onStopped = null, TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private long failedAt;
    public int Failures { get; private set; }
    public bool Stopped => Failures >= 3;
    public Exception? LastError { get; private set; }

    public bool Run(Action action)
    {
        if (Stopped || (Failures > 0 && time.GetElapsedTime(failedAt) < delay)) return false;
        try
        {
            action();
            Retry();
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex;
            failedAt = time.GetTimestamp();
            if (++Failures == 3) onStopped?.Invoke(ex);
            return false;
        }
    }

    public void Retry()
    {
        Failures = 0;
        LastError = null;
    }
}
