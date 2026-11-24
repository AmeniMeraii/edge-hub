using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;

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
                Kinds = Sport?.Kinds is { Count: > 0 } kinds ? kinds : defaults.Kinds,
                CancelledMarkers = Sport?.CancelledMarkers is { Count: > 0 } markers ? markers : defaults.CancelledMarkers,
                CancelledWords = Sport?.CancelledWords is { Count: > 0 } words ? words : defaults.CancelledWords,
            };
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsConfigured
            && !(Uri.TryCreate(IcalUrl, UriKind.Absolute, out Uri? link) && (link.Scheme == Uri.UriSchemeHttps || link.Scheme == Uri.UriSchemeHttp)))
        {
            // The message never repeats the link: it is a secret.
            yield return new ValidationResult("Calendar:IcalUrl doit être une adresse web complète (http:// ou https://).", [nameof(IcalUrl)]);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _))
        {
            yield return new ValidationResult($"Calendar:TimeZone « {TimeZone} » est inconnu (exemple : Europe/Paris).", [nameof(TimeZone)]);
        }

        // An empty keyword would make every event a session; an empty cancel word or marker would cancel them all.
        bool kindsBroken = Sport?.Kinds is { } kinds && kinds.Any(kind =>
            kind is null || string.IsNullOrWhiteSpace(kind.Name) || kind.Keywords is not { Count: > 0 } || !kind.Keywords.All(IsUsableKeyword));
        if (Sport is null || Sport.Kinds is null || kindsBroken)
        {
            yield return new ValidationResult(
                "Calendar:Sport:Kinds : chaque type de séance a besoin d'un nom et d'au moins un mot-clé ; un mot-clé contient une lettre, un chiffre ou un emoji.",
                [nameof(Sport)]);
        }

        if (Sport?.CancelledMarkers is null || Sport.CancelledWords is null
            || Sport.CancelledMarkers.Concat(Sport.CancelledWords).Any(string.IsNullOrWhiteSpace))
        {
            yield return new ValidationResult(
                "Calendar:Sport : les marqueurs et les mots d'annulation ne peuvent pas être vides.",
                [nameof(Sport)]);
        }
    }

    /// <summary>A keyword made only of spaces or punctuation (« - ») would match far too much.</summary>
    private static bool IsUsableKeyword(string? keyword) =>
        keyword is not null
        && (keyword.TrimEnd('*').Any(char.IsLetterOrDigit)
            || keyword.EnumerateRunes().Any(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherSymbol));
}
