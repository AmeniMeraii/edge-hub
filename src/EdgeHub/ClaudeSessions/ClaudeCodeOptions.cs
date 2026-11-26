using System.ComponentModel.DataAnnotations;
using EdgeHub.ClaudeHooks;
using EdgeHub.Configuration;

namespace EdgeHub.ClaudeSessions;

/// <summary>Section <c>ClaudeCode</c>: where Claude Code keeps its sessions, and its hooks (phase 4c).</summary>
public sealed class ClaudeCodeOptions : IValidatableObject
{
    public const string Section = "ClaudeCode";

    public const string SameTokenMessage = $"ClaudeCode:HookTokenHash est l'empreinte du jeton de la tablette : les hooks ont leur propre jeton, {HookHowTo}.";

    private const string HookHowTo = "lance EdgeHub.exe --new-hook-token (ou dotnet run --project src/EdgeHub -- --new-hook-token)";

    /// <summary>
    /// <c>%USERPROFILE%\.claude\projects</c> by default. Only metadata is read there (see
    /// <c>docs/formats/claude-code-sessions.md</c>). Empty: no Claude Code session on the screen.
    /// </summary>
    public string ProjectsFolder { get; set; } = Path.Combine("%USERPROFILE%", ".claude", "projects");

    /// <summary>
    /// SHA-256 of the token the hooks send (<c>--new-hook-token</c>), never the tablet's. Empty: the hooks are off and
    /// their routes answer 404.
    /// </summary>
    [RegularExpression("^([0-9a-fA-F]{64})?$", ErrorMessage = $"ClaudeCode:HookTokenHash doit être vide ou l'empreinte d'un jeton (64 caractères hexadécimaux) : {HookHowTo}.")]
    public string HookTokenHash { get; set; } = "";

    /// <summary>How long a permission request waits for the tablet before Claude Code asks on the PC.</summary>
    [Range(1, 600, ErrorMessage = "ClaudeCode:PermissionTimeoutSeconds doit être entre 1 et 600 secondes (60 par défaut), et rester sous le timeout du hook.")]
    public int PermissionTimeoutSeconds { get; set; } = 60;

    /// <summary>The commands the tablet may not allow. Null: <see cref="SensitiveRules.Defaults"/>; a list replaces them.</summary>
    public List<SensitiveRule>? SensitiveCommands { get; set; }

    /// <summary>Where <c>decisions.log</c> goes: <c>%LOCALAPPDATA%\EdgeHub</c> by default. Empty: no log.</summary>
    public string DecisionsFolder { get; set; } = Path.Combine("%LOCALAPPDATA%", "EdgeHub");

    /// <summary>The folder as a full path, or null when the session reader is off.</summary>
    public string? ResolvedFolder() => FolderSetting.Resolve(ProjectsFolder);

    public string? ResolvedDecisionsFolder() => FolderSetting.Resolve(DecisionsFolder);

    public SensitiveRules Rules() => new(SensitiveCommands ?? SensitiveRules.Defaults);

    /// <summary>Checked at startup with the hash of the tablet's token: one token must never open both doors.</summary>
    public static bool UsesItsOwnToken(ClaudeCodeOptions hooks, string? tabletTokenHash)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        return string.IsNullOrEmpty(hooks.HookTokenHash) || !string.Equals(hooks.HookTokenHash, tabletTokenHash, StringComparison.OrdinalIgnoreCase);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FolderSetting.UnknownVariable(ProjectsFolder) is { } unknown)
        {
            yield return new ValidationResult($"ClaudeCode:ProjectsFolder nomme une variable d'environnement inconnue : {unknown}.", [nameof(ProjectsFolder)]);
        }

        if (FolderSetting.UnknownVariable(DecisionsFolder) is { } unknownDecisions)
        {
            yield return new ValidationResult($"ClaudeCode:DecisionsFolder nomme une variable d'environnement inconnue : {unknownDecisions}.", [nameof(DecisionsFolder)]);
        }

        for (int i = 0; i < (SensitiveCommands?.Count ?? 0); i++)
        {
            SensitiveRule rule = SensitiveCommands![i];
            if (SensitiveRules.PatternError(rule.Pattern) is { } error)
            {
                yield return new ValidationResult($"ClaudeCode:SensitiveCommands:{i}:Pattern n'est pas une expression régulière valide : {error}", [nameof(SensitiveCommands)]);
            }

            if (string.IsNullOrWhiteSpace(rule.Reason))
            {
                yield return new ValidationResult($"ClaudeCode:SensitiveCommands:{i}:Reason est vide : la carte de la tablette doit dire pourquoi la commande est sensible.", [nameof(SensitiveCommands)]);
            }
        }
    }
}
