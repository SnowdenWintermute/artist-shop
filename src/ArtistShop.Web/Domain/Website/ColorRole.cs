namespace ArtistShop.Web.Domain.Website;

// the colours an artist can choose for their website. The values must match the ids seeded into
// color_roles
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

// CssName is the role's name in app.css, as in --theme-page and bg-page. Platform is the colour
// every page has until a website chooses its own. A base role is one the others are derived from
public record ColorRoleDefinition(ColorRole Role, string Name, string Description, string CssName, RgbColor Platform)
{
    public bool IsBase => Role is ColorRole.Page or ColorRole.Ink or ColorRole.Accent;

    // only the backdrop, which is shown over the page rather than as a solid colour
    public bool AllowsOpacity => Role is ColorRole.Backdrop;

    public string CssVariable => $"--theme-{CssName}";
}

public static class ColorRoles
{
    // in the order the Colors page lists them, the base roles first
    public static readonly IReadOnlyList<ColorRoleDefinition> All =
    [
        new(ColorRole.Page, "Background", "The page behind everything.", "page", RgbColor.Parse("#f5f5f5")),
        new(ColorRole.Ink, "Text", "Headings and ordinary text.", "ink", RgbColor.Black),
        new(ColorRole.Accent, "Accent", "Main buttons and progress bars.", "accent", RgbColor.Parse("#155dfc")),
        new(ColorRole.Bar, "Menu bar", "The bar at the top and the menu it opens.", "bar", RgbColor.Parse("#e9e7e2")),
        new(ColorRole.Panel, "Panels", "Boxes and dialogs.", "panel", RgbColor.White),
        new(ColorRole.Placeholder, "Image placeholder", "Where an image is still loading.", "placeholder", RgbColor.Parse("#f3f4f6")),
        new(ColorRole.InkFaded, "Faded text", "Secondary text, like a date or a count.", "ink-faded", RgbColor.Parse("#5f6878")),
        new(ColorRole.InkUnavailable, "Unavailable text", "A step that can't be taken, like Next on the last image.", "ink-unavailable", RgbColor.Parse("#7d8594")),
        new(ColorRole.Link, "Links", "Links, underlined in a sentence or standing alone.", "link", RgbColor.Black),
        new(ColorRole.LinkHover, "Links under the pointer", "Links that stand alone, like menu items, while the pointer is over them.", "link-hover", RgbColor.Parse("#0000ff")),
        new(ColorRole.OnAccent, "Text on the accent", "The label of a main button.", "on-accent", RgbColor.White),
        new(ColorRole.Rule, "Lines", "Dividers and the borders of boxes and fields.", "rule", RgbColor.Black),
        new(ColorRole.Lightbox, "Full-screen background", "Behind an image opened full screen.", "lightbox", RgbColor.Black),
        new(ColorRole.OnLightbox, "Full-screen buttons", "The buttons over an image opened full screen.", "on-lightbox", RgbColor.White),
        new(ColorRole.Backdrop, "Dialog backdrop", "Over the page behind an open dialog.", "backdrop", RgbColor.Black.WithOpacityPercent(40)),
    ];

    public static ColorRoleDefinition For(ColorRole role) => All.Single(definition => definition.Role == role);
}
