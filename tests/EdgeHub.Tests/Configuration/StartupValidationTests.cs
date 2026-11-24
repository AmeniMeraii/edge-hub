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
    private static void ValidateAtStartup(string? token)
    {
        IConfiguration settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:Token"] = token })
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
    [InlineData("too-short-token")]
    [InlineData("a+token+that+breaks+in+an+address+0123456789")]
    public void The_service_refuses_to_start_without_a_valid_token(string? token)
    {
        var failure = Assert.Throws<OptionsValidationException>(() => ValidateAtStartup(token));

        Assert.Contains("Security:Token", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_long_url_safe_token_is_accepted()
    {
        ValidateAtStartup(EdgeHubFactory.Token);
    }
}
