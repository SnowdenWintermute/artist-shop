namespace ArtistShop.Web.Database;

// A sign-up code that was never made, has expired, or has been used or revoked
public class SignUpCodeNotUsableException()
    : Exception("The sign-up code was never made, has expired, or has been used.");
