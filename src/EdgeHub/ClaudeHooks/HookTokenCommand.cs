using EdgeHub.Security;

namespace EdgeHub.ClaudeHooks;

/// <summary>
/// <c>EdgeHub.exe --new-hook-token</c>: makes the token Claude Code's hooks send, and says where its hash and the token
/// go. This token only lets the PC's own Claude Code post requests and notices: it never opens the screen.
/// </summary>
public static class HookTokenCommand
{
    public const string Argument = "--new-hook-token";

    public static string Text(string token) =>
        $"""
        Nouveau jeton pour les hooks de Claude Code. Le service n'en garde que l'empreinte ; le jeton va dans la
        configuration des hooks, que Claude Code lit sur ce PC.

        1. Enregistre l'empreinte (elle remplace la précédente) :
             dotnet user-secrets set ClaudeCode:HookTokenHash {AccessToken.HashOf(token)} --project src/EdgeHub
           ou, pour le service installé, dans appsettings.Local.json à côté de EdgeHub.exe :
             "ClaudeCode": {"{"} "HookTokenHash": "{AccessToken.HashOf(token)}" {"}"}
           puis redémarre le service.

        2. Dans la configuration des hooks (modèle : integrations/claude-code/hooks/settings.example.json), donne aux
           trois hooks cet en-tête :
             "headers": {"{"} "Authorization": "Bearer {token}" {"}"}

        Ce jeton ne sert qu'aux hooks, depuis ce PC : il ne donne pas accès à l'écran de la tablette.

        """;
}
