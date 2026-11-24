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

var builder = WebApplication.CreateBuilder(args);

// Personal values (token, later the calendar link) live outside git: user secrets in development, or this file.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

ServerOptions server = builder.Configuration.GetSection(ServerOptions.Section).Get<ServerOptions>() ?? new ServerOptions();
builder.WebHost.ConfigureKestrel(kestrel =>
{
    if (server.ListenOnNetwork)
    {
        kestrel.ListenAnyIP(server.Port);
    }
    else
    {
        kestrel.ListenLocalhost(server.Port);
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
app.MapGet("/api/stream", (LiveStateHub hub, CancellationToken cancellationToken) =>
    TypedResults.ServerSentEvents(StreamMessages(hub, cancellationToken), eventType: "state"));

await app.RunAsync();
return 0;

static async IAsyncEnumerable<StreamMessage> StreamMessages(
    LiveStateHub hub, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (LiveState state in hub.Subscribe(cancellationToken).ConfigureAwait(false))
    {
        yield return StreamMessage.From(state);
    }
}
