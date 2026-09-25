namespace ArtistShop.Web.Domain;

using System.ComponentModel.DataAnnotations;

// An email address as invitations keep it: trimmed and lowercase, so an invitation matches an
// account's email however either was typed
public sealed record EmailAddress
{
    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    // null for anything the account pages' [EmailAddress] check refuses, or too long to store
    public static EmailAddress? Read(string value)
    {
        var email = value.Trim().ToLowerInvariant();

        return email.Length <= ArtistShopLimits.EmailMaximumLength && new EmailAddressAttribute().IsValid(email)
            ? new EmailAddress(email)
            : null;
    }
}
