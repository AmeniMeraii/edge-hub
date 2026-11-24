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
    public async Task Keeps_the_last_calendar_when_a_download_is_not_a_calendar()
    {
        // A captive portal or a proxy can answer 200 with an HTML page.
        IcalCalendarSource source = Source();
        await source.GetEventsAsync(Thursday, Ct);

        _web.Body = "<!DOCTYPE html><html><body>Connexion au Wi-Fi</body></html>";
        _clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Contains(await source.GetEventsAsync(Thursday, Ct), e => e.Title == "🏃 Footing du jeudi");
    }

    [Fact]
    public async Task Waits_a_minute_before_trying_again_after_a_failed_download()
    {
        _web.Fail = true;
        IcalCalendarSource source = Source();
        await Assert.ThrowsAsync<CalendarUnavailableException>(() => source.GetEventsAsync(Thursday, Ct));

        _clock.Advance(TimeSpan.FromSeconds(30));
        await Assert.ThrowsAsync<CalendarUnavailableException>(() => source.GetEventsAsync(Thursday, Ct));
        Assert.Equal(1, _web.Requests);

        _web.Fail = false;
        _clock.Advance(TimeSpan.FromSeconds(31));
        Assert.NotEmpty(await source.GetEventsAsync(Thursday, Ct));
        Assert.Equal(2, _web.Requests);
    }

    [Fact]
    public async Task A_refused_download_is_logged_once()
    {
        _web.Fail = true;

        await Assert.ThrowsAsync<CalendarUnavailableException>(() => Source().GetEventsAsync(Thursday, Ct));

        Assert.Single(_log.Messages);
    }

    [Fact]
    public async Task Stops_serving_a_kept_calendar_older_than_a_day()
    {
        IcalCalendarSource source = Source();
        await source.GetEventsAsync(Thursday, Ct);

        _web.Fail = true;
        _clock.Advance(TimeSpan.FromHours(23));
        await source.GetEventsAsync(Thursday, Ct);
        _clock.Advance(TimeSpan.FromHours(2));

        await Assert.ThrowsAsync<CalendarUnavailableException>(() => source.GetEventsAsync(Thursday, Ct));
    }

    [Fact]
    public async Task Says_when_the_calendar_was_last_read()
    {
        IcalCalendarSource source = Source();
        Assert.Null(source.LastReadAt);

        await source.GetEventsAsync(Thursday, Ct);
        _web.Fail = true;
        _clock.Advance(TimeSpan.FromMinutes(10));
        await source.GetEventsAsync(Thursday, Ct);

        Assert.Equal(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero), source.LastReadAt);
    }

    [Fact]
    public async Task Any_failure_while_downloading_counts_as_unreachable()
    {
        _web.Throw = new InvalidOperationException("The character set provided in ContentType is invalid.");

        await Assert.ThrowsAsync<CalendarUnavailableException>(() => Source().GetEventsAsync(Thursday, Ct));
    }

    [Fact]
    public async Task Says_the_calendar_is_unavailable_when_nothing_could_ever_be_read()
    {
        _web.Fail = true;

        await Assert.ThrowsAsync<CalendarUnavailableException>(() => Source().GetEventsAsync(Thursday, Ct));
    }

    [Fact]
    public async Task Never_writes_the_secret_link_in_a_log_even_when_an_error_message_holds_it()
    {
        _web.Throw = new HttpRequestException($"Connection refused ({SecretLink})");

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

        public string Body { get; set; } = Demo;

        public Exception? Throw { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Throw is not null)
            {
                return Task.FromException<HttpResponseMessage>(Throw);
            }

            return Task.FromResult(Fail
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Body) });
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
