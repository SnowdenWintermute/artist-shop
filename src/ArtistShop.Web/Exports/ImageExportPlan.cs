using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Exports;

// one image of a work, at its path inside the download's folder, without the extension its bytes decide
public record ImageExportEntry(string PathWithoutExtension, Work Work, WorkImage Image);

// One image download: the originals of one work type's works in one collection, or in none.
// The folders are Type/Collection/, the layout the bulk image upload reads when given the Type folder
public record ImageExportPart(
    WorkType Type,
    Collection? Collection,
    string TypeFolder,
    string? CollectionFolder,
    IReadOnlyList<ImageExportEntry> Entries
);

public static class ImageExportPlan
{
    // Every part that has images. A work in several collections goes in its first, since the bulk
    // upload attaches an image to one work. Types, then collections, by name, with no collection last
    public static List<ImageExportPart> Parts(IEnumerable<Work> works) =>
        [
            .. works
                .Where(work => work.Images.Count > 0)
                .GroupBy(work => (work.Type, Collection: work.Collections.FirstOrDefault()))
                .OrderBy(group => group.Key.Type.Name.Value, ImportNames.Comparer)
                .ThenBy(group => group.Key.Collection is null)
                .ThenBy(group => group.Key.Collection?.Name.Value, ImportNames.Comparer)
                .Select(group => Part(group.Key.Type, group.Key.Collection, [.. group])),
        ];

    private static string TypeFolder(WorkType type) =>
        ExportFileNames.IsPortable(type.Name.Value) ? type.Name.Value : $"work-type-{type.Id.Value}";

    private static ImageExportPart Part(WorkType type, Collection? collection, List<Work> works)
    {
        var typeFolder = TypeFolder(type);
        // A slug can't match another collection's name, even ignoring case: that name's slug would be
        // the same slug, which unique_collections_slug refuses
        string? collectionFolder = collection is null ? null
            : ExportFileNames.IsPortable(collection.Name.Value) ? collection.Name.Value
            : collection.Slug.Value;
        var folder = collectionFolder is null ? typeFolder : $"{typeFolder}/{collectionFolder}";
        var names = FileNames(works);

        return new ImageExportPart(
            type,
            collection,
            typeFolder,
            collectionFolder,
            [
                .. works
                    .OrderBy(work => names[work], ImportNames.Comparer)
                    .SelectMany(work => work.Images.Select((image, index) =>
                        new ImageExportEntry($"{folder}/{ImageFileName(names[work], index)}", work, image)
                    )),
            ]
        );
    }

    // The bulk upload finds a work by its title, so each work's files are named after it.
    // The slug instead when the title can't be a file name, or when any of its files would share a
    // name (ignoring case, as Windows and macOS do) with another work's in the folder, which also
    // catches "Dawn (2)" the title against the second image of "Dawn". Slugs are unique and have no
    // spaces or brackets, so a work moved to its slug can't collide again
    private static Dictionary<Work, string> FileNames(List<Work> works)
    {
        var usesSlug = works
            .Where(work => !work.Images.Select((_, index) => ImageFileName(work.Name.Value, index))
                .All(name => ExportFileNames.IsPortable(name + ExportImageFormat.LongestExtension)))
            .ToHashSet();

        while (true)
        {
            var names = works.ToDictionary(work => work, work => usesSlug.Contains(work) ? work.Slug.Value : work.Name.Value);
            var colliding = works
                .SelectMany(work => work.Images.Select((_, index) => (Work: work, Name: ImageFileName(names[work], index))))
                .GroupBy(file => file.Name, ImportNames.Comparer)
                .Where(files => files.Select(file => file.Work).Distinct().Count() > 1)
                .SelectMany(files => files.Select(file => file.Work))
                .Where(work => !usesSlug.Contains(work))
                .ToList();

            if (colliding.Count == 0)
            {
                return names;
            }

            usesSlug.UnionWith(colliding);
        }
    }

    // "Dawn", then "Dawn (2)" for the work's second image
    private static string ImageFileName(string name, int index) => index == 0 ? name : $"{name} ({index + 1})";
}
