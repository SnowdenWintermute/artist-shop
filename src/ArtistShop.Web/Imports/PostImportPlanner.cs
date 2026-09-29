using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Imports;

// One post's folder from the post download: its post.json's text, and each other file in it by
// name, as the id the page's script knows the file by
public record PostImportFolder(string Name, string Json, IReadOnlyDictionary<string, string> FileIdsByName);

// What a post's artwork picture says about its artwork on the website it came from
public record PostArtworkReference(string? Slug, string? Title, string Sha256);

// the artwork a post's artwork picture links to here, and which of its images it shows
public record PostArtworkLink(ArtworkId ArtworkId, string StorageKey);

// Where a post's artwork pictures find their artworks, and which artwork pages its links can reach.
// The post import asks the website's own artworks; the whole-website review answers for the
// artworks it would add as well
public interface IPostImportArtworks
{
    IReadOnlySet<string> Slugs { get; }

    PostArtworkLink? Find(PostArtworkReference reference);
}

// The website's artworks. Sha256ByStorageKey holds the hash of each image original
// ArtworkImagesToHash asked for
public sealed class WebsiteArtworks(IReadOnlyList<Artwork> artworks, IReadOnlyDictionary<string, string> sha256ByStorageKey)
    : IPostImportArtworks
{
    public IReadOnlySet<string> Slugs { get; } = artworks.Select(artwork => artwork.Slug.Value).ToHashSet();

    // the artwork with the same slug, or failing that the same title, that has an image whose
    // original hashes the same
    public PostArtworkLink? Find(PostArtworkReference reference)
    {
        foreach (var artwork in PostImportPlanner.Candidates(reference.Slug, reference.Title, artworks))
        {
            foreach (var image in artwork.Images)
            {
                if (
                    sha256ByStorageKey.TryGetValue(image.StorageKey, out var imageSha256)
                    && string.Equals(imageSha256, reference.Sha256, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return new PostArtworkLink(artwork.Id, image.StorageKey);
                }
            }
        }

        return null;
    }
}

// The website being imported into
public record PostImportTarget(IReadOnlySet<string> PostSlugs, IReadOnlySet<string> SeriesSlugs, IPostImportArtworks Artworks)
{
    // Hashes only the originals of artworks a post may show, rather than every image on the website
    public static async Task<PostImportTarget> LoadAsync(
        IReadOnlyList<PostImportFolder> folders,
        PostRepository posts,
        SeriesRepository series,
        ArtworkRepository artworkRepository,
        ArtworkImageRepository artworkImageRepository,
        ImageStorage imageStorage
    )
    {
        var artworks = await artworkRepository.GetAllAsync();
        var sha256ByStorageKey = await ImageHashes.LoadAsync(
            PostImportPlanner.ArtworkImagesToHash(folders, artworks),
            artworkImageRepository,
            imageStorage
        );

        return new PostImportTarget(
            (await posts.GetAllAsync()).Select(post => post.Slug.Value).ToHashSet(),
            (await series.GetAllAsync()).Select(oneSeries => oneSeries.Slug.Value).ToHashSet(),
            new WebsiteArtworks(artworks, sha256ByStorageKey)
        );
    }
}

public enum PostImportOutcome
{
    WillAdd,
    Skipped,
    Refused,
}

// Document is the post as it will be saved, but for its uploaded images, whose storage keys are
// placeholders until their files are uploaded: FileIdsByPlaceholder says which file each one is.
// Problems say why a post is skipped or refused. BrokenLinks are links to this website's pages
// that don't exist here, which don't stop the import. PictureOnlyArtworks are the titles of
// artworks the post shows that aren't on this website with the same image, so the post shows
// the picture without linking to an artwork
public record PostImportItem(
    string FolderName,
    PostTitle? Title,
    PostSlug? Slug,
    DateTimeOffset? PublishedAt,
    PostDocument? Document,
    IReadOnlyDictionary<string, string> FileIdsByPlaceholder,
    PostImportOutcome Outcome,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> BrokenLinks,
    IReadOnlyList<string> PictureOnlyArtworks,
    int RelinkedArtworkCount
);

// Turns the folders of a post download into the posts importing them would add, without touching
// the database or the files. Nothing the file says about this website is trusted: an uploaded
// image is always a new upload of a file in its folder, and an artwork embed is only linked to an
// artwork found here with the same image. Everything else goes through PostDocumentParser, as a
// save does
public static class PostImportPlanner
{
    // Checked first, so the review can say what it found: the storage keys of the images whose
    // originals need hashing, those of every artwork a post's artwork embed may be
    public static IReadOnlyList<string> ArtworkImagesToHash(IEnumerable<PostImportFolder> folders, IReadOnlyList<Artwork> artworks)
    {
        var embeds = folders
            .Select(folder => TryRead(folder.Json, out var file, out _) ? file : null)
            .OfType<PostFile>()
            .SelectMany(file => ArtworkEmbedValues(file.Ops))
            .ToList();

        return
        [
            .. embeds
                .SelectMany(embed => Candidates(StringOf(embed[PostExportJson.ArtworkSlugProperty]), StringOf(embed[PostExportJson.ArtworkTitleProperty]), artworks))
                .SelectMany(artwork => artwork.Images)
                .Select(image => image.StorageKey)
                .Distinct(),
        ];
    }

    public static IReadOnlyList<PostImportItem> Plan(IEnumerable<PostImportFolder> folders, PostImportTarget target)
    {
        var slugsInImport = new HashSet<string>();
        var items = new List<PostImportItem>();

        foreach (var folder in folders.OrderBy(folder => folder.Name, StringComparer.Ordinal))
        {
            var item = PlanOne(folder, target, slugsInImport);

            if (item.Outcome is PostImportOutcome.WillAdd && item.Slug is { } slug)
            {
                slugsInImport.Add(slug.Value);
            }

            items.Add(item);
        }

        // a link to another post in the same import works once both are in
        return [.. items.Select(item => item with { BrokenLinks = BrokenLinks(item.Document, target, slugsInImport) })];
    }

    // The post as it's saved: each placeholder swapped for its file's upload. Parsed and written
    // again, so what the browser reported about the uploads goes through the parser like
    // everything else
    public static PostBody Finish(PostDocument document, IReadOnlyDictionary<string, ImageUploadResult> uploadsByPlaceholder)
    {
        var finished = document with
        {
            Blocks =
            [
                .. document.Blocks.Select(block =>
                    block is PostImageEmbedBlock image && uploadsByPlaceholder.TryGetValue(image.StorageKey, out var upload)
                        ? image with
                        {
                            StorageKey = upload.StorageKey,
                            Width = upload.Width,
                            Height = upload.Height,
                            BlurDataUri = upload.BlurDataUri,
                        }
                        : block
                ),
            ],
        };

        return PostDeltaWriter.Write(PostDocumentParser.Parse(PostDeltaWriter.Write(finished)));
    }

    private static PostImportItem PlanOne(PostImportFolder folder, PostImportTarget target, HashSet<string> slugsInImport)
    {
        if (!TryRead(folder.Json, out var file, out var readProblem))
        {
            return Refused(folder, title: null, [readProblem]);
        }

        var title = file.Title.Trim();
        var slug = PostSlug.FromTitle(title);
        var problems = new List<string>();

        if (slug.Value.Length is 0)
        {
            problems.Add("The title has no letters or numbers to build a web address from.");
        }
        else if (title.Length > ArtistShopLimits.PostTitleMaximumLength)
        {
            problems.Add($"The title is longer than {ArtistShopLimits.PostTitleMaximumLength} characters.");
        }

        var localized = Localize(file.Ops, folder, target, problems);
        var body = new PostBody(new JsonObject { ["ops"] = localized.Ops }.ToJsonString());

        problems.AddRange(PostDocumentParser.LinksDropped(body).Select(PostDocumentParser.DroppedLinkProblem));

        if (problems.Count > 0)
        {
            return Refused(folder, new PostTitle(title), problems);
        }

        var skippedBecause =
            target.PostSlugs.Contains(slug.Value) ? "A post with this title is already on this website."
            : slugsInImport.Contains(slug.Value) ? "Another post in this folder has the same title, or one that only differs in punctuation, accents or capital letters."
            : null;

        return new PostImportItem(
            folder.Name,
            new PostTitle(title),
            slug,
            file.PublishedAt,
            PostDocumentParser.Parse(body),
            localized.FileIdsByPlaceholder,
            skippedBecause is null ? PostImportOutcome.WillAdd : PostImportOutcome.Skipped,
            skippedBecause is null ? [] : [skippedBecause],
            BrokenLinks: [],
            localized.PictureOnlyArtworks,
            localized.RelinkedArtworkCount
        );
    }

    private static PostImportItem Refused(PostImportFolder folder, PostTitle? title, IReadOnlyList<string> problems) =>
        new(
            folder.Name,
            title,
            Slug: null,
            PublishedAt: null,
            Document: null,
            new Dictionary<string, string>(),
            PostImportOutcome.Refused,
            problems,
            BrokenLinks: [],
            PictureOnlyArtworks: [],
            RelinkedArtworkCount: 0
        );

    private record PostFile(string Title, DateTimeOffset? PublishedAt, JsonArray Ops);

    private static bool TryRead(string json, [NotNullWhen(true)] out PostFile? file, out string problem)
    {
        file = null;
        problem = "";
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            problem = $"Its {PostExportJson.FileName} isn't readable.";
            return false;
        }

        if (root is not JsonObject post)
        {
            problem = $"Its {PostExportJson.FileName} isn't readable.";
            return false;
        }

        var version = IntOf(post[PostExportJson.FormatVersionProperty]);

        if (version != PostExportJson.FormatVersion)
        {
            problem = version > PostExportJson.FormatVersion
                ? $"Its {PostExportJson.FileName} is from a newer version of the post download than this website reads."
                : $"Its {PostExportJson.FileName} doesn't say which version of the post download it's from.";
            return false;
        }

        if (StringOf(post[PostExportJson.TitleProperty]) is not { } title)
        {
            problem = "It has no title.";
            return false;
        }

        DateTimeOffset? publishedAt = null;

        if (post[PostExportJson.PublishedAtProperty] is { } publishedAtNode)
        {
            if (
                StringOf(publishedAtNode) is not { } text
                || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            )
            {
                problem = "Its publish date isn't readable.";
                return false;
            }

            publishedAt = parsed.ToUniversalTime();
        }

        if (post[PostExportJson.BodyProperty] is not JsonObject body || body["ops"] is not JsonArray ops)
        {
            problem = "It has no text.";
            return false;
        }

        file = new PostFile(title, publishedAt, ops);
        return true;
    }

    private record Localized(
        JsonArray Ops,
        IReadOnlyDictionary<string, string> FileIdsByPlaceholder,
        IReadOnlyList<string> PictureOnlyArtworks,
        int RelinkedArtworkCount
    );

    // The file's ops with every embed rebuilt from the parts that are safe to take from it. An
    // uploaded image, and an artwork not found here, becomes an image embed whose storage key is a
    // placeholder for its file. Each file gets one placeholder however often the post shows it
    private static Localized Localize(JsonArray ops, PostImportFolder folder, PostImportTarget target, List<string> problems)
    {
        var localized = new JsonArray();
        var placeholdersByFileName = new Dictionary<string, string>();
        var fileIdsByPlaceholder = new Dictionary<string, string>();
        var pictureOnly = new List<string>();
        var relinked = 0;

        string? PlaceholderFor(JsonObject value)
        {
            if (StringOf(value[PostExportJson.FileProperty]) is not { } fileName)
            {
                problems.Add("An image doesn't say which file it is.");
                return null;
            }

            if (!folder.FileIdsByName.TryGetValue(fileName, out var fileId))
            {
                problems.Add($"The image {fileName} isn't in its folder.");
                return null;
            }

            if (!placeholdersByFileName.TryGetValue(fileName, out var placeholder))
            {
                // 32 hex digits, the shape the parser takes as a storage key; none is ever saved
                placeholder = (placeholdersByFileName.Count + 1).ToString("x32", CultureInfo.InvariantCulture);
                placeholdersByFileName[fileName] = placeholder;
                fileIdsByPlaceholder[placeholder] = fileId;
            }

            return placeholder;
        }

        foreach (var op in ops)
        {
            if (op is not JsonObject { } opObject || opObject["insert"] is not JsonObject insert)
            {
                // text, which the parser reads as a save does
                localized.Add(op?.DeepClone());
                continue;
            }

            if (insert[PostDocumentParser.PostImageEmbedName] is JsonObject image)
            {
                if (PlaceholderFor(image) is { } placeholder)
                {
                    localized.Add(ImageOp(placeholder, image, alt: StringOf(image["alt"]) ?? ""));
                }
            }
            else if (insert[PostDocumentParser.ArtworkEmbedName] is JsonObject artwork)
            {
                if (Relink(artwork, target) is { } found)
                {
                    var value = CopyLook(artwork, new JsonObject { ["artworkId"] = found.ArtworkId.Value, ["storageKey"] = found.StorageKey });
                    localized.Add(new JsonObject { ["insert"] = new JsonObject { [PostDocumentParser.ArtworkEmbedName] = value } });
                    relinked++;
                }
                else if (PlaceholderFor(artwork) is { } placeholder)
                {
                    var artworkTitle = StringOf(artwork[PostExportJson.ArtworkTitleProperty]) ?? "";
                    localized.Add(ImageOp(placeholder, artwork, alt: artworkTitle));
                    pictureOnly.Add(artworkTitle);
                }
            }
            else if (insert[PostDocumentParser.VideoEmbedName] is JsonObject video)
            {
                localized.Add(new JsonObject { ["insert"] = new JsonObject { [PostDocumentParser.VideoEmbedName] = video.DeepClone() } });
            }
        }

        return new Localized(localized, fileIdsByPlaceholder, pictureOnly, relinked);
    }

    // Width and height are the upload's once it's done; 1 until then, the least the parser takes
    private static JsonObject ImageOp(string placeholder, JsonObject from, string alt)
    {
        var value = CopyLook(from, new JsonObject { ["storageKey"] = placeholder, ["width"] = 1, ["height"] = 1, ["alt"] = alt });

        if (from["lightbox"] is JsonValue lightbox && lightbox.GetValueKind() is JsonValueKind.True)
        {
            value["lightbox"] = true;
        }

        return new JsonObject { ["insert"] = new JsonObject { [PostDocumentParser.PostImageEmbedName] = value } };
    }

    private static readonly string[] LookProperties = ["size", "layout", "caption"];

    // the settings every image embed has, left for the parser to check as it would a save's
    private static JsonObject CopyLook(JsonObject from, JsonObject to)
    {
        foreach (var name in LookProperties)
        {
            if (StringOf(from[name]) is { } value)
            {
                to[name] = value;
            }
        }

        return to;
    }

    private static PostArtworkLink? Relink(JsonObject embed, PostImportTarget target) =>
        StringOf(embed[PostExportJson.ImageSha256Property]) is { } sha256
            ? target.Artworks.Find(
                new PostArtworkReference(
                    StringOf(embed[PostExportJson.ArtworkSlugProperty]),
                    StringOf(embed[PostExportJson.ArtworkTitleProperty]),
                    sha256
                )
            )
            : null;

    // the catalog import makes a slug from the title, so an artwork whose slug was taken here has
    // another one but still its title
    public static IEnumerable<Artwork> Candidates(string? slug, string? title, IReadOnlyList<Artwork> artworks) =>
        artworks
            .Where(artwork => artwork.Slug.Value == slug || artwork.Name.Value == title)
            .OrderByDescending(artwork => artwork.Slug.Value == slug);

    private static IEnumerable<JsonObject> ArtworkEmbedValues(JsonArray ops) =>
        ops.OfType<JsonObject>()
            .Select(op => op["insert"] is JsonObject insert ? insert[PostDocumentParser.ArtworkEmbedName] as JsonObject : null)
            .OfType<JsonObject>();

    // The links to this website's own pages that go nowhere here: a post, series or artwork that
    // isn't here, or a path that isn't one of the public pages
    private static IReadOnlyList<string> BrokenLinks(PostDocument? document, PostImportTarget target, IReadOnlySet<string> slugsInImport)
    {
        if (document is null)
        {
            return [];
        }


        bool Works(string link)
        {
            var path = link.Split('?', '#')[0];
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString).ToArray();

            return parts switch
            {
                [] or ["posts"] => true,
                ["posts", var slug] => target.PostSlugs.Contains(slug) || slugsInImport.Contains(slug),
                ["series", var slug] => target.SeriesSlugs.Contains(slug),
                ["artworks", var slug] => target.Artworks.Slugs.Contains(slug),
                _ => false,
            };
        }

        return
        [
            .. TextOf(document)
                .Select(run => run.Link)
                .OfType<string>()
                .Where(link => link.StartsWith('/') && !Works(link))
                .Distinct(),
        ];
    }

    private static IEnumerable<PostText> TextOf(PostDocument document) =>
        document.Blocks.SelectMany(block =>
            block switch
            {
                ParagraphBlock paragraph => paragraph.Text,
                HeadingBlock heading => heading.Text,
                BlockquoteBlock quote => quote.Text,
                ListBlock list => list.Items.SelectMany(item => item),
                _ => [],
            }
        );

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.String ? value.GetValue<string>() : null;

    private static int? IntOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() is JsonValueKind.Number && value.TryGetValue<int>(out var number) ? number : null;
}
