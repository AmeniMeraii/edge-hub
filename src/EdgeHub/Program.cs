using EdgeHub.ConsoleMode;

if (args.Contains("--console"))
{
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        stop.Cancel();
    };
    return await ConsoleProbe.RunOnWindowsAsync(stop.Token);
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

await app.RunAsync();
return 0;
