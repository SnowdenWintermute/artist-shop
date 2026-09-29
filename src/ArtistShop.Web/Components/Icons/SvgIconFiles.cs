using System.Collections.Concurrent;
using System.Xml.Linq;
using Microsoft.Extensions.FileProviders;

namespace ArtistShop.Web.Components.Icons;

// The icons in wwwroot/icons, read once and made ready to draw inline. Fill and stroke colours move
// to the root <svg> as currentColor, so an icon takes the colour of the text around it, and a
// fill-*, stroke-* or text-* class on it reaches every shape. A file with several colours, like a
// brand's logo, becomes one colour, so it belongs in an <img> instead
public class SvgIconFiles(IFileProvider webRoot)
{
    public const string Folder = "icons";

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    // attributes that would fix the size or clash with another copy of the icon on the same page
    private static readonly string[] RootAttributesDropped = ["width", "height", "id", "version", "x", "y"];

    private static readonly HashSet<string> Shapes = ["path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "use"];

    private readonly ConcurrentDictionary<string, XElement> _icons = new();

    // name is the path inside wwwroot/icons without ".svg", like "folder-open" or "brands/google".
    // A name with no file is a mistake in the code, so it throws
    public string Markup(string name, string? cssClass)
    {
        var icon = new XElement(_icons.GetOrAdd(name, Load));

        if (cssClass is not null)
        {
            icon.SetAttributeValue("class", cssClass);
        }

        return icon.ToString(SaveOptions.DisableFormatting);
    }

    private XElement Load(string name)
    {
        // only icons: the file provider would still read anything else in wwwroot by a "../" path
        if (name.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidOperationException($"\"{name}\" isn't a path inside {Folder}.");
        }

        var file = webRoot.GetFileInfo($"{Folder}/{name}.svg");

        if (!file.Exists || file.IsDirectory)
        {
            throw new InvalidOperationException($"There's no icon {Folder}/{name}.svg in wwwroot.");
        }

        using var stream = file.CreateReadStream();
        var root = XDocument.Load(stream).Root;

        if (root is null || root.Name != Svg + "svg")
        {
            throw new InvalidOperationException($"{Folder}/{name}.svg isn't an SVG.");
        }

        root.DescendantNodesAndSelf().OfType<XComment>().Remove();

        foreach (var attribute in RootAttributesDropped)
        {
            root.SetAttributeValue(attribute, null);
        }

        // a shape with no fill anywhere above it is filled black; with no stroke, it has none
        Recolor(root, "fill", paintsByDefault: true);
        Recolor(root, "stroke", paintsByDefault: false);

        // it sits beside text that says what it's for
        root.SetAttributeValue("aria-hidden", "true");

        return root;
    }

    // Every colour for this property moves to the root as currentColor, and the shapes inherit it.
    // A shape that took "none" from the root keeps it, so an outline stays an outline
    private static void Recolor(XElement root, string property, bool paintsByDefault)
    {
        var rootValue = root.Attribute(property)?.Value;
        var rootPaints = rootValue is null ? paintsByDefault : rootValue != "none";
        var descendants = root.Descendants().ToList();
        var colored = descendants.Where(element => element.Attribute(property) is { Value: not "none" }).ToList();

        if (!rootPaints && colored.Count == 0)
        {
            return;
        }

        if (!rootPaints)
        {
            var takingNone = descendants
                .Where(element => Shapes.Contains(element.Name.LocalName))
                .Where(shape => shape.AncestorsAndSelf().First(element => element == root || element.Attribute(property) is not null) == root)
                .ToList();

            foreach (var shape in takingNone)
            {
                shape.SetAttributeValue(property, "none");
            }
        }

        foreach (var element in colored)
        {
            element.SetAttributeValue(property, null);
        }

        root.SetAttributeValue(property, "currentColor");
    }
}
