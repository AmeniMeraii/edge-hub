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
    private static void ValidateAtStartup(string? tokenHash, string? clearToken = null)
    {
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:TokenHash"] = tokenHash, ["Security:Token"] = clearToken })
            .Build();
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(settings)
            .AddEdgeHubOptions()
            .BuildServiceProvider();

        services.GetRequiredService<IStartupValidator>().Validate();
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
