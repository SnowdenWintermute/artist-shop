namespace ArtistShop.Web.Utilities;

public static class SetToggle
{
    // for a checkbox list: takes the value out if it's there, otherwise puts it in
    public static void Toggle<T>(this ISet<T> set, T value)
    {
        if (!set.Remove(value))
        {
            set.Add(value);
        }
    }
}
