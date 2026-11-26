using EdgeHub.Security;

namespace EdgeHub.Tests.Security;

public sealed class AccessTokenTests
{
    private const string Token = "a-long-enough-token-for-the-tests-0123456789";
    private static readonly string Hash = AccessToken.HashOf(Token);

    [Fact]
    public void Accepts_the_token_whose_hash_is_kept()
    {
        Assert.True(AccessToken.Matches(Token, Hash));
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
        Assert.False(AccessToken.Matches(candidate, Hash));
    }

    [Fact]
    public void Refuses_the_hash_itself()
    {
        // Someone who reads the PC's configuration gets the hash, not the token.
        Assert.False(AccessToken.Matches(Hash, Hash));
    }

    [Fact]
    public void Keeps_a_sha256_hash_in_lower_case_hexadecimal()
    {
        Assert.Matches("^[0-9a-f]{64}$", Hash);
        Assert.Equal(Hash, AccessToken.HashOf(Token));
    }

    [Fact]
    public void Creates_long_random_tokens_that_fit_in_an_address()
    {
        string first = AccessToken.Create(), second = AccessToken.Create();

        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
        Assert.NotEqual(first, second);
    }
}
