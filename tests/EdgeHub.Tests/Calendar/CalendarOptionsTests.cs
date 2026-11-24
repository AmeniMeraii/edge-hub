using System.ComponentModel.DataAnnotations;
using EdgeHub.Calendar;

namespace EdgeHub.Tests.Calendar;

public sealed class CalendarOptionsTests
{
    private static List<ValidationResult> Validate(CalendarOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Without_a_link_the_calendar_is_simply_not_configured()
    {
        var options = new CalendarOptions();

        Assert.False(options.IsConfigured);
        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData("not a link")]
    [InlineData("calendar.example.test/private/basic.ics")]
    [InlineData("ftp://calendar.example.test/basic.ics")]
    public void Refuses_a_link_that_is_not_a_full_web_address_without_repeating_it(string link)
    {
        ValidationResult error = Assert.Single(Validate(new CalendarOptions { IcalUrl = link }));

        Assert.Contains("Calendar:IcalUrl", error.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(link, error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_an_unknown_time_zone()
    {
        ValidationResult error = Assert.Single(Validate(new CalendarOptions { TimeZone = "Mars/Olympus_Mons" }));

        Assert.Contains("Calendar:TimeZone", error.ErrorMessage, StringComparison.Ordinal);
    }

    public static TheoryData<SportRules> BrokenRules => new()
    {
        new SportRules { Kinds = [new SportKind { Name = "Yoga", Keywords = ["yoga", ""] }] },
        new SportRules { Kinds = [new SportKind { Name = "", Keywords = ["yoga"] }] },
        new SportRules { Kinds = [new SportKind { Name = "Yoga", Keywords = [] }] },
        new SportRules { Kinds = [new SportKind { Name = "Yoga", Keywords = null! }] },
        new SportRules { CancelledWords = ["  "] },
        new SportRules { CancelledMarkers = [""] },
        new SportRules { CancelledWords = null! },
    };

    [Theory]
    [MemberData(nameof(BrokenRules))]
    public void Refuses_sport_rules_that_would_match_or_cancel_everything(SportRules rules)
    {
        ValidationResult error = Assert.Single(Validate(new CalendarOptions { Sport = rules }));

        Assert.Contains("Calendar:Sport", error.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Uses_the_plan_rules_for_whatever_the_settings_leave_out()
    {
        var options = new CalendarOptions { Sport = new SportRules { CancelledWords = ["reporté"] } };

        SportRules rules = options.EffectiveSport;

        Assert.Equal(["reporté"], rules.CancelledWords);
        Assert.Equal(["❌"], rules.CancelledMarkers);
        Assert.Equal(["Course", "Muscu", "Marche"], rules.Kinds.Select(k => k.Name));
    }
}
