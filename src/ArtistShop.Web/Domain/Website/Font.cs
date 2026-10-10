namespace ArtistShop.Web.Domain.Website;

// A font a theme can use. Stored by name, in the platform's font_order and in themes
public enum Font
{
    Roboto,
    JosefinSlab,
    Isometra,
    OpenSans,
    PlayfairDisplay,
    RobotoSlab,
    Quicksand,
    ArchivoBlack,
    BlackOpsOne,
    BricolageGrotesque,
    TitilliumWeb,
    ShareTech,
    SmoochSans,
    PlaywriteAR,
    PTSerif,
    IbmPlexMono,
    DancingScript,
    Fraunces,
    Bitter,
    DMSerifDisplay,
    Exo2,
    Orbitron,
    Cinzel,
    Pacifico,
    Bungee,
    ZillaSlab,
    PermanentMarker,
    LuckiestGuy,
    PressStart2P,
    Yellowtail,
    Creepster,
    Quantico,
    Chicle,
    FugazOne,
    AmaticSC,
    RussoOne,
    TenorSans,
    Rowdies,
    Audiowide,
    SpecialElite,
    Sancreek,
    OldStandardTT,
    RubikMonoOne,
    ChelseaMarket,
    Sacramento,
    EagleLake,
    PTSansCaption,
    VT323,
    Philosopher,
}

public record FontFace(int Weight, bool IsItalic, string FileName);

// Family is the CSS font-family name. XHeight and CapHeight are the heights of its x and H as fractions of
// the font size, measured from the regular face's shapes. RecordedXHeight is the x-height the font file
// states, which browsers go by for font-size-adjust: several fonts state it wrongly, like Sancreek's 0.196
public record FontDefinition(
    Font Font,
    string Family,
    string Folder,
    double XHeight,
    double CapHeight,
    double RecordedXHeight,
    IReadOnlyList<FontFace> Faces
)
{
    // Without its own bold and italic, the browser fakes them, which looks poor in the text font where
    // they're common
    public bool IsHeadingOnly =>
        !(
            Has(weight: 400, isItalic: false)
            && Has(weight: 400, isItalic: true)
            && Has(weight: 700, isItalic: false)
            && Has(weight: 700, isItalic: true)
        );

    // quoted, for a font-family declaration
    public string CssFamily => $"\"{Family}\"";

    public string Url(FontFace face) => $"/fonts/{Folder}/{face.FileName}";

    private bool Has(int weight, bool isItalic) => Faces.Any(face => face.Weight == weight && face.IsItalic == isItalic);
}

