using EdgeHub.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeHub.Tests.Security;

public sealed class RequestLoggingTests
{
    [Fact]
    public void Request_and_outgoing_call_logs_stay_quiet_even_when_a_provider_is_set_to_information()
    {
        var capture = new CaptureLoggerProvider();
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:Capture:LogLevel:Default"] = "Information" })
            .Build();
        using ServiceProvider services = new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.AddConfiguration(settings.GetSection("Logging"));
                logging.AddProvider(capture);
                logging.KeepRequestAddressesOutOfLogs();
            })
            .BuildServiceProvider();
        var loggers = services.GetRequiredService<ILoggerFactory>();

        Information(loggers.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics"), "Request starting GET /?k=secret");
        Information(loggers.CreateLogger("System.Net.Http.HttpClient.calendar.LogicalHandler"), "Start processing GET https://calendar.example.test/private.ics");
        Information(loggers.CreateLogger("EdgeHub.Anything"), "Other information");

        Assert.Equal(["Other information"], capture.Messages);
    }

    private static void Information(ILogger logger, string message) =>
        logger.Log(LogLevel.Information, default, message, null, (state, _) => state);

    [ProviderAlias("Capture")]
    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                messages.Add(formatter(state, exception));
        }
    }
}
