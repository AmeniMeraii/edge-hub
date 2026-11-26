using System.Text.Json;
using System.Text.Json.Serialization;
using EdgeHub.ClaudeSessions;
using EdgeHub.Security;
using EdgeHub.Streaming;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EdgeHub.ClaudeHooks;

/// <summary>
/// The routes Claude Code's hooks call (PLAN 4.7), and the tablet's answers. A hook is taken from the PC only, with its
/// own token. An answer comes from the screen with the tablet's cookie, and Allow and Refuse never from the PC. Whatever
/// goes wrong, Claude Code gets no decision and asks on the PC as usual: the service never decides on its own.
/// </summary>
public static partial class ClaudeHookEndpoints
{
    public const string RefusedMessage = "Refusé depuis la tablette";

    /// <summary>A Write request carries the whole file: 4 MB is plenty, and beyond it Claude Code simply asks on the PC.</summary>
    private const long MaxHookBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions AnswerJson = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapClaudeHooks(this IEndpointRouteBuilder app)
    {
        var limit = new RequestSizeLimitAttribute(MaxHookBytes);
        app.MapPost("/hooks/claude/permission", PermissionAsync).WithMetadata(limit);
        app.MapPost("/hooks/claude/notification", NotificationAsync).WithMetadata(limit);
        app.MapPost("/hooks/claude/stop", StopAsync).WithMetadata(limit);
        app.MapGet("/api/claude/pending", ([FromServices] ClaudeDesk desk) => desk.Snapshot());
        app.MapPost("/api/claude/pending/{id}/{answer}", Answer);
        return app;
    }

    /// <summary>« No decision »: an empty 200, after which Claude Code asks on the PC.</summary>
    private static IResult NoDecision => TypedResults.Ok();

    private static async Task<IResult> PermissionAsync(
        HttpContext context,
        [FromServices] IOptionsMonitor<ClaudeCodeOptions> options,
        [FromServices] ClaudeDesk desk,
        [FromServices] LiveStateHub hub,
        [FromServices] IClaudeSessionReader sessions,
        [FromServices] ILoggerFactory loggers)
    {
        ClaudeCodeOptions settings = options.CurrentValue;
        if (Refusal(context, settings) is { } refusal)
        {
            return refusal;
        }

        (JsonDocument? body, IResult? unreadable) = await ReadAsync(context.Request).ConfigureAwait(false);
        using (body)
        {
            if (body is null)
            {
                return unreadable!;
            }

            HookInput input = HookInput.From(body.RootElement);
            ILogger logger = loggers.CreateLogger(typeof(ClaudeHookEndpoints));
            if (hub.ScreensElsewhere == 0)
            {
                LogPermission(logger, input.ToolName ?? "?", "aucun écran connecté, question posée sur le PC");
                return NoDecision;
            }

            PermissionRequestInfo request = input.ToPermission(sessions.NameOf(input.TranscriptPath, input.Cwd), settings.Rules());
            Verdict? verdict = await desk.AskAsync(request, TimeSpan.FromSeconds(settings.PermissionTimeoutSeconds), context.RequestAborted).ConfigureAwait(false);
            LogPermission(logger, request.Tool, verdict switch
            {
                Verdict.Allow => "autorisée depuis la tablette",
                Verdict.Deny => "refusée depuis la tablette",
                Verdict.OnPc => "renvoyée au PC depuis la tablette",
                _ when context.RequestAborted.IsCancellationRequested => "Claude Code n'attend plus",
                _ => "pas de réponse à temps, question posée sur le PC",
            });

            return verdict switch
            {
                Verdict.Allow => Decision("allow"),
                Verdict.Deny => Decision("deny", RefusedMessage),
                _ => NoDecision,
            };
        }
    }

