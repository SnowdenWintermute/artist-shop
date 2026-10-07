using ArtistShop.Web.Components.Pages.Platform.DeleteSite;
using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Components.Pages.Platform.Operator;

// the owner's delete form, with the email that tells the owner
public class OperatorDeleteSiteForm : DeleteSiteForm
{
    // false unless posted: an unchecked checkbox sends nothing, so a default of true would outlast
    // unchecking it. The page checks it for the first showing instead
    public bool EmailOwner { get; set; }

    // the email's text, one paragraph per line
    public string? Message { get; set; }

    public static string DefaultMessage(HostName site, string platformName) =>
        $"Your website {site.Value} has been deleted from {platformName}, along with all its artworks, posts and images.";

    public void AddNoMessageError() => AddServerError(nameof(Message), "Write the email, or uncheck emailing the owner.");
}
