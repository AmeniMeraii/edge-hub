using System.Text.Json;
using EdgeHub.ClaudeSessions;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdgeHub.Tests.ClaudeSessions;

public sealed class ClaudeCurrentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 30, 0, TimeSpan.Zero);

    private readonly ManualTimeProvider _time = new(Now);
    private readonly FakeSessionReader _reader = new();

    private static readonly ActiveSession Demo = new("DEMO-101", "feature/DEMO-101", Now.AddMinutes(-40), Now.AddMinutes(-1), 3);

    private (ClaudeSessionMonitor Monitor, ClaudeCurrent Current) Build(string folder = @"C:\demo\.claude\projects")
    {
        var options = new FixedOptionsMonitor<ClaudeCodeOptions>(new ClaudeCodeOptions { ProjectsFolder = folder });
        var monitor = new ClaudeSessionMonitor(_reader, _time, NullLogger<ClaudeSessionMonitor>.Instance);
        return (monitor, new ClaudeCurrent(monitor, options, _time));
    }

    [Fact]
    public void Says_when_no_projects_folder_is_set()
    {
        Assert.False(Build(folder: "").Current.Get().Configured);
    }

    [Fact]
    public void Gives_the_active_session_found_by_the_last_pass()
    {
        (ClaudeSessionMonitor monitor, ClaudeCurrent current) = Build();
        _reader.Session = Demo;

        monitor.RefreshOnce();
        ClaudeCurrentMessage message = current.Get();

        Assert.True(message.Configured);
        Assert.True(message.Active);
        Assert.Equal(("DEMO-101", "feature/DEMO-101", 3), (message.Name, message.Branch, message.EditedFiles));
        Assert.Equal(Now.AddMinutes(-40), message.StartedAt);
    }

    [Fact]
    public void Drops_a_session_that_went_quiet_since_the_last_pass()
    {
        (ClaudeSessionMonitor monitor, ClaudeCurrent current) = Build();
        _reader.Session = Demo;
        monitor.RefreshOnce();

        _time.Advance(TimeSpan.FromMinutes(14));

        Assert.False(current.Get().Active);
    }

    [Fact]
    public void Keeps_the_last_session_found_when_a_pass_fails()
    {
        (ClaudeSessionMonitor monitor, ClaudeCurrent current) = Build();
        _reader.Session = Demo;
        monitor.RefreshOnce();

        _reader.Failure = new IOException("disque occupé");
        monitor.RefreshOnce();

        Assert.True(current.Get().Active);
    }

    [Fact]
    public void Sends_the_screen_metadata_only()
    {
        (ClaudeSessionMonitor monitor, ClaudeCurrent current) = Build();
        _reader.Session = Demo;
        monitor.RefreshOnce();

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(current.Get(), JsonSerializerOptions.Web));

        Assert.Equal(
            ["configured", "active", "name", "branch", "startedAt", "lastActivityAt", "editedFiles"],
            json.RootElement.EnumerateObject().Select(property => property.Name));
    }

    private sealed class FakeSessionReader : IClaudeSessionReader
    {
        public ActiveSession? Session { get; set; }

        public Exception? Failure { get; set; }

        public ActiveSession? FindActive() => Failure is { } failure ? throw failure : Session;
    }
}
