namespace EdgeHub.Calendar;

/// <summary>Section <c>Calendar:Sport</c>: how a calendar title names a sport session, or a cancelled one.</summary>
public sealed class SportRules
{
    /// <summary>The kinds in the order they are tried: the first one with a matching keyword wins.</summary>
    public List<SportKind> Kinds { get; set; } = [];

    /// <summary>A title that starts with one of these (after spaces) is a cancelled session.</summary>
    public List<string> CancelledMarkers { get; set; } = [];

    /// <summary>A title that contains one of these, whatever the case, is a cancelled session.</summary>
    public List<string> CancelledWords { get; set; } = [];

    /// <summary>The plan's rules (section 4.2), used when the settings give none.</summary>
    public static SportRules Default => new()
    {
        Kinds =
        [
            new SportKind { Name = "Course", Keywords = ["🏃", "course", "footing", "run"] },
            new SportKind { Name = "Muscu", Keywords = ["💪", "muscu", "full body", "salle"] },
            new SportKind { Name = "Marche", Keywords = ["🚶", "marche"] },
        ],
        CancelledMarkers = ["❌"],
        CancelledWords = ["annul"],
    };
}

public sealed class SportKind
{
    public string Name { get; set; } = "";

    public List<string> Keywords { get; set; } = [];
}
