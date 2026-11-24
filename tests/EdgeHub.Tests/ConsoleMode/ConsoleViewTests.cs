using EdgeHub.ConsoleMode;
using EdgeHub.Spotify;
using EdgeHub.SystemStats;

namespace EdgeHub.Tests.ConsoleMode;

public sealed class ConsoleViewTests
{
    private static readonly SystemSnapshot Stats = new(
        CpuPercent: 23.4,
        MemoryUsedBytes: 10L << 30,
        MemoryTotalBytes: 16L << 30,
        DownloadBytesPerSecond: 1_048_576,
        UploadBytesPerSecond: 50_000,
        NetworkInterface: "Wi-Fi",
        Disks: [new DiskUsage("C:", TotalBytes: 500L << 30, FreeBytes: 120L << 30)]);

    private static MediaState Track(bool isPlaying) => new(
        AppId: "Spotify.exe",
        Title: "Demo Song",
        Artist: "Demo Artist",
        Album: "Demo Album",
        IsPlaying: isPlaying,
        Position: TimeSpan.FromSeconds(83),
        Duration: TimeSpan.FromSeconds(225));

    private static string Render(MediaState? media, string? status = null) =>
        string.Join('\n', ConsoleView.Render(Stats, media, status));

    [Fact]
    public void Shows_cpu_memory_network_and_disks()
    {
        string screen = Render(media: null);

        Assert.Contains("23 %", screen);
        Assert.Contains("10,0 Go / 16,0 Go", screen);
        Assert.Contains("↓ 1,0 Mo/s", screen);
        Assert.Contains("↑ 48,8 Ko/s", screen);
        Assert.Contains("Wi-Fi", screen);
        Assert.Contains("C: 120,0 Go libres sur 500,0 Go", screen);
    }

    [Fact]
    public void Shows_the_track_its_state_and_its_progress()
    {
        string screen = Render(Track(isPlaying: true));

        Assert.Contains("▶ Demo Song — Demo Artist", screen);
        Assert.Contains("1:23 / 3:45", screen);
    }

    [Fact]
    public void Shows_the_pause_sign_when_paused()
    {
        Assert.Contains("⏸ Demo Song", Render(Track(isPlaying: false)));
    }

    [Fact]
    public void Says_spotify_is_not_running_when_there_is_no_session()
    {
        Assert.Contains("Spotify n'est pas lancé sur le PC", Render(media: null));
    }

    [Fact]
    public void Shows_the_result_of_the_last_command()
    {
        Assert.Contains("Suivant envoyé", Render(Track(isPlaying: true), status: "Suivant envoyé"));
    }
}
