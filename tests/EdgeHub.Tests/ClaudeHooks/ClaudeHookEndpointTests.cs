using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EdgeHub.Security;
using EdgeHub.Streaming;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace EdgeHub.Tests.ClaudeHooks;

/// <summary>
/// The hooks as Claude Code calls them (<c>docs/formats/claude-code-hooks.md</c>), and the tablet's answers. The tablet
/// is another device of the home network; the hooks come from the PC. Every value is invented.
/// </summary>
public sealed class ClaudeHookEndpointTests(ClaudeHookEndpointTests.HookFactory factory) : IClassFixture<ClaudeHookEndpointTests.HookFactory>
{
    public const string HookToken = "hook-token-for-the-tests-0123456789-abcdefgh";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public class HookFactory : EdgeHubFactory
    {
        protected virtual int TimeoutSeconds => 30;

        protected override IReadOnlyDictionary<string, string?> ExtraSettings => new Dictionary<string, string?>
        {
            ["ClaudeCode:HookTokenHash"] = AccessToken.HashOf(HookToken),
            ["ClaudeCode:PermissionTimeoutSeconds"] = TimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        };
    }

    public sealed class QuickFactory : HookFactory
    {
        protected override int TimeoutSeconds => 1;
    }

    private static object Permission(string command, string folder = "bookshelf", string session = "s-permission") => new
    {
        hook_event_name = "PermissionRequest",
        session_id = session,
        transcript_path = $@"C:\Users\demo\.claude\projects\C--Users-demo-repo-{folder}\{session}.jsonl",
        cwd = $@"C:\Users\demo\repo\{folder}",
        permission_mode = "default",
        tool_name = "PowerShell",
        tool_input = new { command, description = "Invented" },
        permission_suggestions = new[] { new { type = "setMode", mode = "acceptEdits", destination = "session" } },
    };

    private static object Notification(string type, string folder, string session = "s-notification") => new
    {
        hook_event_name = "Notification",
        session_id = session,
        cwd = $@"C:\Users\demo\repo\{folder}",
        notification_type = type,
        message = "Claude is waiting for your input",
    };

    private static HttpRequestMessage Hook(string path, object body, string? token = HookToken, string? from = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (from is not null)
        {
            request.Headers.Add(TestRemoteAddress.Header, from);
        }

        return request;
    }

    private static HttpRequestMessage FromTheTablet(HttpMethod method, string path, string? from = TestRemoteAddress.Tablet)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", $"{AccessTokenMiddleware.CookieName}={EdgeHubFactory.Token}");
        request.Headers.Add("Sec-Fetch-Site", "same-origin");
        if (from is not null)
        {
            request.Headers.Add(TestRemoteAddress.Header, from);
        }

