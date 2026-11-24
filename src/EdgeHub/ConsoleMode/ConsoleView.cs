using System.Globalization;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.ConsoleMode;

/// <summary>The lines the console shows for one refresh.</summary>
public static class ConsoleView
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static IReadOnlyList<string> Render(SystemSnapshot stats, MediaState? media, string? status)
    {
        var lines = new List<string>
        {
            "Edge Hub · console      Espace : lecture/pause · N : suivant · P : précédent · Q : quitter",
            "",
            $"CPU      {Math.Round(stats.CpuPercent).ToString("0", French)} %",
            $"RAM      {Units.FormatSize(stats.MemoryUsedBytes)} / {Units.FormatSize(stats.MemoryTotalBytes)}",
            $"Réseau   ↓ {Units.FormatRate(stats.DownloadBytesPerSecond)}  ↑ {Units.FormatRate(stats.UploadBytesPerSecond)}  ({stats.NetworkInterface ?? "aucune interface"})",
        };

        foreach (DiskUsage disk in stats.Disks)
        {
            lines.Add($"Disque   {disk.Name} {Units.FormatSize(disk.FreeBytes)} libres sur {Units.FormatSize(disk.TotalBytes)}");
        }

        lines.Add("");
        if (media is null)
        {
            lines.Add("Spotify  Spotify n'est pas lancé sur le PC");
        }
        else
        {
            string source = media.AppId.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ? "Spotify" : "Média  ";
            lines.Add($"{source}  {(media.IsPlaying ? "▶" : "⏸")} {media.Title} — {media.Artist}");
            lines.Add($"         {Clock(media.Position)} / {Clock(media.Duration)}{(media.Album.Length > 0 ? "  · " + media.Album : "")}");
        }

        if (status is not null)
        {
            lines.Add("");
            lines.Add(status);
        }

        return lines;
    }

    private static string Clock(TimeSpan time) =>
        time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", French);
}
