using System.ComponentModel.DataAnnotations;
using System.Globalization;
using EdgeHub.Configuration;

namespace EdgeHub.Metrics;

/// <summary>
/// Section <c>Metrics</c>: the weekly indicators of <c>docs/product/indicateurs.md</c>, measured on this PC only. The
/// screen's events go to <c>events.jsonl</c> in <see cref="Folder"/>.
/// </summary>
public sealed class MetricsOptions : IValidatableObject
{
    public const string Section = "Metrics";

    /// <summary>False: nothing is written and the screen hides « Ma semaine ».</summary>
    public bool Enabled { get; set; } = true;

    /// <summary><c>%LOCALAPPDATA%\EdgeHub</c> by default. It may name environment variables; empty: no measure.</summary>
    public string Folder { get; set; } = Path.Combine("%LOCALAPPDATA%", "EdgeHub");

    /// <summary>The time of the daily meeting (<c>09:30</c>), to tell a preparation done before it. Empty: not measured.</summary>
    public string? DailyTime { get; set; }

    /// <summary>The folder as a full path, or null when the measure is off.</summary>
    public string? ResolvedFolder() => Enabled ? FolderSetting.Resolve(Folder) : null;

    public TimeOnly? DailyAt() =>
        TimeOnly.TryParseExact(DailyTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly at) ? at : null;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FolderSetting.UnknownVariable(Folder) is { } unknown)
        {
            yield return new ValidationResult($"Metrics:Folder nomme une variable d'environnement inconnue : {unknown}.", [nameof(Folder)]);
        }

        if (!string.IsNullOrWhiteSpace(DailyTime) && DailyAt() is null)
        {
            yield return new ValidationResult($"Metrics:DailyTime « {DailyTime} » doit être une heure au format HH:mm (exemple : 09:30).", [nameof(DailyTime)]);
        }
    }
}