    private static async Task<IResult> NotificationAsync(
        HttpContext context,
        [FromServices] IOptionsMonitor<ClaudeCodeOptions> options,
        [FromServices] ClaudeDesk desk,
        [FromServices] IClaudeSessionReader sessions)
    {
        if (Refusal(context, options.CurrentValue) is { } refusal)
        {
            return refusal;
        }

        (JsonDocument? body, IResult? unreadable) = await ReadAsync(context.Request).ConfigureAwait(false);
        using (body)
        {
            if (body is null)
            {
                return unreadable!;
            }

            HookInput input = HookInput.From(body.RootElement);
            NoticeKind? kind = input.NotificationType switch
            {
                // Sent a few seconds after each request: the card already says it.
                "permission_prompt" when desk.IsAsking(input.SessionId) => null,
                "permission_prompt" or "idle_prompt" or "agent_needs_input" or "elicitation_dialog" => NoticeKind.NeedsYou,
                "agent_completed" => NoticeKind.Done,
                _ => null,
            };
            if (kind is { } notice)
            {
                desk.Notify(notice, sessions.NameOf(input.TranscriptPath, input.Cwd));
            }

            return NoDecision;
        }
    }

    /// <summary>The end of a turn. Always an empty answer: any other could keep Claude Code going.</summary>
    private static async Task<IResult> StopAsync(
        HttpContext context,
        [FromServices] IOptionsMonitor<ClaudeCodeOptions> options,
        [FromServices] ClaudeDesk desk,
        [FromServices] IClaudeSessionReader sessions)
    {
        if (Refusal(context, options.CurrentValue) is { } refusal)
        {
            return refusal;
        }

        (JsonDocument? body, IResult? unreadable) = await ReadAsync(context.Request).ConfigureAwait(false);
        using (body)
        {
            if (body is null)
            {
                return unreadable!;
            }

            // last_assistant_message is never read.
            HookInput input = HookInput.From(body.RootElement);
            desk.Notify(NoticeKind.Done, sessions.NameOf(input.TranscriptPath, input.Cwd));
            return NoDecision;
        }
    }

    private static IResult Answer(string id, string answer, HttpContext context, [FromServices] ClaudeDesk desk)
    {
        Verdict? verdict = answer switch
        {
            "allow" => Verdict.Allow,
            "deny" => Verdict.Deny,
            "pc" => Verdict.OnPc,
            _ => null,
        };
        if (verdict is not { } chosen)
        {
            return TypedResults.NotFound();
        }

        // Anything running on the PC could press Allow: only another device may decide.
        if (chosen != Verdict.OnPc && ThisPc.IsTheCaller(context.Connection))
        {
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);
        }

        return desk.Answer(id, chosen) switch
        {
            AnswerResult.Done => TypedResults.NoContent(),
            AnswerResult.NotAllowed => TypedResults.Conflict(),
            _ => TypedResults.NotFound(),
        };
    }

    /// <summary>404 when the hooks are off or the call comes from another device, 401 without the hooks' token.</summary>
    private static IResult? Refusal(HttpContext context, ClaudeCodeOptions settings)
    {
        if (string.IsNullOrEmpty(settings.HookTokenHash) || !ThisPc.IsTheCaller(context.Connection))
        {
            return TypedResults.NotFound();
        }

        const string Scheme = "Bearer ";
        string header = context.Request.Headers.Authorization.ToString();
        string? presented = header.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ? header[Scheme.Length..].Trim() : null;
        return AccessToken.Matches(presented, settings.HookTokenHash) ? null : TypedResults.Unauthorized();
    }

    private static async Task<(JsonDocument? Body, IResult? Unreadable)> ReadAsync(HttpRequest request)
    {
        try
        {
            return (await JsonDocument.ParseAsync(request.Body, default, request.HttpContext.RequestAborted).ConfigureAwait(false), null);
        }
        catch (JsonException)
        {
            return (null, TypedResults.BadRequest());
        }
        catch (BadHttpRequestException e)
        {
            // Larger than allowed (413), or cut off.
            return (null, TypedResults.StatusCode(e.StatusCode));
        }
    }

    private static JsonHttpResult<HookAnswer> Decision(string behavior, string? message = null) =>
        TypedResults.Json(new HookAnswer(new HookOutput("PermissionRequest", new HookDecision(behavior, message))), AnswerJson);

    // Never the command: the log file is not the place for it.
    [LoggerMessage(Level = LogLevel.Information, Message = "Demande d'autorisation de Claude Code ({Tool}) : {Outcome}")]
    private static partial void LogPermission(ILogger logger, string tool, string outcome);

    private sealed record HookAnswer(HookOutput HookSpecificOutput);

    private sealed record HookOutput(string HookEventName, HookDecision Decision);

    private sealed record HookDecision(
        string Behavior,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message);
}
