namespace ArtistShop.Web.Components.CustomPages;

using System.Globalization;
using ArtistShop.Web.Components.Layout;
using ArtistShop.Web.Domain.Website;

// A button component's choices as Tailwind classes, spelled out so Tailwind finds them. Its corners and border
// width are the theme frame's (FrameCss), an outline's border never thinner than a pixel so it still shows. Its
// padding is in ems, so it grows with the size's font size
public static class ButtonClasses
{
    public static string Of(ButtonSettings button) =>
        "inline-block rounded-(--theme-frame-corners) px-[1em] py-[0.5em] text-center leading-[calc(1.25*var(--current-font-scale))] "
        + $"{Border(button.Style)} {Colors(button.Style, button.Color)}";

    // 12px at the smallest, 4px more each step: 16px, ordinary text, is 2. A border width or corners of its own are
    // set over the theme frame's, as a custom frame's are
    public static string Style(ButtonSettings button) =>
        $"font-size: {(12 + (button.Size - 1) * 4).ToString(CultureInfo.InvariantCulture)}px; "
        + FrameCss.Overrides(new ComponentFrame.Custom(null, null, button.BorderWidth, button.Corners, null));

    public static string Alignment(ComponentAlignment alignment) =>
        alignment switch
        {
            ComponentAlignment.Left => "justify-start",
            ComponentAlignment.Center => "justify-center",
            ComponentAlignment.Right => "justify-end",
            _ => throw new InvalidOperationException($"{alignment} has no class."),
        };

    private static string Border(ButtonStyle style) =>
        style switch
        {
            ButtonStyle.Filled => "border-(length:--theme-frame-border-width)",
            ButtonStyle.Outline => "border-[length:max(1px,var(--theme-frame-border-width))]",
            _ => throw new InvalidOperationException($"{style} has no border."),
        };

    private static string Colors(ButtonStyle style, ButtonColor color) =>
        (style, color) switch
        {
            (ButtonStyle.Filled, ButtonColor.Accent) => "border-accent bg-accent text-on-accent",
            (ButtonStyle.Filled, ButtonColor.Primary) => "border-ink bg-ink text-page",
            (ButtonStyle.Filled, ButtonColor.Normal) => "border-rule bg-panel text-ink",
            (ButtonStyle.Outline, ButtonColor.Accent) => "border-accent text-accent",
            (ButtonStyle.Outline, ButtonColor.Primary) => "border-ink text-ink",
            (ButtonStyle.Outline, ButtonColor.Normal) => "border-rule text-ink",
            _ => throw new InvalidOperationException($"A {style} {color} button has no colours."),
        };

}
