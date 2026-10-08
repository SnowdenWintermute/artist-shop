using System.Diagnostics.CodeAnalysis;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;
using static ArtistShop.Web.Exports.ImageExportArchive;

namespace ArtistShop.Web.Imports;

// The unzipped whole-website download as the page read it. A text is null when its file isn't
// there. ArtworkCsvsByFileName is each file in catalog/artworks/ by its name; ImageFileIdsByPath is
// each file in images/ by its path there, as the id the page's script knows it by
public record WebsiteImportFolder(
    string? Manifest,
    string? ArtworkTypesCsv,
    string? VocabulariesCsv,
    IReadOnlyDictionary<string, string> ArtworkCsvsByFileName,
    string? ImageList,
    IReadOnlyDictionary<string, string> ImageFileIdsByPath,
    IReadOnlyList<PostImportFolder> Posts
);

// The website being imported into. Sha256ByStorageKey holds the hash of each original
// WebsiteImportPlanner.ImagesToHash asked for
public record WebsiteImportTarget(
    CatalogSetupSnapshot Setup,
    IReadOnlyList<Series> Series,
    IReadOnlyList<ProductType> ProductTypes,
    IReadOnlyList<Artwork> Artworks,
    IReadOnlyDictionary<string, string> Sha256ByStorageKey,
    IReadOnlySet<string> PostSlugs
)
{
    // Hashes only the originals of artworks the download has images or post pictures for, and
    // only those saved without a stored hash
    public static async Task<WebsiteImportTarget> LoadAsync(
        WebsiteImportFolder folder,
        ArtworkFieldRepository artworkFieldRepository,
        ArtworkTypeRepository artworkTypeRepository,
        VocabularyRepository vocabularyRepository,
        SeriesRepository seriesRepository,
        ProductTypeRepository productTypeRepository,
        ArtworkRepository artworkRepository,
        ArtworkImageRepository artworkImageRepository,
        PostRepository postRepository,
        ImageStorage imageStorage
    )
    {
        var artworks = await artworkRepository.GetAllAsync();
        var sha256ByStorageKey = await ImageHashes.LoadAsync(
            WebsiteImportPlanner.ImagesToHash(folder, artworks),
            artworkImageRepository,
            imageStorage
        );

        return new WebsiteImportTarget(
            await CatalogSetupSnapshot.LoadAsync(artworkFieldRepository, artworkTypeRepository, vocabularyRepository),
            await seriesRepository.GetAllAsync(),
            await productTypeRepository.GetAllAsync(),
            artworks,
            sha256ByStorageKey,
            (await postRepository.GetAllAsync()).Select(post => post.Slug.Value).ToHashSet()
        );
    }
}

// one file in catalog/artworks/: the type its rows are, and what importing it would do
public record WebsiteImportArtworkFile(string FileName, ArtworkTypeName TypeName, ArtworkImportPlan Plan);

// an image the import adds: its path in images/, the id the page's script knows it by, and its hash
public record WebsiteImportImage(string Path, string FileId, string Sha256);

// One artwork's images: those the import adds, in images.csv's order, and how many it has already.
// ArtworkId is a placeholder for an artwork the review would add
public record WebsiteImportArtworkImages(
    ArtworkId ArtworkId,
    ArtworkTypeName TypeName,
    ArtworkName Title,
    IReadOnlyList<WebsiteImportImage> ToAdd,
    int AlreadyThereCount
);

// Errors are images.csv's own. WithoutArtwork are listed files whose artwork won't be on this
// website; MissingFiles are listed but not in the folder; UnlistedFiles are in the folder but not
// listed. None of them stops the import
public record WebsiteImportImagesPlan(
    IReadOnlyList<WebsiteImportArtworkImages> Artworks,
    IReadOnlyList<ImportError> Errors,
    IReadOnlyList<string> WithoutArtwork,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> UnlistedFiles
)
{
    public static readonly WebsiteImportImagesPlan Empty = new([], [], [], [], []);

    public int ToAddCount => Artworks.Sum(artwork => artwork.ToAdd.Count);
}

