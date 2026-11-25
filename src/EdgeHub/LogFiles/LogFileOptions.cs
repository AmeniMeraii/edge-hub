using System.ComponentModel.DataAnnotations;
using EdgeHub.Configuration;

namespace EdgeHub.LogFiles;

/// <summary>Section <c>Logs</c>: the service's log file, one per day.</summary>
public sealed class LogFileOptions : IValidatableObject
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
    public string? ResolvedFolder() => FolderSetting.Resolve(Folder);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FolderSetting.UnknownVariable(Folder) is { } unknown)
        {
            yield return new ValidationResult($"Logs:Folder nomme une variable d'environnement inconnue : {unknown}.", [nameof(Folder)]);
        }
    }
}
