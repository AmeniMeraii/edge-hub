namespace EdgeHub.Security;

/// <summary>
/// <c>EdgeHub.exe --new-token</c>: makes a tablet token and says where its hash goes. The token is shown once, in the
/// terminal of the person who runs the command; the PC keeps only the hash.
/// </summary>
public static class TokenCommand
{
    public const string Argument = "--new-token";

    public static string Text(string token) =>
        $"""
        Nouveau jeton pour la tablette. Il n'est affiché qu'une fois : le PC n'en garde que l'empreinte.

        1. Sur le PC, enregistre l'empreinte (elle remplace la précédente) :
             dotnet user-secrets set Security:TokenHash {AccessToken.HashOf(token)} --project src/EdgeHub
           ou, pour le service installé, dans appsettings.Local.json à côté de EdgeHub.exe :
             "Security": {"{"} "TokenHash": "{AccessToken.HashOf(token)}" {"}"}
           puis redémarre le service.

        2. Sur la tablette, ouvre une seule fois (5080, ou le port de Server:Port) :
             http://<adresse du PC>:5080/?k={token}
           Le jeton passe dans un cookie et disparaît de l'adresse. Ne le garde nulle part ailleurs :
           ni dans un fichier du PC, ni dans une conversation avec Claude Code.

        """;
}