// What importing the folder would do, stage by stage. Problems are with the folder as a whole, and
// when there are any nothing else is planned. Each stage is planned as though the ones before it
// had run, with placeholder ids for what they'd add; the import plans each stage again against the
// website once the one before has run, so this is a preview
public record WebsiteImportReview(
    IReadOnlyList<string> Problems,
    ArtworkTypeImportPlan ArtworkTypes,
    VocabularyImportPlan Vocabularies,
    IReadOnlyList<WebsiteImportArtworkFile> ArtworkFiles,
    WebsiteImportImagesPlan Images,
    IReadOnlyList<PostImportItem> Posts
)
{
    // the catalog's problems stop the import, as on the single imports; images and posts that
    // can't be imported are left out instead
    public bool CanImport =>
        Problems.Count == 0
        && ArtworkTypes.Errors.Count == 0
        && Vocabularies.Errors.Count == 0
        && ArtworkFiles.All(file => file.Plan.Errors.Count == 0);

    public static WebsiteImportReview Refused(IReadOnlyList<string> problems) =>
        new(problems, ArtworkTypeImportPlan.WithErrors([]), VocabularyImportPlan.WithErrors([]), [], WebsiteImportImagesPlan.Empty, []);
}

public static class WebsiteImportPlanner
{
    private static readonly string[] ImageListColumns =
    [
        ImageListHeaders.File,
        ImageListHeaders.ArtworkType,
        ImageListHeaders.Title,
        ImageListHeaders.Slug,
        ImageListHeaders.Sha256,
    ];

    private static readonly string[] ImageListRequiredColumns =
    [
        ImageListHeaders.File,
        ImageListHeaders.ArtworkType,
        ImageListHeaders.Title,
        ImageListHeaders.Sha256,
    ];

    // Checked first, so the review can say which images are already here: the storage keys of the
    // images of every artwork images.csv or a post's picture may be
    public static IReadOnlyList<string> ImagesToHash(WebsiteImportFolder folder, IReadOnlyList<Artwork> artworks)
    {
        var named = ReadImageList(folder.ImageList, out _)
            .Select(row => ArtworkKey(row.ArtworkType, row.Title))
            .ToHashSet(ImportNames.Comparer);

        return
        [
            .. artworks
                .Where(artwork => named.Contains(ArtworkKey(artwork.Type.Name.Value, artwork.Name.Value)))
                .SelectMany(artwork => artwork.Images)
                .Select(image => image.StorageKey)
                .Concat(PostImportPlanner.ArtworkImagesToHash(folder.Posts, artworks))
                .Distinct(),
        ];
    }

    // The shape the catalog download writes: centimetres, and one product per artwork, of the
    // website's default product type. There's at least one product type
    public static ArtworkImportSettings ArtworkSettings(IReadOnlyList<ProductType> productTypes, char listSeparator) =>
        new(
            LengthUnit.Centimeters,
            (productTypes.FirstOrDefault(productType => productType.IsDefault) ?? productTypes[0]).Id,
            IsOneOfAKind: true,
            listSeparator
        );

    // What's wrong with the folder as a whole, which stops everything: website.json unreadable, a
    // file the import reads missing, or no product type for the artworks' products. The review
    // and the import both check, so the import never reads a file that isn't there
    public static bool TryCheckFolder(
        WebsiteImportFolder folder,
        IReadOnlyList<ProductType> productTypes,
        [NotNullWhen(true)] out WebsiteImportManifest? manifest,
        out List<string> problems
    )
    {
        problems = [];

        if (!WebsiteImportManifest.TryRead(folder.Manifest, out manifest, out var manifestProblem))
        {
            problems.Add(manifestProblem);
            return false;
        }

        if (folder.ArtworkTypesCsv is null)
        {
            problems.Add($"{WebsiteExportArchive.CatalogFolder}/{CatalogExportArchive.ArtworkTypesFileName} is missing.");
        }

        if (folder.VocabulariesCsv is null)
        {
            problems.Add($"{WebsiteExportArchive.CatalogFolder}/{CatalogExportArchive.VocabulariesFileName} is missing.");
        }

        foreach (var file in manifest.ArtworkFiles.Where(file => !folder.ArtworkCsvsByFileName.ContainsKey(file.FileName)))
        {
            problems.Add($"{WebsiteExportArchive.CatalogFolder}/{CatalogExportArchive.ArtworksFolder}/{file.FileName} is missing.");
        }

        if (folder.ImageList is null)
        {
            problems.Add($"{WebsiteExportArchive.ImagesFolder}/{ImageListFileName} is missing.");
        }

        if (productTypes.Count == 0)
        {
            problems.Add("This website has no product types for the artworks' products.");
        }

        if (problems.Count > 0)
        {
            manifest = null;
            return false;
        }

        return true;
    }

