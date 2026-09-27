namespace ArtistShop.Web.Identity;

using System.ComponentModel.DataAnnotations;

// read from Authentication:Google in configuration by ValidatedSettings: the OAuth client made in the
// Google Cloud console, whose authorized redirect URI is the platform's /signin-google
public sealed record GoogleSettings
{
    [Required]
    public string ClientId { get; init; } = "";

    [Required]
    public string ClientSecret { get; init; } = "";
}
