using System.ComponentModel.DataAnnotations;

namespace EdgeHub.Configuration;

/// <summary>
/// Section <c>Security</c>: the hash of the tablet's token, made with <c>--new-token</c>. The token itself is shown
/// once and then lives only in the tablet's cookie.
/// </summary>
public sealed class SecurityOptions : IValidatableObject
{
    public const string Section = "Security";

    private const string HowTo = "lance EdgeHub.exe --new-token (ou dotnet run --project src/EdgeHub -- --new-token) et mets l'empreinte affichée dans user-secrets ou appsettings.Local.json";

    /// <summary>SHA-256 of the tablet's token, 64 hexadecimal characters.</summary>
    [Required(ErrorMessage = $"Security:TokenHash est manquant : {HowTo}.")]
    [RegularExpression("^[0-9a-fA-F]{64}$", ErrorMessage = $"Security:TokenHash doit être l'empreinte d'un jeton (64 caractères hexadécimaux) : {HowTo}.")]
    public string TokenHash { get; set; } = "";

    /// <summary>The token in clear, as kept before phase 4c: refused, since an agent could read it.</summary>
    public string? Token { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Token))
        {
            // The message never repeats the token.
            yield return new ValidationResult(
                $"Security:Token n'est plus accepté : le PC ne garde que l'empreinte du jeton. Retire Security:Token, puis {HowTo}.",
                [nameof(Token)]);
        }
    }
}
