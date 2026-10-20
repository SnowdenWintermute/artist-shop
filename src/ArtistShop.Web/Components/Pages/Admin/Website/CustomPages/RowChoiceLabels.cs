namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

using ArtistShop.Web.Domain.Website;

// how the row editor shows each of a row's choices
public static class RowChoiceLabels
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

    public static string Of(RowHeight height) =>
        height switch
        {
            RowHeight.Natural => "Natural",
            RowHeight.FillScreen => "Fill the screen",
            _ => throw new InvalidOperationException($"{height} has no label."),
        };

    public static string Of(RowContentWidth width) =>
        width switch
        {
            RowContentWidth.Page => "Page width",
            RowContentWidth.EdgeToEdge => "Edge to edge",
            _ => throw new InvalidOperationException($"{width} has no label."),
        };

    public static string Of(RowBackgroundType type) =>
        type switch
        {
            RowBackgroundType.None => "None",
            RowBackgroundType.Color => "Colour",
            RowBackgroundType.Image => "Image",
            RowBackgroundType.Slideshow => "Slideshow",
            _ => throw new InvalidOperationException($"{type} has no label."),
        };

    public static string Of(RowGap gap) =>
        gap switch
        {
            RowGap.None => "None",
            RowGap.Small => "Small",
            RowGap.Medium => "Medium",
            RowGap.Large => "Large",
            _ => throw new InvalidOperationException($"{gap} has no label."),
        };

    public static string Of(RowVerticalPadding padding) =>
        padding switch
        {
            RowVerticalPadding.Auto => "Auto",
            RowVerticalPadding.None => "None",
            RowVerticalPadding.Small => "Small",
            RowVerticalPadding.Medium => "Medium",
            RowVerticalPadding.Large => "Large",
            _ => throw new InvalidOperationException($"{padding} has no label."),
        };

    public static string Of(RowSidePadding padding) =>
        padding switch
        {
            RowSidePadding.None => "None",
            RowSidePadding.Small => "Small",
            RowSidePadding.Medium => "Medium",
            RowSidePadding.Large => "Large",
            _ => throw new InvalidOperationException($"{padding} has no label."),
        };
}
