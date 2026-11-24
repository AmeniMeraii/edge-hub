using EdgeHub.SystemStats;

namespace EdgeHub.Tests.SystemStats;

public sealed class UnitsTests
{
    [Theory]
    [InlineData(512, "512 o/s")]
    [InlineData(1_536, "1,5 Ko/s")]
    [InlineData(12_897_485, "12,3 Mo/s")]
    [InlineData(1_023.6, "1,0 Ko/s")]
    [InlineData(1_048_575, "1,0 Mo/s")]
    public void Formats_a_rate_with_french_units(double bytesPerSecond, string expected)
    {
        Assert.Equal(expected, Units.FormatRate(bytesPerSecond));
    }

    [Theory]
    [InlineData(17_179_869_184L, "16,0 Go")]
    [InlineData(805_306_368L, "768,0 Mo")]
    [InlineData(1_649_267_441_664L, "1,5 To")]
    [InlineData(1_099_511_627_775L, "1,0 To")]
    public void Formats_a_size_with_french_units(long bytes, string expected)
    {
        Assert.Equal(expected, Units.FormatSize(bytes));
    }
}
