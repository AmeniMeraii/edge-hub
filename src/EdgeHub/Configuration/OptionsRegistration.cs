using EdgeHub.Calendar;
using EdgeHub.ClaudeSessions;
using EdgeHub.Journal;
using EdgeHub.Metrics;
using EdgeHub.LogFiles;

namespace EdgeHub.Configuration;

public static class OptionsRegistration
{
    /// <summary>Binds every settings section and checks it when the service starts: a wrong value stops it at once.</summary>
    public static IServiceCollection AddEdgeHubOptions(this IServiceCollection services)
    {
        services.AddOptions<ServerOptions>().BindConfiguration(ServerOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CalendarOptions>().BindConfiguration(CalendarOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<JournalOptions>().BindConfiguration(JournalOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ClaudeCodeOptions>().BindConfiguration(ClaudeCodeOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MetricsOptions>().BindConfiguration(MetricsOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<LogFileOptions>().BindConfiguration(LogFileOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        return services;
    }
}
