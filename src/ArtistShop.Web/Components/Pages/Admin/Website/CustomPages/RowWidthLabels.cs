namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Website;

// how the row editor shows each split and alignment
public static class RowWidthLabels
{
    public static string Of(RowWidths widths) =>
        widths switch
        {
            RowWidths.Whole => "Whole",
            RowWidths.Halves => "½ ½",
            RowWidths.ThirdThenTwoThirds => "⅓ ⅔",
            RowWidths.TwoThirdsThenThird => "⅔ ⅓",
            RowWidths.Thirds => "⅓ ⅓ ⅓",
            RowWidths.QuarterHalfQuarter => "¼ ½ ¼",
            _ => throw new InvalidOperationException($"{widths} has no label."),
        };

    public static string Of(RowAlignment alignment) =>
        alignment switch
        {
            RowAlignment.Start => "Top",
            RowAlignment.Center => "Middle",
            RowAlignment.End => "Bottom",
            _ => throw new InvalidOperationException($"{alignment} has no label."),
        };
}
