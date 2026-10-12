namespace ArtistShop.Web.Database;

// Thrown by a repository when a UNIQUE constraint on an address rejects the write. The page
// catches it and shows a field message
public class AddressAlreadyInUseException(string address)
    : Exception($"The address \"{address}\" is already in use.")
{
    public string Address { get; } = address;
}
