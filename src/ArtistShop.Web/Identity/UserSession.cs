namespace ArtistShop.Web.Identity;

// One sign-in, kept in the identity database by DatabaseTicketStore. The browser's cookie holds only
// Id; Ticket is everything the cookie would otherwise hold. SecurityStamp is the account's stamp in
// Ticket, so a later change of the account's stamp ends it
public sealed class UserSession
{
    public required string Id { get; init; }

    public required string UserId { get; init; }

    public required string SecurityStamp { get; set; }

    public required byte[] Ticket { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }

    // On a website, the platform sign-in it was handed over from (SiteSignIns). Deleting that row
    // deletes this one, so logging out on any host ends every sign-in in the browser
    public string? ParentId { get; init; }
}
