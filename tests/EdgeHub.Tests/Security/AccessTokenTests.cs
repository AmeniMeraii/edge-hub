using EdgeHub.Security;

namespace EdgeHub.Tests.Security;

public sealed class AccessTokenTests
{
    private const string Expected = "a-long-enough-token-for-the-tests-0123456789";

    [Fact]
    public void Accepts_the_exact_token()
    {
        Assert.True(AccessToken.Matches(Expected, Expected));
    }

    [Theory]
    [InlineData("a-long-enough-token-for-the-tests-0123456780")]
    [InlineData("a-long-enough-token-for-the-tests")]
    [InlineData("a-long-enough-token-for-the-tests-0123456789-and-more")]
    [InlineData("A-LONG-ENOUGH-TOKEN-FOR-THE-TESTS-0123456789")]
    [InlineData("")]
    [InlineData(null)]
    public void Refuses_anything_else(string? candidate)
    {
        Assert.False(AccessToken.Matches(candidate, Expected));
    }
}
