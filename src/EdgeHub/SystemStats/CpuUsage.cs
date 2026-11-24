namespace EdgeHub.SystemStats;

/// <summary>Processor times since boot, in 100 ns ticks, as Windows' GetSystemTimes returns them (kernel time includes idle time).</summary>
public readonly record struct CpuTimes(long Idle, long Kernel, long User);

public static class CpuUsage
{
    /// <summary>Share of processor time spent working between two readings, from 0 to 100.</summary>
    public static double Between(CpuTimes previous, CpuTimes current)
    {
        long total = (current.Kernel - previous.Kernel) + (current.User - previous.User);
        if (total <= 0)
        {
            return 0;
        }

        long busy = total - (current.Idle - previous.Idle);
        return Math.Clamp(busy * 100.0 / total, 0, 100);
    }
}
