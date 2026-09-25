namespace ArtistShop.Web.Domain.Sites;

// an invitation for an email to become one of a site's admins, as the site's Admins page lists it
public record SiteInvite(EmailAddress Email, DateTimeOffset InvitedAt, DateTimeOffset ExpiresAt)
{
    public const int DaysValid = 7;

    public bool IsExpiredAt(DateTimeOffset moment) => ExpiresAt <= moment;
}

// an unexpired invitation to a site, as My websites lists it for the invited account
public record ReceivedInvite(SiteId SiteId, HostName MainHost, DateTimeOffset ExpiresAt);
