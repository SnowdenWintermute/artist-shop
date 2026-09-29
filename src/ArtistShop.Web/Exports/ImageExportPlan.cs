using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// one image of an artwork, at its path inside the download's folder, without the extension its bytes decide
public record ImageExportEntry(string PathWithoutExtension, Artwork Artwork, ArtworkImage Image);

// One image download: the originals of one artwork type's artworks in one series, or in none.
// The folders are Type/Series/, the layout the bulk image upload reads when given the Type folder
public record ImageExportPart(
    ArtworkType Type,
    Series? Series,
    string TypeFolder,
    string? SeriesFolder,
    IReadOnlyList<ImageExportEntry> Entries
);

public static class ImageExportPlan
{
    // Every part that has images. An artwork in several series goes in its first, since the bulk
    // upload attaches an image to one artwork. Types, then series, by name, with no series last
    public static List<ImageExportPart> Parts(IEnumerable<Artwork> artworks) =>
        [
            .. artworks
                .Where(artwork => artwork.Images.Count > 0)
                .GroupBy(artwork => (artwork.Type, Series: artwork.Series.FirstOrDefault()))
                .OrderBy(group => group.Key.Type.Name.Value, ImportNames.Comparer)
                .ThenBy(group => group.Key.Series is null)
                .ThenBy(group => group.Key.Series?.Name.Value, ImportNames.Comparer)
                .Select(group => Part(group.Key.Type, group.Key.Series, [.. group])),
        ];

    private static string TypeFolder(ArtworkType type) =>
        ExportFileNames.IsPortable(type.Name.Value) ? type.Name.Value : $"artwork-type-{type.Id.Value}";

    private static ImageExportPart Part(ArtworkType type, Series? series, List<Artwork> artworks)
    {
        var typeFolder = TypeFolder(type);
        // A slug can't match another series' name, even ignoring case: that name's slug would be
        // the same slug, which unique_series_slug refuses
        string? seriesFolder = series is null ? null
            : ExportFileNames.IsPortable(series.Name.Value) ? series.Name.Value
            : series.Slug.Value;
        var folder = seriesFolder is null ? typeFolder : $"{typeFolder}/{seriesFolder}";
        var names = FileNames(artworks);

        return new ImageExportPart(
            type,
            series,
            typeFolder,
            seriesFolder,
            [
                .. artworks
                    .OrderBy(artwork => names[artwork], ImportNames.Comparer)
                    .SelectMany(artwork => artwork.Images.Select((image, index) =>
                        new ImageExportEntry($"{folder}/{ImageFileName(names[artwork], index)}", artwork, image)
                    )),
            ]
        );
    }

    // The bulk upload finds an artwork by its title, so each artwork's files are named after it.
    // The slug instead when the title can't be a file name, or when any of its files would share a
    // name (ignoring case, as Windows and macOS do) with another artwork's in the folder, which also
    // catches "Dawn (2)" the title against the second image of "Dawn". Slugs are unique and have no
    // spaces or brackets, so an artwork moved to its slug can't collide again
    private static Dictionary<Artwork, string> FileNames(List<Artwork> artworks)
    {
        var usesSlug = artworks
            .Where(artwork => !artwork.Images.Select((_, index) => ImageFileName(artwork.Name.Value, index))
                .All(name => ExportFileNames.IsPortable(name + ExportImageFormat.LongestExtension)))
            .ToHashSet();

        while (true)
        {
            var names = artworks.ToDictionary(artwork => artwork, artwork => usesSlug.Contains(artwork) ? artwork.Slug.Value : artwork.Name.Value);
            var colliding = artworks
                .SelectMany(artwork => artwork.Images.Select((_, index) => (Artwork: artwork, Name: ImageFileName(names[artwork], index))))
                .GroupBy(file => file.Name, ImportNames.Comparer)
                .Where(files => files.Select(file => file.Artwork).Distinct().Count() > 1)
                .SelectMany(files => files.Select(file => file.Artwork))
                .Where(artwork => !usesSlug.Contains(artwork))
                .ToList();

            if (colliding.Count == 0)
            {
                return names;
            }

            usesSlug.UnionWith(colliding);
        }
    }

    // "Dawn", then "Dawn (2)" for the artwork's second image
    private static string ImageFileName(string name, int index) => index == 0 ? name : $"{name} ({index + 1})";
}
