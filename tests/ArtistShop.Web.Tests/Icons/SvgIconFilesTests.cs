using ArtistShop.Web.Components.Icons;
using ArtistShop.Web.Utilities;
using Microsoft.Extensions.FileProviders;

namespace ArtistShop.Web.Tests.Icons;

// Icons as SvgIconFiles draws them: colours on the root as currentColor, so a class on the svg reaches
// every shape, and no fixed size
public sealed class SvgIconFilesTests : IDisposable
{
    private readonly DirectoryInfo _webRoot = Directory.CreateTempSubdirectory();

    public void Dispose() => _webRoot.Delete(recursive: true);

    private string Markup(string name, string svg, string? cssClass = null)
    {
        var path = Path.Combine(_webRoot.FullName, SvgIconFiles.Folder, $"{name}.svg");
        Directory.CreateDirectory(Unwrap.Value(Path.GetDirectoryName(path)));
        File.WriteAllText(path, svg);

        using var provider = new PhysicalFileProvider(_webRoot.FullName);
        return new SvgIconFiles(provider).Markup(name, cssClass);
    }

    // the shape of the SVG Repo folder icon: no fill on the root, black on the shape
    [Fact]
    public void AShapesColourMovesToTheRootSoAFillClassReachesIt()
    {
        var markup = Markup(
            "folder-open",
            """<?xml version="1.0" encoding="utf-8"?><!-- from somewhere --><svg width="800px" height="800px" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M0 1H5Z" fill="#000000"/></svg>""",
            "size-5 fill-white"
        );

        Assert.Equal(
            """<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg" aria-hidden="true" class="size-5 fill-white"><path d="M0 1H5Z" /></svg>""",
            markup
        );
    }

    [Fact]
    public void AnIconInAFolderIsNamedByItsPathAndARootWithNoFillGetsOne()
    {
        var markup = Markup("files/csv", """<svg viewBox="0 0 48 48" xmlns="http://www.w3.org/2000/svg"><g><path d="M1 1Z"/></g></svg>""");

        Assert.Equal("""<svg viewBox="0 0 48 48" xmlns="http://www.w3.org/2000/svg" fill="currentColor" aria-hidden="true"><g><path d="M1 1Z" /></g></svg>""", markup);
    }

    // a shape that took "none" from the root stays unfilled once the root carries the colour
    [Fact]
    public void AnOutlineStaysAnOutline()
    {
        var markup = Markup(
            "outline",
            """<svg viewBox="0 0 8 8" fill="none" stroke="#333" xmlns="http://www.w3.org/2000/svg"><circle r="3"/><path d="M1 1Z" fill="black"/></svg>"""
        );

        Assert.Equal(
            """<svg viewBox="0 0 8 8" fill="currentColor" stroke="currentColor" xmlns="http://www.w3.org/2000/svg" aria-hidden="true"><circle r="3" fill="none" /><path d="M1 1Z" /></svg>""",
            markup
        );
    }

    [Fact]
    public void AMissingIconOrOneOutsideTheIconsFolderThrows()
    {
        File.WriteAllText(Path.Combine(_webRoot.FullName, "secret.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        using var provider = new PhysicalFileProvider(_webRoot.FullName);
        var icons = new SvgIconFiles(provider);

        Assert.Throws<InvalidOperationException>(() => icons.Markup("missing", null));
        Assert.Throws<InvalidOperationException>(() => icons.Markup("../secret", null));
    }
}
