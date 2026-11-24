using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace EdgeHub.LogFiles;

/// <summary>Section <c>Logs</c>: the service's log file, one per day.</summary>
public sealed partial class LogFileOptions : IValidatableObject
{
    public const string Section = "Logs";

    /// <summary>
    /// Folder of the log files, <c>%LOCALAPPDATA%\EdgeHub\logs</c> by default. It may name environment variables; a
    /// relative folder is next to the service. Empty: no log file.
    /// </summary>
    public string Folder { get; set; } = Path.Combine("%LOCALAPPDATA%", "EdgeHub", "logs");

    /// <summary>Days of logs kept: older files are deleted.</summary>
    [Range(1, 90, ErrorMessage = "Logs:RetainedDays doit être entre 1 et 90 jours.")]
    public int RetainedDays { get; set; } = 14;

    /// <summary>The folder as a full path, or null when the log file is off.</summary>
    public string? ResolvedFolder() =>
        string.IsNullOrWhiteSpace(Folder)
            ? null
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(Folder), AppContext.BaseDirectory);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Windows leaves an unknown variable as it is: the logs would go to a folder named after it.
        Match unknown = string.IsNullOrWhiteSpace(Folder) ? Match.Empty : UnknownVariable().Match(Environment.ExpandEnvironmentVariables(Folder));
        if (unknown.Success)
        {
            yield return new ValidationResult($"Logs:Folder nomme une variable d'environnement inconnue : {unknown.Value}.", [nameof(Folder)]);
        }
    }

    [GeneratedRegex(@"%[^%\/]+%")]
    private static partial Regex UnknownVariable();
}