        return request;
    }

    private async Task<HttpStatusCode> AnswerAsync(string id, string answer, string? from = TestRemoteAddress.Tablet)
    {
        using HttpRequestMessage request = FromTheTablet(HttpMethod.Post, $"/api/claude/pending/{id}/{answer}", from);
        using HttpResponseMessage response = await factory.CreateClient().SendAsync(request, Ct);
        return response.StatusCode;
    }

    private static async Task<JsonElement> DeskAsync(EdgeHubFactory server)
    {
        using HttpRequestMessage request = FromTheTablet(HttpMethod.Get, "/api/claude/pending");
        using HttpResponseMessage response = await server.CreateClient().SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.Clone();
    }

    /// <summary>The card of this command once it waits on the tablet.</summary>
    private static async Task<JsonElement> CardAsync(EdgeHubFactory server, string summary)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            foreach (JsonElement card in (await DeskAsync(server)).GetProperty("pending").EnumerateArray())
            {
                if (card.GetProperty("summary").GetString() == summary)
                {
                    return card;
                }
            }

            await Task.Delay(20, timeout.Token);
        }
    }

    private static async Task<bool> HasCardAsync(EdgeHubFactory server, string summary) =>
        (await DeskAsync(server)).GetProperty("pending").EnumerateArray().Any(c => c.GetProperty("summary").GetString() == summary);

    private static async Task<string> EmptyAnswerAsync(Task<HttpResponseMessage> hook)
    {
        using HttpResponseMessage response = await hook;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync(Ct);
    }

    [Fact]
    public async Task The_hooks_are_off_without_their_token_hash()
    {
        await using var server = new EdgeHubFactory();
        foreach (string path in new[] { "/hooks/claude/permission", "/hooks/claude/notification", "/hooks/claude/stop" })
        {
            using HttpResponseMessage response = await server.CreateClient().SendAsync(Hook(path, Permission("mkdir exports")), Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-the-hook-token")]
    [InlineData(EdgeHubFactory.Token)]
    public async Task A_hook_without_its_token_is_refused(string? token)
    {
        using HttpResponseMessage response = await factory.CreateClient().SendAsync(Hook("/hooks/claude/stop", new { session_id = "s" }, token), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_hook_from_another_device_is_not_found_even_with_its_token()
    {
        using HttpResponseMessage response = await factory.CreateClient().SendAsync(Hook("/hooks/claude/stop", new { session_id = "s" }, from: TestRemoteAddress.Tablet), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unreadable_hook_is_a_bad_request()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hooks/claude/permission") { Content = new StringContent("{not json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", HookToken);

        using HttpResponseMessage response = await factory.CreateClient().SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Without_a_screen_on_another_device_claude_code_asks_on_the_pc_at_once()
    {
        await using var server = new HookFactory();
        await using Screen onThePc = await Screen.OpenAsync(server, from: null);

        // At once: well before the 30 seconds a card would wait.
        string body = await EmptyAnswerAsync(server.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir alone")), Ct).WaitAsync(TimeSpan.FromSeconds(5), Ct));

        Assert.Equal("", body);
        Assert.False(await HasCardAsync(server, "mkdir alone"));
    }

    [Fact]
    public async Task The_tablet_allows_a_command_and_claude_code_goes_on()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("git push origin main")), Ct);

        JsonElement card = await CardAsync(factory, "git push origin main");
        Assert.Equal(("bookshelf", "bookshelf", "PowerShell", "Command", "Invented", false), (
            card.GetProperty("session").GetString(),
            card.GetProperty("folder").GetString(),
            card.GetProperty("tool").GetString(),
            card.GetProperty("kind").GetString(),
            card.GetProperty("description").GetString(),
            card.GetProperty("sensitive").GetBoolean()));
        Assert.Equal(HttpStatusCode.NoContent, await AnswerAsync(card.GetProperty("id").GetString()!, "allow"));

        using HttpResponseMessage response = await hook;
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        JsonElement output = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("hookSpecificOutput");
        Assert.Equal("PermissionRequest", output.GetProperty("hookEventName").GetString());
        Assert.Equal("allow", output.GetProperty("decision").GetProperty("behavior").GetString());
        Assert.False(output.GetProperty("decision").TryGetProperty("message", out _));
        Assert.False(await HasCardAsync(factory, "git push origin main"));
    }

    [Fact]
    public async Task The_tablet_refuses_a_command()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir refused")), Ct);

        Assert.Equal(HttpStatusCode.NoContent, await AnswerAsync((await CardAsync(factory, "mkdir refused")).GetProperty("id").GetString()!, "deny"));

        using HttpResponseMessage response = await hook;
        JsonElement decision = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("hookSpecificOutput").GetProperty("decision");
        Assert.Equal(("deny", "Refusé depuis la tablette"), (decision.GetProperty("behavior").GetString(), decision.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Allow_and_deny_are_refused_from_the_pc_which_can_only_hand_the_question_back()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir from-pc")), Ct);
        string id = (await CardAsync(factory, "mkdir from-pc")).GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.Forbidden, await AnswerAsync(id, "allow", from: null));
        Assert.Equal(HttpStatusCode.Forbidden, await AnswerAsync(id, "deny", from: "127.0.0.1"));
        Assert.Equal(HttpStatusCode.Forbidden, await AnswerAsync(id, "allow", from: "::1"));
        Assert.True(await HasCardAsync(factory, "mkdir from-pc"));

        Assert.Equal(HttpStatusCode.NoContent, await AnswerAsync(id, "pc", from: null));
        Assert.Equal("", await EmptyAnswerAsync(hook));
    }

    [Fact]
    public async Task A_sensitive_command_cannot_be_allowed_from_the_tablet()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("git push --force")), Ct);
        JsonElement card = await CardAsync(factory, "git push --force");
        Assert.Equal((true, "push forcé"), (card.GetProperty("sensitive").GetBoolean(), card.GetProperty("reason").GetString()));
        string id = card.GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.Conflict, await AnswerAsync(id, "allow"));
        Assert.Equal(HttpStatusCode.NoContent, await AnswerAsync(id, "pc"));

        Assert.Equal("", await EmptyAnswerAsync(hook));
    }

    [Theory]
    [InlineData("no-such-card", "allow")]
    [InlineData("no-such-card", "pc")]
    [InlineData("no-such-card", "maybe")]
    public async Task An_unknown_card_or_answer_is_not_found(string id, string answer)
    {
        Assert.Equal(HttpStatusCode.NotFound, await AnswerAsync(id, answer));
    }

    [Fact]
    public async Task The_waiting_requests_need_the_tablets_token()
    {
        using HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/claude/pending", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_card_leaves_the_tablet_when_claude_code_stops_waiting()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        using var stopsWaiting = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir answered-on-pc")), stopsWaiting.Token);
        await CardAsync(factory, "mkdir answered-on-pc");

        await stopsWaiting.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => hook);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (await HasCardAsync(factory, "mkdir answered-on-pc"))
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    [Fact]
    public async Task Without_an_answer_in_time_claude_code_asks_on_the_pc()
    {
        await using var server = new QuickFactory();
        await using Screen tablet = await Screen.OpenAsync(server);

        Task<HttpResponseMessage> hook = server.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir too-late")), Ct);
        await CardAsync(server, "mkdir too-late");

        Assert.Equal("", await EmptyAnswerAsync(hook));
        Assert.False(await HasCardAsync(server, "mkdir too-late"));
    }

    [Fact]
    public async Task The_stream_carries_the_waiting_cards()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir streamed")), Ct);

        JsonElement card;
        do
        {
            card = (await tablet.NextAsync()).GetProperty("claude").GetProperty("pending").EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("summary").GetString() == "mkdir streamed");
        }
        while (card.ValueKind == JsonValueKind.Undefined);

        Assert.Equal(HttpStatusCode.NoContent, await AnswerAsync(card.GetProperty("id").GetString()!, "pc"));
        await EmptyAnswerAsync(hook);
    }

    [Fact]
    public async Task Notifications_and_stops_become_notices_and_never_get_an_answer()
    {
        string notification = await EmptyAnswerAsync(factory.CreateClient().SendAsync(Hook("/hooks/claude/notification", Notification("idle_prompt", "notices-demo")), Ct));
        string stop = await EmptyAnswerAsync(factory.CreateClient().SendAsync(
            Hook("/hooks/claude/stop", new
            {
                hook_event_name = "Stop",
                session_id = "s-notification",
                cwd = @"C:\Users\demo\repo\notices-demo",
                stop_hook_active = false,
                last_assistant_message = "Invented last answer.",
            }),
            Ct));

        Assert.Equal(("", ""), (notification, stop));
        Assert.Equal(
            ["NeedsYou", "Done"],
            (await DeskAsync(factory)).GetProperty("notices").EnumerateArray()
                .Where(n => n.GetProperty("session").GetString() == "notices-demo")
                .Select(n => n.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task A_notification_of_an_unknown_type_is_ignored()
    {
        await EmptyAnswerAsync(factory.CreateClient().SendAsync(Hook("/hooks/claude/notification", Notification("auth_success", "unknown-demo")), Ct));

        Assert.DoesNotContain((await DeskAsync(factory)).GetProperty("notices").EnumerateArray(), n => n.GetProperty("session").GetString() == "unknown-demo");
    }

    [Fact]
    public async Task The_permission_prompt_is_not_a_notice_while_its_request_waits_on_the_tablet()
    {
        await using Screen tablet = await Screen.OpenAsync(factory);
        Task<HttpResponseMessage> hook = factory.CreateClient().SendAsync(Hook("/hooks/claude/permission", Permission("mkdir prompted", folder: "prompt-demo", session: "s-prompt")), Ct);
        string id = (await CardAsync(factory, "mkdir prompted")).GetProperty("id").GetString()!;

        await EmptyAnswerAsync(factory.CreateClient().SendAsync(Hook("/hooks/claude/notification", Notification("permission_prompt", "prompt-demo", session: "s-prompt")), Ct));
        Assert.DoesNotContain((await DeskAsync(factory)).GetProperty("notices").EnumerateArray(), n => n.GetProperty("session").GetString() == "prompt-demo");

        Assert.Equal(HttpStatusCode.NoContent, await AnswerAsync(id, "pc"));
        await EmptyAnswerAsync(hook);
        await EmptyAnswerAsync(factory.CreateClient().SendAsync(Hook("/hooks/claude/notification", Notification("permission_prompt", "prompt-demo", session: "s-prompt")), Ct));
        Assert.Contains((await DeskAsync(factory)).GetProperty("notices").EnumerateArray(), n => n.GetProperty("session").GetString() == "prompt-demo");
    }

    /// <summary>A screen connected to <c>/api/stream</c>, on the tablet unless said otherwise.</summary>
    private sealed class Screen : IAsyncDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly StreamReader _reader;
        private readonly CancellationTokenSource _timeout;

        private Screen(HttpResponseMessage response, StreamReader reader, CancellationTokenSource timeout)
        {
            _response = response;
            _reader = reader;
            _timeout = timeout;
        }

        public static async Task<Screen> OpenAsync(EdgeHubFactory server, string? from = TestRemoteAddress.Tablet)
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            HttpRequestMessage request = FromTheTablet(HttpMethod.Get, "/api/stream", from);
            HttpResponseMessage response = await server.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var screen = new Screen(response, new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token)), timeout);

            // Subscribed once the first state arrives.
            await screen.NextAsync();
            LiveStateHub hub = server.Services.GetRequiredService<LiveStateHub>();
            Assert.True(from is null ? hub.SubscriberCount > 0 : hub.ScreensElsewhere > 0);
            return screen;
        }

        public async Task<JsonElement> NextAsync()
        {
            while (await _reader.ReadLineAsync(_timeout.Token) is { } line)
            {
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                {
                    return JsonDocument.Parse(line["data: ".Length..]).RootElement.Clone();
                }
            }

            throw new InvalidOperationException("The stream ended.");
        }

        public ValueTask DisposeAsync()
        {
            _reader.Dispose();
            _response.Dispose();
            _timeout.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
