using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Extensions.Logging;

namespace EdgeHub.LogFiles;

public static class LogFile
{
    /// <summary>
    /// Adds the log file next to the console. Serilog only writes the file: it is one more provider of the built-in
    /// logging, named <c>Serilog</c>, so the <c>Logging</c> settings and the rule that keeps addresses out of the logs
    /// apply to it as to the console.
    /// </summary>
    public static ILoggingBuilder AddLogFile(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        // Created from the final settings, once the service starts; the container disposes it, which closes the file.
        logging.Services.AddSingleton(services => CreateProvider(services.GetRequiredService<IOptions<LogFileOptions>>().Value));
        return logging;
    }

    /// <summary>
    /// One file per day (<c>edgehub-20261124.log</c>), a new one past 10 MB, deleted after
    /// <see cref="LogFileOptions.RetainedDays"/>, 31 files at most. No provider at all when the log file is off.
    /// </summary>
    public static ILoggerProvider CreateProvider(LogFileOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        string? folder = options.ResolvedFolder();
        if (folder is null)
        {
            return NullLoggerProvider.Instance;
        }

        Serilog.Core.Logger logger = new LoggerConfiguration()
            // Every level reaches Serilog: the Logging settings have already chosen what to write.
            .MinimumLevel.Verbose()
            .WriteTo.File(
                Path.Combine(folder, "edgehub-.log"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                retainedFileTimeLimit: TimeSpan.FromDays(options.RetainedDays))
            .CreateLogger();
        return new SerilogLoggerProvider(logger, dispose: true);
    }
}
