using EdgeHub.Tests.TestSupport;

namespace EdgeHub.Tests.Configuration;

public sealed class StartupValidationTests
{
    private sealed class FactoryWithToken(string? token) : EdgeHubFactory
    {
        protected override string? ConfiguredToken => token;
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-token")]
    public void The_service_refuses_to_start_without_a_long_enough_token(string token)
    {
        using var factory = new FactoryWithToken(token);

        Exception? failure = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(failure);
        Assert.Contains("Security:Token", failure.ToString(), StringComparison.Ordinal);
    }
}
