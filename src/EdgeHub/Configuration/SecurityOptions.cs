using System.ComponentModel.DataAnnotations;

namespace EdgeHub.Configuration;

/// <summary>Section <c>Security</c>. The token goes in <c>dotnet user-secrets</c> or <c>appsettings.Local.json</c>, never in git.</summary>
public sealed class SecurityOptions
{
    public const string Section = "Security";

    [Required(ErrorMessage = "Security:Token est manquant : ajoutez-le avec dotnet user-secrets ou dans appsettings.Local.json.")]
    [MinLength(32, ErrorMessage = "Security:Token doit faire au moins 32 caractères.")]
    [RegularExpression(
        "^[A-Za-z0-9_-]*$",
        ErrorMessage = "Security:Token ne doit contenir que des lettres, des chiffres, - et _ : il passe tel quel dans une adresse.")]
    public string Token { get; set; } = "";
}
