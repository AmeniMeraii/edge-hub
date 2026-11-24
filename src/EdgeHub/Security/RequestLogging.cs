namespace EdgeHub.Security;

public static class RequestLogging
{
    /// <summary>The category whose Information logs print each request's address, query string included.</summary>
    public const string Category = "Microsoft.AspNetCore.Hosting.Diagnostics";

    private static readonly string[] BuiltInProviders = ["Console", "Debug", "EventSource", "EventLog"];

    /// <summary>
    /// Keeps request addresses out of every log: the first one the tablet opens carries <c>?k=&lt;token&gt;</c>.
    /// A setting for one provider (<c>Logging:Console:LogLevel</c>) beats a setting without one, so the Warning
    /// minimum is added for each built-in provider and for every provider named in the settings, after they are read.
    /// A logger outside Microsoft.Extensions.Logging (Serilog's own pipeline, for instance) must apply the same rule.
    /// </summary>
    public static ILoggingBuilder KeepRequestAddressesOutOfLogs(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        logging.AddFilter(Category, LogLevel.Warning);
        logging.Services.PostConfigure<LoggerFilterOptions>(options =>
        {
            IEnumerable<string> providers = options.Rules
                .Select(rule => rule.ProviderName)
                .OfType<string>()
                .Concat(BuiltInProviders)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (string provider in providers)
            {
                options.Rules.Add(new LoggerFilterRule(provider, Category, LogLevel.Warning, filter: null));
            }
        });
        return logging;
    }
}
