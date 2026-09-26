using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain.Sites;

namespace ArtistShop.Web.Components.Pages.Platform.DeleteSite;

// form posts create this, and they need exactly one public constructor
public class DeleteSiteForm : ServerValidatedForm
{
    // typed out, so a website isn't deleted by a slip
    [Required]
    public string? Address { get; set; }

    // in any case, as a host is
    public bool Names(HostName host) => Address is not null && HostName.Read(Address) == host;

    public void AddWrongAddressError() => AddServerError(nameof(Address), "This isn't the website's address.");
}
