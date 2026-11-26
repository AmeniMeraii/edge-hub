using EdgeHub.ClaudeSessions;
using EdgeHub.Configuration;
using EdgeHub.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EdgeHub.Tests.Configuration;

/// <summary>
/// Runs the same registration as the service and the check it performs at startup, without starting a host (a host
/// that fails to start can surface a disposal error instead of the validation error).
/// </summary>
public sealed class StartupValidationTests
{
    private static readonly string TabletHash = EdgeHub.Security.AccessToken.HashOf(EdgeHubFactory.Token);

    private static void ValidateAtStartup(string? tokenHash, string? clearToken = null, Dictionary<string, string?>? more = null) =>
        ValidatedClaudeCode(new Dictionary<string, string?>(more ?? []) { ["Security:TokenHash"] = tokenHash, ["Security:Token"] = clearToken });

    private static ClaudeCodeOptions ValidatedClaudeCode(Dictionary<string, string?> values)
    {
        values.TryAdd("Security:TokenHash", TabletHash);
        IConfiguration settings = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(settings)
            .AddEdgeHubOptions()
            .BuildServiceProvider();

        services.GetRequiredService<IStartupValidator>().Validate();
        return services.GetRequiredService<IOptions<ClaudeCodeOptions>>().Value;
    }

    [Theory]
    [InlineData("0123456789abcdef")]
    [InlineData("a-hook-token-in-clear-instead-of-its-hash-0123456789-0123456789-0")]
    public void A_hooks_token_must_be_given_by_its_hash(string hash)
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidatedClaudeCode(new() { ["ClaudeCode:HookTokenHash"] = hash }));

        Assert.Contains("ClaudeCode:HookTokenHash", failure.Message, StringComparison.Ordinal);
        Assert.Contains("--new-hook-token", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hooks_cannot_use_the_tablets_token()
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidatedClaudeCode(new() { ["ClaudeCode:HookTokenHash"] = TabletHash.ToUpperInvariant() }));

        Assert.Contains("--new-hook-token", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("601")]
    public void The_wait_for_the_tablet_stays_between_one_second_and_ten_minutes(string seconds)
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidatedClaudeCode(new() { ["ClaudeCode:PermissionTimeoutSeconds"] = seconds }));

        Assert.Contains("ClaudeCode:PermissionTimeoutSeconds", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("(unclosed", "parenthèse")]
    [InlineData("", "vide")]
    [InlineData(@"\bdeploy\b", "")]
    public void A_sensitive_command_needs_a_valid_pattern_and_a_reason(string pattern, string reason)
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidatedClaudeCode(new()
        {
            ["ClaudeCode:SensitiveCommands:0:Pattern"] = pattern,
            ["ClaudeCode:SensitiveCommands:0:Reason"] = reason,
        }));

        Assert.Contains("ClaudeCode:SensitiveCommands:0", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hooks_settings_are_read_and_the_hooks_are_off_by_default()
    {
        ClaudeCodeOptions defaults = ValidatedClaudeCode([]);
        Assert.Equal(("", 60, (List<EdgeHub.ClaudeHooks.SensitiveRule>?)null), (defaults.HookTokenHash, defaults.PermissionTimeoutSeconds, defaults.SensitiveCommands));
        Assert.EndsWith(@"\EdgeHub", defaults.ResolvedDecisionsFolder(), StringComparison.OrdinalIgnoreCase);

        string hookHash = EdgeHub.Security.AccessToken.HashOf("another-token-only-for-the-hooks-0123456789");
        ClaudeCodeOptions mine = ValidatedClaudeCode(new()
        {
            ["ClaudeCode:HookTokenHash"] = hookHash,
            ["ClaudeCode:PermissionTimeoutSeconds"] = "45",
            ["ClaudeCode:SensitiveCommands:0:Pattern"] = @"\bdeploy\b",
            ["ClaudeCode:SensitiveCommands:0:Reason"] = "déploiement",
            ["ClaudeCode:DecisionsFolder"] = "",
        });
        Assert.Equal((hookHash, 45, (string?)null), (mine.HookTokenHash, mine.PermissionTimeoutSeconds, mine.ResolvedDecisionsFolder()));
        Assert.Equal([new EdgeHub.ClaudeHooks.SensitiveRule(@"\bdeploy\b", "déploiement")], mine.SensitiveCommands);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789abcdef")]
    [InlineData("a-token-in-clear-instead-of-its-hash-0123456789-0123456789-01234")]
    public void The_service_refuses_to_start_without_the_hash_of_a_token(string? tokenHash)
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidateAtStartup(tokenHash));

        Assert.Contains("Security:TokenHash", failure.Message, StringComparison.Ordinal);
        Assert.Contains("--new-token", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hash_of_a_token_is_accepted()
    {
        ValidateAtStartup(EdgeHub.Security.AccessToken.HashOf(EdgeHubFactory.Token));
    }

    [Fact]
    public void A_token_kept_in_clear_is_refused_with_how_to_replace_it()
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidateAtStartup(EdgeHub.Security.AccessToken.HashOf(EdgeHubFactory.Token), EdgeHubFactory.Token));

        Assert.Contains("Security:Token n'est plus accepté", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(EdgeHubFactory.Token, failure.Message, StringComparison.Ordinal);
    }
}
