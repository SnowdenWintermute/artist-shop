namespace ArtistShop.Web.Domain.Website;

// the colours a theme can choose. A saved theme stores them by name, so renaming one means
// rewriting the themes table's settings too. From 1, so a post that leaves a role out binds 0,
// which isn't one
public enum ColorRole
{
    Page = 1,
    Ink = 2,
    Accent = 3,
    Bar = 4,
    Panel = 5,
    Placeholder = 6,
    InkFaded = 7,
    InkUnavailable = 8,
    Link = 9,
    LinkHover = 10,
    OnAccent = 11,
    Rule = 12,
    Lightbox = 13,
    OnLightbox = 14,
    Backdrop = 15,
}

// CssName is the role's name in app.css, as in --theme-page and bg-page. A base role is one the
// others are derived from, which every theme chooses
public record ColorRoleDefinition(ColorRole Role, string Name, string Description, string CssName)
{
    public bool IsBase => Role is ColorRole.Page or ColorRole.Ink or ColorRole.Accent;

    // only the backdrop, which is shown over the page rather than as a solid colour
    public bool AllowsOpacity => Role is ColorRole.Backdrop;

    public string CssVariable => $"--theme-{CssName}";
}

public static class ColorRoles
{
    // in the order the Theme page lists them, the base roles first
    public static readonly IReadOnlyList<ColorRoleDefinition> All =
    [
        new(ColorRole.Page, "Background", "The page behind everything.", "page"),
        new(ColorRole.Ink, "Text", "Headings and ordinary text.", "ink"),
        new(ColorRole.Accent, "Accent", "Main buttons and progress bars.", "accent"),
        new(ColorRole.Bar, "Menu bar", "The bar at the top and the menu it opens.", "bar"),
        new(ColorRole.Panel, "Panels", "Boxes and dialogs.", "panel"),
        new(ColorRole.Placeholder, "Image placeholder", "Where an image is still loading.", "placeholder"),
        new(ColorRole.InkFaded, "Faded text", "Secondary text, like a date or a count.", "ink-faded"),
        new(ColorRole.InkUnavailable, "Unavailable text", "A step that can't be taken, like Next on the last image.", "ink-unavailable"),
        new(ColorRole.Link, "Links", "Links, underlined in a sentence or standing alone.", "link"),
        new(ColorRole.LinkHover, "Links under the pointer", "Links that stand alone, like menu items, while the pointer is over them.", "link-hover"),
        new(ColorRole.OnAccent, "Text on the accent", "The label of a main button.", "on-accent"),
        new(ColorRole.Rule, "Lines", "Dividers and the borders of boxes and fields.", "rule"),
        new(ColorRole.Lightbox, "Full-screen background", "Behind an image opened full screen.", "lightbox"),
        new(ColorRole.OnLightbox, "Full-screen buttons", "The buttons over an image opened full screen.", "on-lightbox"),
        new(ColorRole.Backdrop, "Dialog backdrop", "Over the page behind an open dialog.", "backdrop"),
    ];

    public static ColorRoleDefinition For(ColorRole role) => All.Single(definition => definition.Role == role);
}
