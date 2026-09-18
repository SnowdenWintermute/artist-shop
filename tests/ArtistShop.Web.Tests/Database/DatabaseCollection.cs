namespace ArtistShop.Web.Tests.Database;

// Every test in this collection works against the one ArtistShopTests database, and some of them
// assert on a whole table: reordering the series posts the complete list, and the procedure refuses
// a list that is missing a row. xUnit runs collections in parallel with each other, so a class
// inserting a series while another reads them all made that test fail about one run in four.
// Sharing a collection runs them in sequence instead. The tests that touch no database stay parallel
[CollectionDefinition(Name)]
public class DatabaseCollection
{
    public const string Name = "Database";
}
