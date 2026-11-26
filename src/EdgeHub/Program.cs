using EdgeHub.Calendar;
using EdgeHub.ClaudeSessions;
using EdgeHub.Configuration;
using EdgeHub.ConsoleMode;
using EdgeHub.Journal;
using EdgeHub.Metrics;
using EdgeHub.LogFiles;
using EdgeHub.Security;
using EdgeHub.Spotify;
using EdgeHub.Streaming;
using EdgeHub.SystemStats;

if (args.Contains(TokenCommand.Argument))
{
    Console.Write(TokenCommand.Text(AccessToken.Create()));
    return 0;
}

if (args.Contains("--console"))
{
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        // First Ctrl+C: stop cleanly. A second one ends the process at once, in case something hangs.
        if (!stop.IsCancellationRequested)
        {
            e.Cancel = true;
            stop.Cancel();
        }
    };
    return await ConsoleProbe.RunOnWindowsAsync(stop.Token);
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = PublishedContentRoot(),
});

// Personal values (the token, the calendar's secret link) live outside git: user secrets in development, or
// appsettings.Local.json, which environment variables and the command line can still override.
builder.Configuration.AddLocalSettings();

// Logs of requests and of outgoing calls would print addresses: ?k=<token>, the calendar's secret link. The rule
// covers the console and the log file alike.
builder.Logging.AddLogFile();
builder.Logging.KeepRequestAddressesOutOfLogs();

ServerOptions server = builder.Configuration.GetSection(ServerOptions.Section).Get<ServerOptions>() ?? new ServerOptions();
builder.WebHost.ConfigureKestrel(kestrel =>
{
    // The decision is made (and tested) in ServerOptions; ListenLocalhost and ListenAnyIP add Kestrel's fallback
    // when one address family is unavailable.
    if (server.ListenAddresses().All(System.Net.IPAddress.IsLoopback))
    {
        kestrel.ListenLocalhost(server.Port);
    }
    else
    {
        kestrel.ListenAnyIP(server.Port);
    }
});

builder.Services.AddEdgeHubOptions();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISystemReadings, WindowsSystemReadings>();
builder.Services.AddSingleton<ISystemStats, SystemStatsSampler>();
builder.Services.AddSingleton<IMediaController>(services =>
{
    TimeProvider time = services.GetRequiredService<TimeProvider>();
    return new RetryingMediaController(
        async cancellationToken => await WindowsMediaController.CreateAsync(time, cancellationToken).ConfigureAwait(false),
        time);
});
builder.Services.AddHttpClient(IcalCalendarSource.ClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
    .RemoveAllLoggers();
builder.Services.AddSingleton<ICalendarSource, IcalCalendarSource>();
builder.Services.AddSingleton<CalendarToday>();
builder.Services.AddSingleton<JournalReader>();
builder.Services.AddSingleton<JournalToday>();
builder.Services.AddSingleton<IClaudeSessionReader, ClaudeSessionReader>();
builder.Services.AddSingleton<ClaudeSessionMonitor>();
builder.Services.AddHostedService(services => services.GetRequiredService<ClaudeSessionMonitor>());
builder.Services.AddSingleton<ClaudeCurrent>();
builder.Services.AddSingleton<EventLog>();
builder.Services.AddSingleton<EventRecorder>();
builder.Services.AddSingleton<WeekMetrics>();
builder.Services.AddHostedService<PreparationWatch>();
builder.Services.AddSingleton<LiveStateHub>();
builder.Services.AddHostedService<LiveStateService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<AccessTokenMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapGet("/api/stream", (LiveStateHub hub, IHostApplicationLifetime lifetime, CancellationToken requestAborted) =>
    TypedResults.ServerSentEvents(StreamMessages(hub, requestAborted, lifetime.ApplicationStopping), eventType: "state"));

app.MapGet("/api/calendar/today", (CalendarToday today, CancellationToken cancellationToken) => today.GetAsync(cancellationToken));
app.MapGet("/api/journal/today", (JournalToday journal, CancellationToken cancellationToken) => journal.GetAsync(cancellationToken));
app.MapGet("/api/claude/current", (ClaudeCurrent current) => current.Get());
app.MapGet("/api/metrics/week", (WeekMetrics week, CancellationToken cancellationToken) => week.GetAsync(cancellationToken));
// An event is a few dozen bytes: a larger body is refused before it is read.
app.MapPost("/api/events", (EventRequest request, EventRecorder recorder) => recorder.Record(request))
    .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4096));

await app.RunAsync();
return 0;

// Published service: wwwroot and appsettings*.json sit next to the exe, whatever folder it is started from.
// During development (dotnet run, tests), the default content root, the project folder, is kept.
static string? PublishedContentRoot() =>
    Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")) ? AppContext.BaseDirectory : null;

// A stream ends when the screen leaves or when the service stops, so a connected tablet never delays a restart.
static async IAsyncEnumerable<StreamMessage> StreamMessages(
    LiveStateHub hub,
    CancellationToken requestAborted,
    CancellationToken stopping,
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken enumeration = default)
{
    using var ending = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, stopping, enumeration);
    IAsyncEnumerator<LiveState> states = hub.Subscribe(ending.Token).GetAsyncEnumerator(ending.Token);
    try
    {
        while (true)
        {
            try
            {
                if (!await states.MoveNextAsync().ConfigureAwait(false))
                {
                    yield break;
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                yield break;
            }

            yield return StreamMessage.From(states.Current);
        }
    }
    finally
    {
        await states.DisposeAsync().ConfigureAwait(false);
    }
}