    public static WebsiteImportReview Plan(WebsiteImportFolder folder, WebsiteImportTarget target)
    {
        if (!TryCheckFolder(folder, target.ProductTypes, out var manifest, out var problems))
        {
            return WebsiteImportReview.Refused(problems);
        }

        var types = ArtworkTypeImportPlanner.Plan(Unwrap.Value(folder.ArtworkTypesCsv), manifest.ListSeparator, target.Setup);
        var setupWithTypes = target.Setup with
        {
            Types =
            [
                .. target.Setup.Types,
                .. types.Additions.Select((addition, index) => new ArtworkTypeWithFields(new ArtworkTypeId(-1 - index), addition.Name, addition.Fields)),
            ],
        };

        var vocabularies = VocabularyImportPlanner.Plan(Unwrap.Value(folder.VocabulariesCsv), manifest.ListSeparator, setupWithTypes);
        var setup = setupWithTypes with { Vocabularies = WithChanges(setupWithTypes.Vocabularies, vocabularies) };

        var (artworkFiles, afterImport, series) = PlanArtworks(folder, manifest, setup, target);
        var images = PlanImages(folder, target, afterImport);

        var posts = PostImportPlanner.Plan(
            folder.Posts,
            new PostImportTarget(
                target.PostSlugs,
                series.Select(oneSeries => oneSeries.Slug.Value).ToHashSet(),
                new ArtworksAfterImport(target, afterImport, images)
            )
        );

        return new WebsiteImportReview([], types, vocabularies, artworkFiles, images, posts);
    }

    // the vocabularies once the plan's changes are made, new ones and new terms with placeholder ids
    private static List<VocabularySetup> WithChanges(IReadOnlyList<VocabularySetup> vocabularies, VocabularyImportPlan plan)
    {
        var changed = vocabularies.ToList();
        var nextVocabularyId = -1;
        var nextTermId = -1;

        foreach (var change in plan.Changes)
        {
            var id = change.ExistingId ?? new VocabularyId(nextVocabularyId--);
            var index = changed.FindIndex(vocabulary => vocabulary.Id == id);
            var existing = index >= 0 ? changed[index] : new VocabularySetup(id, change.Name, change.IsSingleChoice, [], []);

            var updated = existing with
            {
                ArtworkTypeIds = [.. existing.ArtworkTypeIds, .. change.AddedArtworkTypes.Select(type => type.Id)],
                Terms = [.. existing.Terms, .. change.AddedTerms.Select(term => new VocabularyTerm(new VocabularyTermId(nextTermId--), term, id, existing.Name))],
            };

            if (index >= 0)
            {
                changed[index] = updated;
            }
            else
            {
                changed.Add(updated);
            }
        }

        return changed;
    }

    // An artwork as it would be once the artworks are imported: one here already (Existing), or one
    // a file adds, with a placeholder id
    private record ArtworkAfterImport(ArtworkId Id, ArtworkTypeName TypeName, ArtworkName Title, ArtworkSlug Slug, Artwork? Existing);

