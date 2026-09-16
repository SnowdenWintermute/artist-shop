namespace ArtistShop.Web.Utilities;

public static class CountText
{
    // for nouns whose plural just adds "s"
    public static string Of(int count, string singular) =>
        count == 1 ? $"1 {singular}" : $"{count} {singular}s";
}
