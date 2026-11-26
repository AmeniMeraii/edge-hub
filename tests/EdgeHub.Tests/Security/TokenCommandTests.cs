using EdgeHub.Security;

namespace EdgeHub.Tests.Security;

public sealed class TokenCommandTests
{
    private const string Token = "a-long-enough-token-for-the-tests-0123456789";

    [Fact]
    public void Shows_the_token_once_for_the_tablet_and_its_hash_for_the_pc()
    {
        string text = TokenCommand.Text(Token);

        Assert.Contains($"dotnet user-secrets set Security:TokenHash {AccessToken.HashOf(Token)} --project src/EdgeHub", text, StringComparison.Ordinal);
        Assert.Contains($"\"Security\": {{ \"TokenHash\": \"{AccessToken.HashOf(Token)}\" }}", text, StringComparison.Ordinal);
        Assert.Contains($"/?k={Token}", text, StringComparison.Ordinal);
        Assert.Single(text.Split(Token).Skip(1));
    }
}
