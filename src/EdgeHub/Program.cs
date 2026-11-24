using EdgeHub.Configuration;
using EdgeHub.ConsoleMode;
using EdgeHub.Security;
using EdgeHub.Spotify;
using EdgeHub.Streaming;
using EdgeHub.SystemStats;

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

// Personal values (token, later the calendar link) live outside git: user secrets in development, or
// appsettings.Local.json, which environment variables and the command line can still override.
builder.Configuration.AddLocalSettings();

// Request logs would print each address, and the first one carries ?k=<token>.
builder.Logging.KeepRequestAddressesOutOfLogs();

ServerOptions server = builder.Configuration.GetSection(ServerOptions.Section).Get<ServerOptions>() ?? new ServerOptions();
builder.WebHost.ConfigureKestrel(kestrel =>
{
    foreach (System.Net.IPAddress address in server.ListenAddresses())
    {
        kestrel.Listen(address, server.Port);
    }
});

builder.Services.AddOptions<ServerOptions>().BindConfiguration(ServerOptions.Section).ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.Section).ValidateDataAnnotations().ValidateOnStart();

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
