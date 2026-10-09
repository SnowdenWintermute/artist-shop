namespace ArtistShop.Web.Components.Atoms;

// the two looks a link has, so every link takes the website's link colours the same way
public static class LinkStyles
{
    // on its own, like a menu item or Previous: changes colour under the pointer
    public const string Standalone = "text-link hover:text-link-hover";

    // in a sentence or a heading, where only the underline sets it apart
    public const string InSentence = "text-link underline";
}
