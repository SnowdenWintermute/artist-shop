namespace ArtistShop.Web.Domain.Website;

// Two colours too close to read one on the other, as the Theme page warns of them
public record ContrastProblem(ColorRole Foreground, ColorRole Background, double Ratio, double Minimum);

// A theme's colours. Every base role is chosen; any other role it didn't choose is derived from them
public sealed class ThemeColors
{
    private readonly IReadOnlyDictionary<ColorRole, RgbColor> _chosen;

    public ThemeColors(IReadOnlyDictionary<ColorRole, RgbColor> chosen)
    {
        if (ColorRoles.All.FirstOrDefault(definition => definition.IsBase && !chosen.ContainsKey(definition.Role)) is { } missing)
        {
            throw new ArgumentException($"A theme's colours need every base role, and {missing.Role} is missing.", nameof(chosen));
        }

        _chosen = chosen;
    }

    public IReadOnlyDictionary<ColorRole, RgbColor> Chosen => _chosen;

    public RgbColor? ChosenFor(ColorRole role) => _chosen.TryGetValue(role, out var color) ? color : null;

    public RgbColor Resolve(ColorRole role) => ChosenFor(role) ?? Derive(role);

    // whether any role that could be derived was chosen instead
    public bool ChoosesADerivableRole => _chosen.Keys.Any(role => !ColorRoles.For(role).IsBase);

    private RgbColor Derive(ColorRole role)
    {
        var page = Resolve(ColorRole.Page);
        var ink = Resolve(ColorRole.Ink);

        return role switch
        {
            ColorRole.Bar => page.Mix(ink, 0.06),
            // white on a light page, and a step lighter than a dark one
            ColorRole.Panel => page.IsLight ? RgbColor.White : page.Mix(ink, 0.08),
            ColorRole.Placeholder => page.Mix(ink, 0.05),
            ColorRole.InkFaded => ink.Mix(page, 0.4),
            ColorRole.InkUnavailable => ink.Mix(page, 0.55),
            ColorRole.Link => ink,
            ColorRole.LinkHover => Resolve(ColorRole.Accent),
            ColorRole.OnAccent => Resolve(ColorRole.Accent).ReadableText,
            ColorRole.Rule => ink,
            // shown over images, not beside the page's text, so black suits any page
            ColorRole.Lightbox => RgbColor.Black,
            ColorRole.OnLightbox => Resolve(ColorRole.Lightbox).ReadableText,
            ColorRole.Backdrop => RgbColor.Black.WithOpacityPercent(40),
            ColorRole.Page or ColorRole.Ink or ColorRole.Accent => throw new InvalidOperationException($"{role} is a base role, which isn't derived."),
        };
    }

    // what's read on what. Text needs 4.5:1. Unavailable text only has to be told apart from its
    // background, at 3:1, the same as a control's outline
    private static readonly IReadOnlyList<(ColorRole Foreground, ColorRole Background, double Minimum)> ReadPairs =
    [
        (ColorRole.Ink, ColorRole.Page, 4.5),
        (ColorRole.Ink, ColorRole.Panel, 4.5),
        // the drawer's text
        (ColorRole.Ink, ColorRole.Bar, 4.5),
        (ColorRole.InkFaded, ColorRole.Page, 4.5),
        (ColorRole.InkFaded, ColorRole.Panel, 4.5),
        (ColorRole.InkFaded, ColorRole.Bar, 4.5),
        (ColorRole.InkUnavailable, ColorRole.Page, 3),
        (ColorRole.InkUnavailable, ColorRole.Panel, 3),
        (ColorRole.Link, ColorRole.Page, 4.5),
        (ColorRole.Link, ColorRole.Panel, 4.5),
        (ColorRole.Link, ColorRole.Bar, 4.5),
        (ColorRole.LinkHover, ColorRole.Page, 4.5),
        (ColorRole.LinkHover, ColorRole.Bar, 4.5),
        (ColorRole.OnAccent, ColorRole.Accent, 4.5),
        (ColorRole.OnLightbox, ColorRole.Lightbox, 4.5),
    ];

    public IReadOnlyList<ContrastProblem> ContrastProblems() =>
    [
        .. ReadPairs
            .Select(pair => new ContrastProblem(
                pair.Foreground,
                pair.Background,
                RgbColor.Contrast(Resolve(pair.Foreground), Resolve(pair.Background)),
                pair.Minimum
            ))
            .Where(problem => problem.Ratio < problem.Minimum),
    ];
}
