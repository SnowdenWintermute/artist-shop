namespace ArtistShop.Web.Components.Atoms;

// what ButtonBasic and LinkButton share, so a link and a button of one variant look the same
public static class ButtonStyles
{
    // one line, as the fixed height and zero line height would draw a wrapped label over itself
    public const string SizeClass = "p-2 h-10 min-w-16 leading-0 whitespace-nowrap";

    public static string VariantClass(ButtonVariant variant) =>
        variant switch
        {
            ButtonVariant.Plain => "",
            ButtonVariant.Primary => "bg-accent text-on-accent border",
            ButtonVariant.PrimaryOutline => "border border-accent text-accent",
            ButtonVariant.Outline => "border",
            ButtonVariant.Danger => "bg-error text-white",
            ButtonVariant.DangerOutline => "border border-error text-error",
            ButtonVariant.DangerText => "text-error",
        };
}
