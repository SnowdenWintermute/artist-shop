namespace ArtistShop.Web.Email;

using ArtistShop.Web.Utilities;

// Whether an email someone asked for may go. Register, Forgot password, the account page's Password
// box and admin invitations each email an address typed in, so without a limit anyone could flood an
// inbox and spend the sending reputation SES judges the domain by. Emails that follow something done,
// such as a deletion or a handover, aren't limited. RateLimitedEmailSendLimit in the app; the tests'
// fake refuses the addresses it's told to
public abstract class EmailSendLimit
{
    // false when the email mustn't go; true counts it as sent
    public abstract bool TryTake(string recipient, Requester requester);
}
