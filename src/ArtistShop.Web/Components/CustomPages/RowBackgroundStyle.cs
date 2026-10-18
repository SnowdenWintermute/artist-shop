namespace ArtistShop.Web.Components.CustomPages;

using ArtistShop.Web.Domain.Website;

// a row's background as an inline style, since a colour is anything the artist picks
public static class RowBackgroundStyle
{
    public static string Of(RowBackground background) =>
        background switch
        {
            RowBackground.None => "",
            RowBackground.Color color => $"background-color: {color.Value.Hex};",
            _ => throw new InvalidOperationException($"{background.GetType().Name} has no style."),
        };
}