    // Each file in the manifest's order, against the catalog as the files before it would leave it:
    // a series one file creates isn't new again for the next. Returns every artwork that would be
    // here after, by ArtworkKey (a title can be on several artworks of a type), and every series
    private static (List<WebsiteImportArtworkFile> Files, Dictionary<string, List<ArtworkAfterImport>> AfterImport, List<Series> Series) PlanArtworks(
        WebsiteImportFolder folder,
        WebsiteImportManifest manifest,
        CatalogSetupSnapshot setup,
        WebsiteImportTarget target
    )
    {
        var settings = ArtworkSettings(target.ProductTypes, manifest.ListSeparator);
        var series = target.Series.ToList();
        var afterImport = new Dictionary<string, List<ArtworkAfterImport>>(ImportNames.Comparer);
        var files = new List<WebsiteImportArtworkFile>();
        var nextArtworkId = -1;
        var nextSeriesId = -1;

        void Add(ArtworkAfterImport artwork)
        {
            var key = ArtworkKey(artwork.TypeName.Value, artwork.Title.Value);

            if (afterImport.TryGetValue(key, out var sameTitle))
            {
                sameTitle.Add(artwork);
            }
            else
            {
                afterImport[key] = [artwork];
            }
        }

        foreach (var artwork in target.Artworks)
        {
            Add(new ArtworkAfterImport(artwork.Id, artwork.Type.Name, artwork.Name, artwork.Slug, artwork));
        }

        foreach (var (fileName, typeName) in manifest.ArtworkFiles)
        {
            var type = setup.Types.FirstOrDefault(type => ImportNames.Comparer.Equals(type.Name.Value, typeName));

            if (type is null)
            {
                files.Add(
                    new WebsiteImportArtworkFile(
                        fileName,
                        new ArtworkTypeName(typeName),
                        ArtworkImportPlan.WithErrors(
                            [new ImportError(1, null, $"\"{typeName}\" isn't an artwork type here, and {CatalogExportArchive.ArtworkTypesFileName} doesn't add it.")]
                        )
                    )
                );
                continue;
            }

            var snapshot = new ArtworkImportCatalogSnapshot(
                type,
                [.. setup.VocabulariesFor(type.Id).Select(vocabulary => new VocabularyWithTerms(vocabulary.Id, vocabulary.Name, vocabulary.IsSingleChoice, vocabulary.Terms))],
                [.. setup.Vocabularies.Select(vocabulary => new Vocabulary(vocabulary.Id, vocabulary.Name))],
                [.. series],
                target.ProductTypes,
                [
                    .. target.Artworks
                        .Where(artwork => artwork.Type.Id == type.Id)
                        .Select(artwork => new ArtworkTitleAndSlug(artwork.Name.Value, artwork.Slug.Value)),
                ]
            );
            var plan = ArtworkImportPlanner.Plan(folder.ArtworkCsvsByFileName[fileName], settings, snapshot);
            files.Add(new WebsiteImportArtworkFile(fileName, type.Name, plan));

            series.AddRange(
                plan.NewSeries.Select(newSeries =>
                    new Series(new SeriesId(nextSeriesId--), new SeriesName(newSeries.Name), SeriesSlug.FromName(newSeries.Name))
                )
            );

            // a plan with errors imports nothing
            if (plan.Errors.Count > 0)
            {
                continue;
            }

            foreach (var addition in plan.Additions.Select(addition => addition.Addition))
            {
                Add(new ArtworkAfterImport(new ArtworkId(nextArtworkId--), type.Name, addition.Name, addition.CandidateSlug, Existing: null));
            }
        }

        return (files, afterImport, series);
    }

