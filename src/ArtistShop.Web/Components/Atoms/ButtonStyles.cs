namespace ArtistShop.Web.Components.Atoms;

// what ButtonBasic and LinkButton share, so a link and a button of one variant look the same
public static class ButtonStyles
{
    // one line, as the fixed height and zero line height would draw a wrapped label over itself
    public const string SizeClass = "p-2 h-10 leading-0 whitespace-nowrap";

    public static string VariantClass(ButtonVariant variant) =>
        variant switch
        {
            ButtonVariant.Plain => "",
            ButtonVariant.Primary => "bg-blue-500 text-white border",
            ButtonVariant.PrimaryOutline => "border border-blue-500 text-blue-600",
            ButtonVariant.Outline => "border",
            ButtonVariant.Danger => "bg-red-600 text-white",
            ButtonVariant.DangerOutline => "border border-red-600 text-red-600",
            ButtonVariant.DangerText => "text-red-600",
        };
}
