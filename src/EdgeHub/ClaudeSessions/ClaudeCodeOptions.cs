using System.ComponentModel.DataAnnotations;
using EdgeHub.Configuration;

namespace EdgeHub.ClaudeSessions;

/// <summary>Section <c>ClaudeCode</c>: where Claude Code keeps its sessions.</summary>
public sealed class ClaudeCodeOptions : IValidatableObject
{
    public const string Section = "ClaudeCode";

    /// <summary>
    /// <c>%USERPROFILE%\.claude\projects</c> by default. Only metadata is read there (see
    /// <c>docs/formats/claude-code-sessions.md</c>). Empty: no Claude Code session on the screen.
    /// </summary>
    public string ProjectsFolder { get; set; } = Path.Combine("%USERPROFILE%", ".claude", "projects");

    /// <summary>The folder as a full path, or null when the session reader is off.</summary>
    public string? ResolvedFolder() => FolderSetting.Resolve(ProjectsFolder);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FolderSetting.UnknownVariable(ProjectsFolder) is { } unknown)
        {
            yield return new ValidationResult($"ClaudeCode:ProjectsFolder nomme une variable d'environnement inconnue : {unknown}.", [nameof(ProjectsFolder)]);
        }
    }
}
