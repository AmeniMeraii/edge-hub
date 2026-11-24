using EdgeHub.Calendar;

namespace EdgeHub.Configuration;

public static class OptionsRegistration
{
    /// <summary>Binds every settings section and checks it when the service starts: a wrong value stops it at once.</summary>
    public static IServiceCollection AddEdgeHubOptions(this IServiceCollection services)
    {
        services.AddOptions<ServerOptions>().BindConfiguration(ServerOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CalendarOptions>().BindConfiguration(CalendarOptions.Section).ValidateDataAnnotations().ValidateOnStart();
        return services;
    }
}
