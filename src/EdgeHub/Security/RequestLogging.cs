namespace EdgeHub.Security;

public static class RequestLogging
{
    /// <summary>
    /// Categories whose Information logs print addresses: incoming requests (the first one carries ?k=&lt;token&gt;) and
    /// outgoing calls (the calendar's secret iCal link).
    /// </summary>
    public static readonly IReadOnlyList<string> Categories = ["Microsoft.AspNetCore.Hosting.Diagnostics", "System.Net.Http.HttpClient"];

    private static readonly string[] BuiltInProviders = ["Console", "Debug", "EventSource", "EventLog", "Serilog"];

    /// <summary>
    /// Keeps addresses out of every log: the first one the tablet opens carries <c>?k=&lt;token&gt;</c>, and the calendar's
    /// link is a secret.
    /// A setting for one provider (<c>Logging:Console:LogLevel</c>) beats a setting without one, so the Warning
    /// minimum is added for each built-in provider, the log file's (<c>Serilog</c>) and every provider named in the
    /// settings, after they are read. A logger outside Microsoft.Extensions.Logging would have to apply the same rule.
    /// </summary>
    public static ILoggingBuilder KeepRequestAddressesOutOfLogs(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);
        foreach (string category in Categories)
        {
            logging.AddFilter(category, LogLevel.Warning);
        }

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
                foreach (string category in Categories)
                {
                    options.Rules.Add(new LoggerFilterRule(provider, category, LogLevel.Warning, filter: null));
                }
            }
        });
        return logging;
    }
}