// Every font's files are in wwwroot/fonts; SOURCES.md says where they came from
public static class Fonts
{
    public static readonly IReadOnlyList<FontDefinition> All =
    [
        new(Font.Roboto, "Roboto", "roboto", 0.528, 0.711, 0.528, [new(400, false, "Roboto-Regular.woff2"), new(400, true, "Roboto-Regular-Italic.woff2"), new(700, false, "Roboto-Bold.woff2"), new(700, true, "Roboto-Bold-Italic.woff2")]),
        new(Font.JosefinSlab, "Josefin Slab", "josefin-slab", 0.375, 0.700, 0.375, [new(400, false, "JosefinSlab-Regular.woff2"), new(400, true, "JosefinSlab-Regular-Italic.woff2"), new(700, false, "JosefinSlab-Bold.woff2"), new(700, true, "JosefinSlab-Bold-Italic.woff2")]),
        new(Font.Isometra, "Isometra", "isometra", 0.600, 0.800, 0.600, [new(400, false, "Isometra-Regular.woff2")]),
        new(Font.OpenSans, "Open Sans", "open-sans", 0.535, 0.714, 0.535, [new(400, false, "OpenSans-Regular.woff2"), new(400, true, "OpenSans-Regular-Italic.woff2"), new(700, false, "OpenSans-Bold.woff2"), new(700, true, "OpenSans-Bold-Italic.woff2")]),
        new(Font.PlayfairDisplay, "Playfair Display", "playfair-display", 0.515, 0.708, 0.514, [new(400, false, "PlayfairDisplay-Regular.woff2"), new(400, true, "PlayfairDisplay-Regular-Italic.woff2"), new(700, false, "PlayfairDisplay-Bold.woff2"), new(700, true, "PlayfairDisplay-Bold-Italic.woff2")]),
        new(Font.RobotoSlab, "Roboto Slab", "roboto-slab", 0.528, 0.711, 0.528, [new(400, false, "RobotoSlab-Regular.woff2"), new(700, false, "RobotoSlab-Bold.woff2")]),
        new(Font.Quicksand, "Quicksand", "quicksand", 0.516, 0.700, 0.503, [new(400, false, "Quicksand-Regular.woff2"), new(700, false, "Quicksand-Bold.woff2")]),
        new(Font.ArchivoBlack, "Archivo Black", "archivo-black", 0.528, 0.688, 0.528, [new(400, false, "ArchivoBlack-Regular.woff2")]),
        new(Font.BlackOpsOne, "Black Ops One", "black-ops-one", 0.519, 0.648, 0.519, [new(400, false, "BlackOpsOne-Regular.woff2")]),
        new(Font.BricolageGrotesque, "Bricolage Grotesque", "bricolage-grotesque", 0.517, 0.660, 0.517, [new(400, false, "BricolageGrotesque-Regular.woff2"), new(700, false, "BricolageGrotesque-Bold.woff2")]),
        new(Font.TitilliumWeb, "Titillium Web", "titillium-web", 0.500, 0.692, 0.500, [new(400, false, "TitilliumWeb-Regular.woff2"), new(400, true, "TitilliumWeb-Regular-Italic.woff2"), new(700, false, "TitilliumWeb-Bold.woff2"), new(700, true, "TitilliumWeb-Bold-Italic.woff2")]),
        new(Font.ShareTech, "Share Tech", "share-tech", 0.500, 0.700, 0.500, [new(400, false, "ShareTech-Regular.woff2")]),
        new(Font.SmoochSans, "Smooch Sans", "smooch-sans", 0.470, 0.620, 0.470, [new(400, false, "SmoochSans-Regular.woff2"), new(700, false, "SmoochSans-Bold.woff2")]),
        new(Font.PlaywriteAR, "Playwrite AR", "playwrite-ar", 0.519, 1.080, 0.500, [new(400, false, "PlaywriteAR-Regular.woff2")]),
        new(Font.PTSerif, "PT Serif", "pt-serif", 0.500, 0.700, 0.500, [new(400, false, "PTSerif-Regular.woff2"), new(400, true, "PTSerif-Regular-Italic.woff2"), new(700, false, "PTSerif-Bold.woff2"), new(700, true, "PTSerif-Bold-Italic.woff2")]),
        new(Font.IbmPlexMono, "IBM Plex Mono", "ibm-plex-mono", 0.516, 0.698, 0.516, [new(400, false, "IbmPlexMono-Regular.woff2"), new(400, true, "IbmPlexMono-Regular-Italic.woff2"), new(700, false, "IbmPlexMono-Bold.woff2"), new(700, true, "IbmPlexMono-Bold-Italic.woff2")]),
        new(Font.DancingScript, "Dancing Script", "dancing-script", 0.332, 0.720, 0.332, [new(400, false, "DancingScript-Regular.woff2"), new(700, false, "DancingScript-Bold.woff2")]),
        new(Font.Fraunces, "Fraunces", "fraunces", 0.469, 0.700, 0.482, [new(400, false, "Fraunces-Regular.woff2"), new(400, true, "Fraunces-Regular-Italic.woff2"), new(700, false, "Fraunces-Bold.woff2"), new(700, true, "Fraunces-Bold-Italic.woff2")]),
        new(Font.Bitter, "Bitter", "bitter", 0.532, 0.698, 0.528, [new(400, false, "Bitter-Regular.woff2"), new(400, true, "Bitter-Regular-Italic.woff2"), new(700, false, "Bitter-Bold.woff2"), new(700, true, "Bitter-Bold-Italic.woff2")]),
        new(Font.DMSerifDisplay, "DM Serif Display", "dm-serif-display", 0.481, 0.660, 0.481, [new(400, false, "DMSerifDisplay-Regular.woff2"), new(400, true, "DMSerifDisplay-Regular-Italic.woff2")]),
        new(Font.Exo2, "Exo 2", "exo-2", 0.487, 0.690, 0.487, [new(400, false, "Exo2-Regular.woff2"), new(400, true, "Exo2-Regular-Italic.woff2"), new(700, false, "Exo2-Bold.woff2"), new(700, true, "Exo2-Bold-Italic.woff2")]),
        new(Font.Orbitron, "Orbitron", "orbitron", 0.580, 0.720, 0.580, [new(400, false, "Orbitron-Regular.woff2"), new(700, false, "Orbitron-Bold.woff2")]),
        new(Font.Cinzel, "Cinzel", "cinzel", 0.600, 0.700, 0.500, [new(400, false, "Cinzel-Regular.woff2"), new(700, false, "Cinzel-Bold.woff2")]),
        new(Font.Pacifico, "Pacifico", "pacifico", 0.470, 0.877, 0.460, [new(400, false, "Pacifico-Regular.woff2")]),
        new(Font.Bungee, "Bungee", "bungee", 0.720, 0.720, 0.500, [new(400, false, "Bungee-Regular.woff2")]),
        new(Font.ZillaSlab, "Zilla Slab", "zilla-slab", 0.445, 0.650, 0.445, [new(400, false, "ZillaSlab-Regular.woff2"), new(400, true, "ZillaSlab-Regular-Italic.woff2"), new(700, false, "ZillaSlab-Bold.woff2"), new(700, true, "ZillaSlab-Bold-Italic.woff2")]),
        new(Font.PermanentMarker, "Permanent Marker", "permanent-marker", 0.590, 0.740, 0.610, [new(400, false, "PermanentMarker-Regular.woff2")]),
        new(Font.LuckiestGuy, "Luckiest Guy", "luckiest-guy", 0.692, 0.702, 0.684, [new(400, false, "LuckiestGuy-Regular.woff2")]),
        new(Font.PressStart2P, "Press Start 2P", "press-start-2p", 0.750, 1.000, 0.750, [new(400, false, "PressStart2P-Regular.woff2")]),
        new(Font.Yellowtail, "Yellowtail", "yellowtail", 0.444, 0.721, 0.444, [new(400, false, "Yellowtail-Regular.woff2")]),
        new(Font.Creepster, "Creepster", "creepster", 0.732, 0.745, 0.733, [new(400, false, "Creepster-Regular.woff2")]),
        new(Font.Quantico, "Quantico", "quantico", 0.500, 0.700, 0.500, [new(400, false, "Quantico-Regular.woff2"), new(400, true, "Quantico-Regular-Italic.woff2"), new(700, false, "Quantico-Bold.woff2"), new(700, true, "Quantico-Bold-Italic.woff2")]),
        new(Font.Chicle, "Chicle", "chicle", 0.497, 0.736, 0.277, [new(400, false, "Chicle-Regular.woff2")]),
        new(Font.FugazOne, "Fugaz One", "fugaz-one", 0.488, 0.720, 0.488, [new(400, false, "FugazOne-Regular.woff2")]),
        new(Font.AmaticSC, "Amatic SC", "amatic-sc", 0.662, 0.758, 0.659, [new(400, false, "AmaticSC-Regular.woff2"), new(700, false, "AmaticSC-Bold.woff2")]),
        new(Font.RussoOne, "Russo One", "russo-one", 0.530, 0.700, 0.530, [new(400, false, "RussoOne-Regular.woff2")]),
        new(Font.TenorSans, "Tenor Sans", "tenor-sans", 0.500, 0.700, 0.500, [new(400, false, "TenorSans-Regular.woff2")]),
        new(Font.Rowdies, "Rowdies", "rowdies", 0.472, 0.708, 0.473, [new(400, false, "Rowdies-Regular.woff2"), new(700, false, "Rowdies-Bold.woff2")]),
        new(Font.Audiowide, "Audiowide", "audiowide", 0.528, 0.700, 0.529, [new(400, false, "Audiowide-Regular.woff2")]),
        new(Font.SpecialElite, "Special Elite", "special-elite", 0.481, 0.689, 0.258, [new(400, false, "SpecialElite-Regular.woff2")]),
        new(Font.Sancreek, "Sancreek", "sancreek", 0.640, 0.763, 0.196, [new(400, false, "Sancreek-Regular.woff2")]),
        new(Font.OldStandardTT, "Old Standard TT", "old-standard-tt", 0.456, 0.712, 0.456, [new(400, false, "OldStandardTT-Regular.woff2"), new(400, true, "OldStandardTT-Regular-Italic.woff2"), new(700, false, "OldStandardTT-Bold.woff2")]),
        new(Font.RubikMonoOne, "Rubik Mono One", "rubik-mono-one", 0.700, 0.700, 0.700, [new(400, false, "RubikMonoOne-Regular.woff2")]),
        new(Font.ChelseaMarket, "Chelsea Market", "chelsea-market", 0.585, 0.735, 0.594, [new(400, false, "ChelseaMarket-Regular.woff2")]),
        new(Font.Sacramento, "Sacramento", "sacramento", 0.306, 0.744, 0.306, [new(400, false, "Sacramento-Regular.woff2")]),
        new(Font.EagleLake, "Eagle Lake", "eagle-lake", 0.522, 0.723, 0.534, [new(400, false, "EagleLake-Regular.woff2")]),
        new(Font.PTSansCaption, "PT Sans Caption", "pt-sans-caption", 0.525, 0.700, 0.525, [new(400, false, "PTSansCaption-Regular.woff2"), new(700, false, "PTSansCaption-Bold.woff2")]),
        new(Font.VT323, "VT323", "vt323", 0.400, 0.560, 0.400, [new(400, false, "VT323-Regular.woff2")]),
        new(Font.Philosopher, "Philosopher", "philosopher", 0.470, 0.660, 0.470, [new(400, false, "Philosopher-Regular.woff2"), new(400, true, "Philosopher-Regular-Italic.woff2"), new(700, false, "Philosopher-Bold.woff2"), new(700, true, "Philosopher-Bold-Italic.woff2")]),
    ];

    public static FontDefinition For(Font font) => All.Single(definition => definition.Font == font);

    // the operator's order, from FontRepository; a font it doesn't place yet, like one added since, comes
    // after, in All's order
    public static IReadOnlyList<FontDefinition> InOrder(IReadOnlyList<Font> order) =>
        [.. order.Where(Enum.IsDefined).Distinct().Select(For), .. All.Where(definition => !order.Contains(definition.Font))];
}
