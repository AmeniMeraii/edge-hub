using System.ComponentModel.DataAnnotations;

namespace EdgeHub.Calendar;

/// <summary>
/// Section <c>Calendar</c>. Without <see cref="IcalUrl"/> the calendar is simply not configured: the screen keeps the
/// manual choice of session. The link is a secret (it gives read access to the whole calendar): it goes in
/// <c>dotnet user-secrets</c> or <c>appsettings.Local.json</c>, never in git, and never in a log or an error message.
/// </summary>
public sealed class CalendarOptions : IValidatableObject
{
    public const string Section = "Calendar";

    /// <summary>The calendar's secret iCal address (Google Calendar: Settings, Integrate calendar).</summary>
    public string? IcalUrl { get; set; }

    /// <summary>The time zone the day and the times are read in.</summary>
    public string TimeZone { get; set; } = "Europe/Paris";

    /// <summary>Optional overrides of the sport rules; anything left out keeps the plan's defaults.</summary>
    public SportRules Sport { get; set; } = new();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(IcalUrl);

    public SportRules EffectiveSport
    {
        get
        {
            SportRules defaults = SportRules.Default;
            return new SportRules
            {
                Kinds = Sport.Kinds.Count > 0 ? Sport.Kinds : defaults.Kinds,
                CancelledMarkers = Sport.CancelledMarkers.Count > 0 ? Sport.CancelledMarkers : defaults.CancelledMarkers,
                CancelledWords = Sport.CancelledWords.Count > 0 ? Sport.CancelledWords : defaults.CancelledWords,
            };
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsConfigured
            && !(Uri.TryCreate(IcalUrl, UriKind.Absolute, out Uri? link) && (link.Scheme == Uri.UriSchemeHttps || link.Scheme == Uri.UriSchemeHttp)))
        {
            // The message never repeats the link: it is a secret.
            yield return new ValidationResult("Calendar:IcalUrl doit être une adresse web complète (https://…).", [nameof(IcalUrl)]);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _))
        {
            yield return new ValidationResult($"Calendar:TimeZone « {TimeZone} » est inconnu (exemple : Europe/Paris).", [nameof(TimeZone)]);
        }
    }
}
