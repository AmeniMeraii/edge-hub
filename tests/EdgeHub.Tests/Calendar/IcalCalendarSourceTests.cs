using System.Net;
using EdgeHub.Calendar;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace EdgeHub.Tests.Calendar;

public sealed class IcalCalendarSourceTests : IDisposable
{
    // Invented address: what matters is that it never reaches a log.
    private const string SecretLink = "https://calendar.example.test/ical/demo%40example.test/private-0123456789abcdef/basic.ics";
    private static readonly string Demo = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "calendar", "demo.ics"));
    private static readonly DateOnly Thursday = new(2026, 11, 5);

    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly ScriptedHandler _web = new();
    private readonly CapturingLogger<IcalCalendarSource> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _web.Dispose();

    private IcalCalendarSource Source(string? link = SecretLink) =>
        new(new SingleClientFactory(_web), new FixedOptionsMonitor<CalendarOptions>(new CalendarOptions { IcalUrl = link }), _clock, _log);

    [Fact]
    public async Task Without_a_link_it_reads_nothing()
    {
        IcalCalendarSource source = Source(link: null);

        Assert.False(source.IsConfigured);
        Assert.Empty(await source.GetEventsAsync(Thursday, Ct));
        Assert.Equal(0, _web.Requests);
    }

    [Fact]
    public async Task Downloads_the_calendar_once_every_five_minutes()
    {
        IcalCalendarSource source = Source();

        Assert.Contains(await source.GetEventsAsync(Thursday, Ct), e => e.Title == "🏃 Footing du jeudi");
        _clock.Advance(TimeSpan.FromMinutes(4));
        await source.GetEventsAsync(Thursday, Ct);
        Assert.Equal(1, _web.Requests);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await source.GetEventsAsync(Thursday, Ct);
        Assert.Equal(2, _web.Requests);
    }

    [Fact]
    public async Task Keeps_the_last_calendar_when_a_download_fails()
    {
        IcalCalendarSource source = Source();
        await source.GetEventsAsync(Thursday, Ct);

        _web.Fail = true;
        _clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Contains(await source.GetEventsAsync(Thursday, Ct), e => e.Title == "🏃 Footing du jeudi");
    }

    [Fact]
    public async Task Says_the_calendar_is_unavailable_when_nothing_could_ever_be_read()
    {
        _web.Fail = true;

        await Assert.ThrowsAsync<CalendarUnavailableException>(() => Source().GetEventsAsync(Thursday, Ct));
    }

    [Fact]
    public async Task Never_writes_the_secret_link_in_a_log()
    {
        _web.Fail = true;

        await Assert.ThrowsAsync<CalendarUnavailableException>(() => Source().GetEventsAsync(Thursday, Ct));

        Assert.NotEmpty(_log.Messages);
        Assert.All(_log.Messages, message =>
        {
            Assert.DoesNotContain("private-0123456789abcdef", message, StringComparison.Ordinal);
            Assert.DoesNotContain("calendar.example.test", message, StringComparison.Ordinal);
        });
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(Fail
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Demo) });
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + " " + exception);
    }
}
