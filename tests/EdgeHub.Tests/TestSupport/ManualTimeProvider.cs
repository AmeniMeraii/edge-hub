namespace EdgeHub.Tests.TestSupport;

/// <summary>A clock that only moves when a test says so, in the machine's time zone unless the test gives one.</summary>
internal sealed class ManualTimeProvider(DateTimeOffset start, TimeZoneInfo? zone = null) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override TimeZoneInfo LocalTimeZone => zone ?? base.LocalTimeZone;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => _now.UtcTicks;

    public void Advance(TimeSpan by) => _now += by;
}
