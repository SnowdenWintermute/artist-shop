namespace ArtistShop.Web.Components.Atoms;

// what ButtonBasic and LinkButton share, so a link and a button of one variant look the same
public static class ButtonStyles
{
    public const string SizeClass = "p-2 h-10 leading-0";

    public static string VariantClass(ButtonVariant variant) =>
        variant switch
        {
            ButtonVariant.Plain => "",
            ButtonVariant.Primary => "bg-blue-400 text-white",
            ButtonVariant.PrimaryOutline => "border border-blue-400 text-blue-600",
            ButtonVariant.Outline => "border",
            ButtonVariant.Danger => "bg-red-600 text-white",
            ButtonVariant.DangerOutline => "border border-red-600 text-red-600",
            ButtonVariant.DangerText => "text-red-600",
        };
}
