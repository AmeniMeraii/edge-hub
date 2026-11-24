namespace EdgeHub.Security;

public static class RequestLogging
{
    /// <summary>
    /// Categories whose logs below Warning print addresses: incoming requests (the first one carries ?k=&lt;token&gt;),
    /// malformed request lines, and outgoing calls (the calendar's secret iCal link).
    /// </summary>
    public static readonly IReadOnlyList<string> Categories =
        ["Microsoft.AspNetCore.Hosting.Diagnostics", "Microsoft.AspNetCore.Server.Kestrel.BadRequests", "System.Net.Http.HttpClient"];

    private const string HttpClientCategory = "System.Net.Http.HttpClient";

    /// <summary>The categories below <see cref="HttpClientCategory"/>: System.Net.Http.HttpClient.&lt;client&gt;.LogicalHandler or .ClientHandler.</summary>
    private static readonly string[] HttpClientHandlers = ["LogicalHandler", "ClientHandler"];

    private static readonly string[] BuiltInProviders = ["Console", "Debug", "EventSource", "EventLog", "Serilog"];

    /// <summary>
    /// Keeps addresses out of every log: the first one the tablet opens carries <c>?k=&lt;token&gt;</c>, and the calendar's
    /// link is a secret.
    /// A setting for one provider (<c>Logging:Console:LogLevel</c>) beats a setting without one, so the Warning
    /// minimum is added for each built-in provider, the log file's (<c>Serilog</c>) and every provider named in the
    /// settings, after they are read. The logging then picks the longest matching category, so a longer rule from the
    /// settings would still win, with a wildcard (<c>…Diagnostics*</c>) or a category below
    /// (<c>System.Net.Http.HttpClient.x</c>): such a rule is raised to Warning too. A logger outside
    /// Microsoft.Extensions.Logging would have to apply the same rule.
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

            for (int i = 0; i < options.Rules.Count; i++)
            {
                LoggerFilterRule rule = options.Rules[i];
                if (rule.CategoryName is { } pattern
                    && (rule.LogLevel is null || rule.LogLevel < LogLevel.Warning)
                    && Categories.Any(category => MayOutrank(pattern, category)))
                {
                    options.Rules[i] = new LoggerFilterRule(rule.ProviderName, pattern, LogLevel.Warning, rule.Filter);
                }
            }
        });
        return logging;
    }

    /// <summary>
    /// True when a rule could apply to the category, or to one below it, and beat the Warning rule added for it: it is
    /// longer (the wildcard counts as a character, as in the logging's own choice) and matches the way the logging
    /// matches, the start before the wildcard and the end after it, whatever the case.
    /// </summary>
    private static bool MayOutrank(string pattern, string category)
    {
        if (pattern.Length <= category.Length)
        {
            return false;
        }

        int wildcard = pattern.IndexOf('*', StringComparison.Ordinal);
        string start = wildcard < 0 ? pattern : pattern[..wildcard];
        string end = wildcard < 0 ? "" : pattern[(wildcard + 1)..];
        if (category != HttpClientCategory)
        {
            // A single category, with nothing below it.
            return category.StartsWith(start, StringComparison.OrdinalIgnoreCase) && category.EndsWith(end, StringComparison.OrdinalIgnoreCase);
        }

        // Any client name may sit between the two: only the start and the handler at the end limit what applies.
        bool startFits = start.StartsWith(category, StringComparison.OrdinalIgnoreCase) || category.StartsWith(start, StringComparison.OrdinalIgnoreCase);
        return startFits && HttpClientHandlers.Any(handler =>
            end.EndsWith(handler, StringComparison.OrdinalIgnoreCase) || handler.EndsWith(end, StringComparison.OrdinalIgnoreCase));
    }
}
