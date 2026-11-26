using System.Text.Json;
using System.Text.RegularExpressions;

namespace EdgeHub.ClaudeHooks;

/// <summary>A command pattern (a regular expression, case ignored) that makes a request sensitive, and why, in French.</summary>
public sealed record SensitiveRule(string Pattern, string Reason);

/// <summary>Whether the tablet may not allow a request and, when it may not, why: the card shows the reason.</summary>
public sealed record Sensitivity(bool IsSensitive, string? Reason)
{
    public static Sensitivity None { get; } = new(false, null);
}

/// <summary>
/// Tells the requests the tablet may not allow (PLAN 4.7, US-503): a command matching one of the rules (push --force,
/// reset --hard, recursive deletes; the settings can replace them), and always whatever touches a <c>.git</c> folder or
/// reaches outside the project's folder: an absolute path elsewhere, <c>..</c> going up, <c>~</c>, a variable such as
/// <c>%USERPROFILE%</c>. In doubt, a request is sensitive: the question then goes back to the PC.
/// </summary>
public sealed partial class SensitiveRules
{
    public const string Outside = "hors du dossier du projet";
    public const string GitFolder = "touche au dossier .git";
    public const string NoProject = "dossier du projet inconnu";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>Where a tool names the file it works on.</summary>
    private static readonly string[] FileFields = ["file_path", "notebook_path", "path"];

