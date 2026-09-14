namespace ArtistShop.Web.Database;

// Thrown by a repository when a UNIQUE constraint on a name rejects the write. The page
// catches it and shows a field message.
// System.Data already has a class called DuplicateNameException
// (used for DataSet columns), and every repository imports System.Data
public class NameAlreadyInUseException(string name)
    : Exception($"The name \"{name}\" is already in use.")
{
    public string Name { get; } = name;
}
