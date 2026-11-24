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

    [Theory]
    // A wildcard makes a rule longer than the exact category, so it would win.
    [InlineData("Logging:Capture:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics*", "Microsoft.AspNetCore.Hosting.Diagnostics")]
    [InlineData("Logging:Capture:LogLevel:*Microsoft.AspNetCore.Hosting.Diagnostics", "Microsoft.AspNetCore.Hosting.Diagnostics")]
    [InlineData("Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diag*nostics", "Microsoft.AspNetCore.Hosting.Diagnostics")]
    // A longer category under a quiet one.
    [InlineData("Logging:Capture:LogLevel:System.Net.Http.HttpClient.calendar", "System.Net.Http.HttpClient.calendar.LogicalHandler")]
    // A malformed request line is logged with its address.
    [InlineData("Logging:Capture:LogLevel:Default", "Microsoft.AspNetCore.Server.Kestrel.BadRequests")]
    public void No_setting_lets_an_address_through(string setting, string category)
    {
        var capture = new CaptureLoggerProvider();
        using ServiceProvider services = Services(capture, new() { [setting] = "Trace" });
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(category);

        Information(logger, "GET /?k=secret");
        logger.Log(LogLevel.Debug, default, "GET /?k=secret", null, (state, _) => state);

        Assert.Empty(capture.Messages);
    }

    [Theory]
    [InlineData("EdgeHub.*", "EdgeHub.Calendar.IcalCalendarSource")]
    // Longer than the quiet categories, but they cannot apply to them.
    [InlineData("*EdgeHub.Calendar.IcalCalendarSource", "EdgeHub.Calendar.IcalCalendarSource")]
    [InlineData("Microsoft.AspNetCore.*Routing.EndpointMiddleware", "Microsoft.AspNetCore.Routing.EndpointMiddleware")]
    [InlineData("Microsoft.AspNetCore.Server.Kestrel.*Connections", "Microsoft.AspNetCore.Server.Kestrel.Connections")]
    public void A_wildcard_rule_elsewhere_keeps_its_level(string pattern, string category)
    {
        var capture = new CaptureLoggerProvider();
        using ServiceProvider services = Services(capture, new() { ["Logging:Capture:LogLevel:" + pattern] = "Debug" });

        services.GetRequiredService<ILoggerFactory>().CreateLogger(category)
            .Log(LogLevel.Debug, default, "Debug line", null, (state, _) => state);

        Assert.Equal(["Debug line"], capture.Messages);
    }

    private static ServiceProvider Services(CaptureLoggerProvider capture, Dictionary<string, string?> values)
    {
        IConfiguration settings = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new ServiceCollection()
            .AddLogging(logging =>
            {
                logging.AddConfiguration(settings.GetSection("Logging"));
                logging.AddProvider(capture);
                logging.KeepRequestAddressesOutOfLogs();
            })
            .BuildServiceProvider();
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