    private static WebsiteImportImagesPlan PlanImages(
        WebsiteImportFolder folder,
        WebsiteImportTarget target,
        Dictionary<string, List<ArtworkAfterImport>> afterImport
    )
    {
        var rows = ReadImageList(folder.ImageList, out var errors);
        var byArtwork = new Dictionary<ArtworkAfterImport, (List<WebsiteImportImage> ToAdd, int AlreadyThere)>();
        var withoutArtwork = new List<string>();
        var missingFiles = new List<string>();

        // the download's own slugs for each title, to tell apart two artworks of a type sharing one
        var slugsByKey = rows
            .GroupBy(row => ArtworkKey(row.ArtworkType, row.Title), ImportNames.Comparer)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Slug).Distinct().Count(), ImportNames.Comparer);

        foreach (var row in rows)
        {
            if (!folder.ImageFileIdsByPath.TryGetValue(row.File, out var fileId))
            {
                missingFiles.Add(row.File);
                continue;
            }

            if (ArtworkFor(row, slugsByKey[ArtworkKey(row.ArtworkType, row.Title)], afterImport) is not { } artwork)
            {
                withoutArtwork.Add(row.File);
                continue;
            }

            var (toAdd, alreadyThere) = byArtwork.GetValueOrDefault(artwork, ([], 0));
            var hasIt = artwork.Existing?.Images.Any(image =>
                target.Sha256ByStorageKey.TryGetValue(image.StorageKey, out var sha256) && sha256 == row.Sha256
            ) is true;

            if (hasIt)
            {
                alreadyThere++;
            }
            // the same image listed twice for one artwork is added once
            else if (toAdd.All(image => image.Sha256 != row.Sha256))
            {
                toAdd.Add(new WebsiteImportImage(row.File, fileId, row.Sha256));
            }

            byArtwork[artwork] = (toAdd, alreadyThere);
        }

        var listed = rows.Select(row => row.File).ToHashSet(StringComparer.Ordinal);

        return new WebsiteImportImagesPlan(
            [
                .. byArtwork.Select(entry =>
                    new WebsiteImportArtworkImages(entry.Key.Id, entry.Key.TypeName, entry.Key.Title, entry.Value.ToAdd, entry.Value.AlreadyThere)
                ),
            ],
            errors,
            withoutArtwork,
            missingFiles,
            [.. folder.ImageFileIdsByPath.Keys.Where(path => path != ImageListFileName && !listed.Contains(path)).Order(StringComparer.Ordinal)]
        );
    }

    // The artwork a row's image goes to: by type and title, since slugs can differ between websites.
    // When the title is on several artworks, of the download or here, only the one with the row's
    // slug; with none, nothing rather than a guess. The artwork import skips a repeated title, so
    // the second "Dawn" isn't imported and its images mustn't land on the first
    private static ArtworkAfterImport? ArtworkFor(
        ImageListRow row,
        int slugCountInDownload,
        Dictionary<string, List<ArtworkAfterImport>> afterImport
    )
    {
        if (!afterImport.TryGetValue(ArtworkKey(row.ArtworkType, row.Title), out var sameTitle))
        {
            return null;
        }

        return slugCountInDownload == 1 && sameTitle.Count == 1
            ? sameTitle[0]
            : sameTitle.FirstOrDefault(artwork => artwork.Slug.Value == row.Slug);
    }

    // What a post's artwork picture would link to once the import has run: an artwork here with the
    // same image, as the post import finds it, or else the artwork images.csv says the image is
    // for, if that artwork will be here. The link's storage key is a placeholder, since the image
    // has no key here until it's uploaded
    private sealed class ArtworksAfterImport(
        WebsiteImportTarget target,
        Dictionary<string, List<ArtworkAfterImport>> afterImport,
        WebsiteImportImagesPlan images
    ) : IPostImportArtworks
    {
        private readonly WebsiteArtworks _website = new(target.Artworks, target.Sha256ByStorageKey);

        private readonly Dictionary<string, ArtworkId> _artworkIdsBySha256 = images
            .Artworks.SelectMany(artwork => artwork.ToAdd.Select(image => (image.Sha256, Id: artwork.ArtworkId)))
            .DistinctBy(pair => pair.Sha256)
            .ToDictionary(pair => pair.Sha256, pair => pair.Id);

        public IReadOnlySet<string> Slugs { get; } = afterImport.Values.SelectMany(sameTitle => sameTitle).Select(artwork => artwork.Slug.Value).ToHashSet();

        public PostArtworkLink? Find(PostArtworkReference reference) =>
            _website.Find(reference)
            ?? (
                _artworkIdsBySha256.TryGetValue(reference.Sha256.ToLowerInvariant(), out var id)
                    ? new PostArtworkLink(id, new string('0', 32))
                    : null
            );
    }

    // Slug is empty when the row has none
    private record ImageListRow(string File, string ArtworkType, string Title, string Slug, string Sha256);

    // images.csv's rows; one missing a value, or naming a file an earlier row named, is an error
    // rather than a row
    private static List<ImageListRow> ReadImageList(string? csvText, out List<ImportError> errors)
    {
        errors = [];

        if (csvText is null)
        {
            return [];
        }

        CsvTable table;

        try
        {
            table = CsvTable.Parse(csvText);
        }
        catch (MalformedCsvException exception)
        {
            errors.Add(new ImportError(exception.RowNumber, null, exception.Problem));
            return [];
        }

        if (ImportColumns.Find(table, ImageListColumns, ImageListRequiredColumns, errors) is not { } columns)
        {
            return [];
        }

        var rows = new List<ImageListRow>();
        var files = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in table.Rows)
        {
            var cells = ImageListRequiredColumns.Select(header => row.Cells[columns[header]]).ToList();

            if (cells.Any(cell => cell.Length == 0))
            {
                errors.Add(new ImportError(row.RowNumber, null, "Every row needs a file, artwork type, title and sha256."));
                continue;
            }

            if (!files.Add(cells[0]))
            {
                errors.Add(new ImportError(row.RowNumber, ImageListHeaders.File, "An earlier row lists this file."));
                continue;
            }

            var slug = columns.TryGetValue(ImageListHeaders.Slug, out var slugColumn) ? row.Cells[slugColumn] : "";
            rows.Add(new ImageListRow(cells[0], cells[1], cells[2], slug, cells[3].ToLowerInvariant()));
        }

        return rows;
    }

    // a type's name and an artwork's title, as one key compared the way import names are
    private static string ArtworkKey(string typeName, string title) => $"{typeName}\u0000{title}";
}
