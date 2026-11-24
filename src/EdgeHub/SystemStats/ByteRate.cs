namespace EdgeHub.SystemStats;

public static class ByteRate
{
    /// <summary>Bytes per second between two readings of a byte counter.</summary>
    public static double PerSecond(long previousBytes, long currentBytes, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || currentBytes < previousBytes)
        {
            return 0;
        }

        return (currentBytes - previousBytes) / elapsed.TotalSeconds;
    }
}
