using System.Globalization;
using EdgeHub.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
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
    /// file is off, or when its folder cannot be written to: the console's error output says so once, rather than
    /// Serilog failing on every line until the next day. Request and outgoing call logs below Warning never reach the
    /// file, whatever the Logging settings.
    /// </summary>
    public static ILoggerProvider CreateProvider(LogFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string? folder = options.ResolvedFolder();
        if (folder is null)
        {
            return NullLoggerProvider.Instance;
        }

        if (!CanWriteTo(folder))
        {
            Console.Error.WriteLine($"Logs:Folder « {folder} » n'est pas accessible en écriture : pas de fichier de journal pour cette fois.");
            return NullLoggerProvider.Instance;
        }

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

    private static bool CanWriteTo(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            using var probe = new FileStream(Path.Combine(folder, $".edgehub-{Guid.NewGuid():N}.tmp"), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
