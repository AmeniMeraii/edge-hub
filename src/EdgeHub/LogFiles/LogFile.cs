using System.Globalization;
using EdgeHub.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace EdgeHub.LogFiles;

public static class LogFile
{
    /// <summary>At most 100 files of 10 MB, 1 GB in all: past that, the oldest go first, whatever their age.</summary>
    public const int MaxFiles = 100;

    /// <summary>
    /// Adds the log file next to the console. Serilog only writes the file: it is one more provider of the built-in
    /// logging, named <c>Serilog</c>, so the <c>Logging</c> settings and the rule that keeps addresses out of the logs
    /// apply to it as to the console.
    /// </summary>
    public static ILoggingBuilder AddLogFile(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        // Created from the final settings, once the service starts; the container disposes it, which closes the file.
        logging.Services.AddSingleton(services =>
        {
            try
            {
                return CreateProvider(services.GetRequiredService<IOptions<LogFileOptions>>().Value);
            }
            catch (OptionsValidationException)
            {
                // No file this time: the startup check reports the wrong setting, with the others, in the console.
                return NullLoggerProvider.Instance;
            }
        });
        return logging;
    }

    /// <summary>
    /// One file per day (<c>edgehub-20261124.log</c>), a new one past 10 MB, deleted after
    /// <see cref="LogFileOptions.RetainedDays"/>, <see cref="MaxFiles"/> files at most. No provider at all when the log
    /// file is off. Request and outgoing call logs below Warning never reach the file, whatever the Logging settings.
    /// </summary>
    public static ILoggerProvider CreateProvider(LogFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string? folder = options.ResolvedFolder();
        if (folder is null)
        {
            return NullLoggerProvider.Instance;
        }

        // Serilog reports its own failures on the console's error output: an unwritable folder means no file until the
        // next day.
        SelfLog.Enable(Console.Error);

        // Every level reaches Serilog, since the Logging settings have already chosen what to write, except for the
        // categories that print addresses: the file applies that rule itself, out of reach of the settings.
        LoggerConfiguration configuration = new LoggerConfiguration().MinimumLevel.Verbose();
        foreach (string category in RequestLogging.Categories)
        {
            configuration.MinimumLevel.Override(category, LogEventLevel.Warning);
        }

        Serilog.Core.Logger logger = configuration
            .WriteTo.File(
                Path.Combine(folder, "edgehub-.log"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: MaxFiles,
                retainedFileTimeLimit: TimeSpan.FromDays(options.RetainedDays))
            .CreateLogger();
        return new SerilogLoggerProvider(logger, dispose: true);
    }
}
