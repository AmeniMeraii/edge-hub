using System.ComponentModel.DataAnnotations;
using EdgeHub.Configuration;

namespace EdgeHub.Journal;

/// <summary>Section <c>Journal</c>: the folder the Claude Code commands write the journal in.</summary>
public sealed class JournalOptions : IValidatableObject
{
    public const string Section = "Journal";

    /// <summary>
    /// <c>%USERPROFILE%\Documents\Journal-activite</c> by default, where <c>/rapport</c>, <c>/prep-reunions</c> and
    /// <c>/decisions</c> write (<c>~/Documents/Journal-activite/</c>). Empty: no journal on the screen.
    /// </summary>
    public string Folder { get; set; } = Path.Combine("%USERPROFILE%", "Documents", "Journal-activite");

    /// <summary>The folder as a full path, or null when the journal is off.</summary>
    public string? ResolvedFolder() => FolderSetting.Resolve(Folder);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FolderSetting.UnknownVariable(Folder) is { } unknown)
        {
            yield return new ValidationResult($"Journal:Folder nomme une variable d'environnement inconnue : {unknown}.", [nameof(Folder)]);
        }
    }
}
