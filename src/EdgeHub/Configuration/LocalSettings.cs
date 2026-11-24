using Microsoft.Extensions.Configuration.Json;

namespace EdgeHub.Configuration;

public static class LocalSettings
{
    public const string FileName = "appsettings.Local.json";

    /// <summary>
    /// Adds <c>appsettings.Local.json</c> (personal values, ignored by git) right after the other settings files, so it
    /// overrides <c>appsettings.json</c> and user secrets, while environment variables and the command line still
    /// override it, as they usually do.
    /// </summary>
    public static IConfigurationBuilder AddLocalSettings(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var local = new JsonConfigurationSource { Path = FileName, Optional = true, ReloadOnChange = true };
        int afterLastFile = builder.Sources
            .Select((source, index) => source is FileConfigurationSource ? index + 1 : 0)
            .DefaultIfEmpty(0)
            .Max();
        builder.Sources.Insert(afterLastFile, local);
        return builder;
    }
}
