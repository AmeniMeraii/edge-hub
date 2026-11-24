using System.ComponentModel.DataAnnotations;

namespace EdgeHub.Configuration;

/// <summary>Section <c>Security</c>. The token goes in <c>dotnet user-secrets</c> or <c>appsettings.Local.json</c>, never in git.</summary>
public sealed class SecurityOptions
{
    public const string Section = "Security";

    [Required(ErrorMessage = "Security:Token est manquant : ajoutez-le avec dotnet user-secrets ou dans appsettings.Local.json.")]
    [MinLength(32, ErrorMessage = "Security:Token doit faire au moins 32 caractères.")]
    public string Token { get; set; } = "";
}
