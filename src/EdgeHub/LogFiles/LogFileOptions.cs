using System.ComponentModel.DataAnnotations;

namespace EdgeHub.LogFiles;

/// <summary>Section <c>Logs</c>: the service's log file, one per day.</summary>
public sealed class LogFileOptions
{
    public const string Section = "Logs";

    /// <summary>
    /// Folder of the log files, <c>%LOCALAPPDATA%\EdgeHub\logs</c> by default. It may name environment variables; a
    /// relative folder is next to the service. Empty: no log file.
    /// </summary>
    public string Folder { get; set; } = Path.Combine("%LOCALAPPDATA%", "EdgeHub", "logs");

    /// <summary>Days of logs kept: older files are deleted.</summary>
    [Range(1, 365)]
    public int RetainedDays { get; set; } = 14;

    /// <summary>The folder as a full path, or null when the log file is off.</summary>
    public string? ResolvedFolder() =>
        string.IsNullOrWhiteSpace(Folder)
            ? null
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(Folder), AppContext.BaseDirectory);
}
