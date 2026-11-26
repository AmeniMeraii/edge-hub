using EdgeHub.ClaudeHooks;
using EdgeHub.Security;

namespace EdgeHub.Tests.ClaudeHooks;

public sealed class HookTokenCommandTests
{
    private const string Token = "a-long-enough-hook-token-for-the-tests-0123";

    [Fact]
    public void Gives_the_hash_to_the_pc_and_the_token_to_the_hooks()
    {
        string text = HookTokenCommand.Text(Token);

        Assert.Contains($"dotnet user-secrets set ClaudeCode:HookTokenHash {AccessToken.HashOf(Token)} --project src/EdgeHub", text, StringComparison.Ordinal);
        Assert.Contains($"\"ClaudeCode\": {{ \"HookTokenHash\": \"{AccessToken.HashOf(Token)}\" }}", text, StringComparison.Ordinal);
        Assert.Contains($"\"Authorization\": \"Bearer {Token}\"", text, StringComparison.Ordinal);
        Assert.Single(text.Split(Token).Skip(1));
    }
}
