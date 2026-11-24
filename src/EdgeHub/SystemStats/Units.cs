using System.Globalization;

namespace EdgeHub.SystemStats;

/// <summary>Sizes and rates in French units (o, Ko, Mo, Go, To), 1 Ko = 1024 octets.</summary>
public static class Units
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly string[] Prefixes = ["", "K", "M", "G", "T"];

    public static string FormatRate(double bytesPerSecond) => Format(bytesPerSecond) + "/s";

    public static string FormatSize(long bytes) => Format(bytes);

    private static string Format(double bytes)
    {
        // Compare the value as it will be printed, so 1023,96 Ko becomes "1,0 Mo" and never "1024,0 Ko".
        int prefix = 0;
        while (prefix < Prefixes.Length - 1 && Math.Round(bytes, prefix == 0 ? 0 : 1) >= 1024)
        {
            bytes /= 1024;
            prefix++;
        }

        string number = prefix == 0
            ? Math.Round(bytes).ToString("0", French)
            : bytes.ToString("0.0", French);

        return $"{number} {Prefixes[prefix]}o";
    }
}
