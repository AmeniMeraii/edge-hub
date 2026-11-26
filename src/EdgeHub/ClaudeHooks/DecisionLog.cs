using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace EdgeHub.ClaudeHooks;

/// <summary>
/// <c>decisions.log</c>: one line for each decision taken on the tablet (PLAN 4.7), with the time, the session, the
/// folder, the tool and the command or file. A request the tablet did not decide leaves no line. No folder: no log.
/// </summary>
public sealed partial class DecisionLog(string? folder, TimeProvider time, ILogger? logger = null)
{
    public const string FileName = "decisions.log";

    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger _logger = logger ?? NullLogger.Instance;
    private readonly Lock _gate = new();

    public void Record(PermissionCard card, Verdict verdict)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        string line = string.Create(
            CultureInfo.InvariantCulture,
            $"{time.GetLocalNow():yyyy-MM-dd HH:mm:ss zzz} · {Said(verdict)} · session {OneLine(card.Session)} · dossier {OneLine(card.Folder ?? "?")} · {OneLine(card.Tool)} · {OneLine(card.Summary)}\n");
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, FileName), line, Utf8);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The decision still goes to Claude Code. The line is not logged: it holds the command.
            LogNotWritten(_logger, e.GetType().Name);
        }
    }

    private static string Said(Verdict verdict) => verdict switch
    {
        Verdict.Allow => "autorisé depuis la tablette",
        Verdict.Deny => "refusé depuis la tablette",
        _ => "renvoyé au PC depuis la tablette",
    };

    /// <summary>A command of several lines stays on one line of the log.</summary>
    private static string OneLine(string text) => text.ReplaceLineEndings(" ⏎ ").Replace('\t', ' ');

    [LoggerMessage(Level = LogLevel.Warning, Message = "Écriture impossible dans decisions.log ({Error})")]
    private static partial void LogNotWritten(ILogger logger, string error);
}