    /// <summary>Variables that name a folder outside any project, wherever they appear.</summary>
    private static readonly HashSet<string> OutsideVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "USERPROFILE", "HOME", "HOMEPATH", "HOMEDRIVE", "APPDATA", "LOCALAPPDATA", "TEMP", "TMP", "TMPDIR",
        "PROGRAMFILES", "ProgramFiles(x86)", "PROGRAMDATA", "SYSTEMROOT", "SYSTEMDRIVE", "WINDIR", "ONEDRIVE",
        "PUBLIC", "ALLUSERSPROFILE",
    };

    /// <summary>Variables that name the current folder: a path built on them stays where the command runs.</summary>
    private static readonly HashSet<string> HereVariables = new(StringComparer.OrdinalIgnoreCase) { "PWD", "CD", "PSScriptRoot" };

    // Each rule looks inside one command of a chain: the text between two of | ; & and line breaks.
    public static IReadOnlyList<SensitiveRule> Defaults { get; } =
    [
        new(@"\bgit\b[^|;&\n]*\spush\b[^|;&\n]*\s(?:--force\S*|-[a-z]*f[a-z]*(?=\s|$)|\+\S)", "push forcé"),
        new(@"\bgit\b[^|;&\n]*\sreset\b[^|;&\n]*\s--hard\b", "reset --hard"),
        new(@"\bgit\b[^|;&\n]*\sclean\b[^|;&\n]*\s(?:-[a-z]*f|--force)", "git clean"),
        // rm -r, -rf, -fr, --recursive; Remove-Item and its aliases with -Recurse (or -r, -rec…).
        new(@"\b(?:rm|remove-item|ri|rmdir|rd|del|erase)\b[^|;&\n]*\s(?:-r|--recursive|-(?-i:[dfiv]+[rR]))", "suppression récursive"),
        new(@"\b(?:del|erase|rmdir|rd)\b[^|;&\n]*\s/s\b", "suppression récursive"),
    ];

    private readonly (Regex Pattern, string Reason)[] _rules;

    public SensitiveRules(IEnumerable<SensitiveRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules
            .Select(rule => (new Regex(rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout), rule.Reason))
            .ToArray();
    }

    /// <summary>The pattern's error in French, or null when it is a valid regular expression.</summary>
    public static string? PatternError(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return "le motif est vide";
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
            return null;
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }
    }

    /// <param name="toolName">The tool Claude Code asks for; not needed to judge, the input says it all.</param>
    /// <param name="toolInput">The tool's input: its <c>command</c>, or the file it writes.</param>
    /// <param name="cwd">The session's folder, taken as the project's folder.</param>
    public Sensitivity Check(string? toolName, JsonElement? toolInput, string? cwd)
    {
        _ = toolName;
        var reasons = new List<string>();
        string? command = Text(toolInput, "command");
        if (command is not null)
        {
            CheckCommand(command, cwd, reasons);
        }

        foreach (string file in FileFields.Select(field => Text(toolInput, field)).OfType<string>())
        {
            CheckFile(file, cwd, reasons);
        }

        return reasons.Count == 0 ? Sensitivity.None : new Sensitivity(true, string.Join(", ", reasons.Distinct(StringComparer.Ordinal)));
    }

    private void CheckCommand(string command, string? cwd, List<string> reasons)
    {
        foreach ((Regex pattern, string reason) in _rules)
        {
            try
            {
                if (pattern.IsMatch(command))
                {
                    reasons.Add(reason);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Too long to tell: sensitive.
                reasons.Add(reason);
            }
        }

        if (GitFolderWord().IsMatch(command))
        {
            reasons.Add(GitFolder);
        }

        if (cwd is null)
        {
            reasons.Add(NoProject);
        }
        else if (NamesAFolderElsewhere(command) || Words().Matches(command).Any(word => LeavesProject(word.Value.Trim('"', '\''), cwd)))
        {
            reasons.Add(Outside);
        }
    }

    private static void CheckFile(string file, string? cwd, List<string> reasons)
    {
        if (cwd is null)
        {
            reasons.Add(NoProject);
            return;
        }

        if (ProjectPaths.Full(file, cwd) is not { } full || !ProjectPaths.IsInside(full, cwd))
        {
            reasons.Add(Outside);
            return;
        }

        if (full.Split(Path.DirectorySeparatorChar).Any(part => part.Equals(".git", StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add(GitFolder);
        }
    }

    private static bool LeavesProject(string word, string project)
    {
        if (word.StartsWith('~'))
        {
            return true;
        }

        string? path = AsPath(word);
        return path is not null && (ProjectPaths.Full(path, project) is not { } full || !ProjectPaths.IsInside(full, project));
    }

    private static bool NamesAFolderElsewhere(string command) =>
        Variable().Matches(command).Any(found =>
        {
            string name = found.Groups["name"].Value;
            return OutsideVariables.Contains(name) || (found.Groups["separator"].Success && !HereVariables.Contains(name));
        });

    /// <summary>The word as a path when it is one that may lead elsewhere, or null for a plain relative word.</summary>
    private static string? AsPath(string word)
    {
        if (GitBashDrive().Match(word) is { Success: true } drive)
        {
            // /c/Users/… in Git Bash is C:\Users\….
            return $"{drive.Groups["drive"].Value}:\\{drive.Groups["rest"].Value}";
        }

        if (DriveRooted().IsMatch(word) || word.StartsWith(@"\\", StringComparison.Ordinal) || UpFolder().IsMatch(word))
        {
            return word;
        }

        // /etc/x or \Windows\x: the root of a drive. A single /x is a switch (del /s, dir /b), and /dev/null is nowhere.
        if (SlashRooted().IsMatch(word) && !word.Equals("/dev/null", StringComparison.Ordinal))
        {
            return word;
        }

        return null;
    }

    internal static string? Text(JsonElement? input, string field) =>
        input is { ValueKind: JsonValueKind.Object } value
        && value.TryGetProperty(field, out JsonElement found)
        && found.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(found.GetString())
            ? found.GetString()
            : null;

    /// <summary>The words of a command: a quoted text whole, otherwise what lies between spaces and shell signs.</summary>
    [GeneratedRegex("\"[^\"]*\"|'[^']*'|[^\\s\"'|;&<>(),=]+")]
    private static partial Regex Words();

    [GeneratedRegex(@"(?:^|[\s""'=\\/])\.git(?:[\\/""'\s]|$)", RegexOptions.IgnoreCase)]
    private static partial Regex GitFolderWord();

    [GeneratedRegex(@"(?:%(?<name>[A-Za-z_][\w()]*)%|\$env:(?<name>[A-Za-z_][\w()]*)|\$\{?(?<name>[A-Za-z_]\w*)\}?)(?<separator>[\\/])?")]
    private static partial Regex Variable();

    [GeneratedRegex(@"^/(?<drive>[A-Za-z])/(?<rest>.*)$")]
    private static partial Regex GitBashDrive();

    [GeneratedRegex(@"^[A-Za-z]:(?:[\\/]|$)")]
    private static partial Regex DriveRooted();

    [GeneratedRegex(@"^[\\/][^\\/]+[\\/]")]
    private static partial Regex SlashRooted();

    [GeneratedRegex(@"(?:^|[\\/])\.\.(?:[\\/]|$)")]
    private static partial Regex UpFolder();
}
