using System.Diagnostics.CodeAnalysis;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;
using static ArtistShop.Web.Exports.ImageExportArchive;

namespace ArtistShop.Web.Imports;

// The unzipped whole-website download as the page read it. A text is null when its file isn't
// there. WorkCsvsByFileName is each file in catalog/works/ by its name; ImageFileIdsByPath is
// each file in images/ by its path there, as the id the page's script knows it by
public record WebsiteImportFolder(
    string? Manifest,
    string? WorkTypesCsv,
    string? VocabulariesCsv,
    IReadOnlyDictionary<string, string> WorkCsvsByFileName,
    string? ImageList,
    IReadOnlyDictionary<string, string> ImageFileIdsByPath,
    IReadOnlyList<PostImportFolder> Posts
);

// The website being imported into. Sha256ByStorageKey holds the hash of each original
// WebsiteImportPlanner.ImagesToHash asked for
public record WebsiteImportTarget(
    CatalogSetupSnapshot Setup,
    IReadOnlyList<Collection> Collections,
    IReadOnlyList<ProductType> ProductTypes,
    IReadOnlyList<Work> Works,
    IReadOnlyDictionary<string, string> Sha256ByStorageKey,
    IReadOnlySet<string> PostSlugs,
    SiteWording Wording
)
{
    // Hashes only the originals of works the download has images or post pictures for, and
    // only those saved without a stored hash
    public static async Task<WebsiteImportTarget> LoadAsync(
        WebsiteImportFolder folder,
        WorkFieldRepository workFieldRepository,
        WorkTypeRepository workTypeRepository,
        VocabularyRepository vocabularyRepository,
        CollectionRepository collectionRepository,
        ProductTypeRepository productTypeRepository,
        WorkRepository workRepository,
        WorkImageRepository workImageRepository,
        PostRepository postRepository,
        WordingRepository wordingRepository,
        ImageStorage imageStorage
    )
    {
        var works = await workRepository.GetAllAsync();
        var sha256ByStorageKey = await ImageHashes.LoadAsync(
            WebsiteImportPlanner.ImagesToHash(folder, works),
            workImageRepository,
            imageStorage
        );

        return new WebsiteImportTarget(
            await CatalogSetupSnapshot.LoadAsync(workFieldRepository, workTypeRepository, vocabularyRepository),
            await collectionRepository.GetAllAsync(),
            await productTypeRepository.GetAllAsync(),
            works,
            sha256ByStorageKey,
            (await postRepository.GetAllAsync()).Select(post => post.Slug.Value).ToHashSet(),
            await wordingRepository.GetAsync()
        );
    }
}

// one file in catalog/works/: the type its rows are, and what importing it would do
public record WebsiteImportWorkFile(string FileName, WorkTypeName TypeName, WorkImportPlan Plan);

// an image the import adds: its path in images/, the id the page's script knows it by, and its hash
public record WebsiteImportImage(string Path, string FileId, string Sha256);

// One work's images: those the import adds, in images.csv's order, and how many it has already.
// WorkId is a placeholder for a work the review would add
public record WebsiteImportWorkImages(
    WorkId WorkId,
    WorkTypeName TypeName,
    WorkName Title,
    IReadOnlyList<WebsiteImportImage> ToAdd,
    int AlreadyThereCount
);

// Errors are images.csv's own. WithoutWork are listed files whose work won't be on this
// website; MissingFiles are listed but not in the folder; UnlistedFiles are in the folder but not
// listed. None of them stops the import
public record WebsiteImportImagesPlan(
    IReadOnlyList<WebsiteImportWorkImages> Works,
    IReadOnlyList<ImportError> Errors,
    IReadOnlyList<string> WithoutWork,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> UnlistedFiles
)
{
    public static readonly WebsiteImportImagesPlan Empty = new([], [], [], [], []);

    public int ToAddCount => Works.Sum(work => work.ToAdd.Count);
}

// What importing the folder would do, stage by stage. Problems are with the folder as a whole, and
// when there are any nothing else is planned. Each stage is planned as though the ones before it
// had run, with placeholder ids for what they'd add; the import plans each stage again against the
// website once the one before has run, so this is a preview
public record WebsiteImportReview(
    IReadOnlyList<string> Problems,
    WorkTypeImportPlan WorkTypes,
    VocabularyImportPlan Vocabularies,
    IReadOnlyList<WebsiteImportWorkFile> WorkFiles,
    WebsiteImportImagesPlan Images,
    IReadOnlyList<PostImportItem> Posts,
    WordingImportPlan Wording
)
{
    // the catalog's problems stop the import, as on the single imports; images and posts that
    // can't be imported are left out instead
    public bool CanImport =>
        Problems.Count == 0
        && WorkTypes.Errors.Count == 0
        && Vocabularies.Errors.Count == 0
        && WorkFiles.All(file => file.Plan.Errors.Count == 0);

    public static WebsiteImportReview Refused(IReadOnlyList<string> problems) =>
        new(
            problems,
            WorkTypeImportPlan.WithErrors([]),
            VocabularyImportPlan.WithErrors([]),
            [],
            WebsiteImportImagesPlan.Empty,
            [],
            new WordingImportPlan(SiteWording.Default, WordingImportOutcome.Same)
        );
}

public static class WebsiteImportPlanner
{
    private static readonly string[] ImageListColumns =
    [
        ImageListHeaders.File,
        ImageListHeaders.WorkType,
        ImageListHeaders.Title,
        ImageListHeaders.Slug,
        ImageListHeaders.Sha256,
    ];

    private static readonly string[] ImageListRequiredColumns =
    [
        ImageListHeaders.File,
        ImageListHeaders.WorkType,
        ImageListHeaders.Title,
        ImageListHeaders.Sha256,
    ];

    // Checked first, so the review can say which images are already here: the storage keys of the
    // images of every work images.csv or a post's picture may be
    public static IReadOnlyList<string> ImagesToHash(WebsiteImportFolder folder, IReadOnlyList<Work> works)
    {
        var named = ReadImageList(folder.ImageList, out _)
            .Select(row => WorkKey(row.WorkType, row.Title))
            .ToHashSet(ImportNames.Comparer);

        return
        [
            .. works
                .Where(work => named.Contains(WorkKey(work.Type.Name.Value, work.Name.Value)))
                .SelectMany(work => work.Images)
                .Select(image => image.StorageKey)
                .Concat(PostImportPlanner.WorkImagesToHash(folder.Posts, works))
                .Distinct(),
        ];
    }

    // The shape the catalog download writes: centimetres, and one product per work, of the
    // website's default product type. There's at least one product type
    public static WorkImportSettings WorkSettings(IReadOnlyList<ProductType> productTypes, char listSeparator) =>
        new(
            LengthUnit.Centimeters,
            (productTypes.FirstOrDefault(productType => productType.IsDefault) ?? productTypes[0]).Id,
            IsOneOfAKind: true,
            listSeparator
        );

    // What's wrong with the folder as a whole, which stops everything: website.json unreadable, a
    // file the import reads missing, or no product type for the works' products. The review
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

        if (folder.WorkTypesCsv is null)
        {
            problems.Add($"{WebsiteExportArchive.CatalogFolder}/{CatalogExportArchive.WorkTypesFileName} is missing.");
        }

        if (folder.VocabulariesCsv is null)
        {
            problems.Add($"{WebsiteExportArchive.CatalogFolder}/{CatalogExportArchive.VocabulariesFileName} is missing.");
        }

        foreach (var file in manifest.WorkFiles.Where(file => !folder.WorkCsvsByFileName.ContainsKey(file.FileName)))
        {
            problems.Add($"{WebsiteExportArchive.CatalogFolder}/{CatalogExportArchive.WorksFolder}/{file.FileName} is missing.");
        }

        if (folder.ImageList is null)
        {
            problems.Add($"{WebsiteExportArchive.ImagesFolder}/{ImageListFileName} is missing.");
        }

        if (productTypes.Count == 0)
        {
            problems.Add("This website has no product types for the works' products.");
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

        var types = WorkTypeImportPlanner.Plan(Unwrap.Value(folder.WorkTypesCsv), manifest.ListSeparator, target.Setup);
        var setupWithTypes = target.Setup with
        {
            Types =
            [
                .. target.Setup.Types,
                .. types.Additions.Select((addition, index) => new WorkTypeWithFields(new WorkTypeId(-1 - index), addition.Name, addition.Fields)),
            ],
        };

        var vocabularies = VocabularyImportPlanner.Plan(Unwrap.Value(folder.VocabulariesCsv), manifest.ListSeparator, setupWithTypes);
        var setup = setupWithTypes with { Vocabularies = WithChanges(setupWithTypes.Vocabularies, vocabularies) };

        var (workFiles, afterImport, collections) = PlanWorks(folder, manifest, setup, target);
        var images = PlanImages(folder, target, afterImport);

        var posts = PostImportPlanner.Plan(
            folder.Posts,
            new PostImportTarget(
                target.PostSlugs,
                collections.Select(collection => collection.Slug.Value).ToHashSet(),
                new WorksAfterImport(target, afterImport, images)
            )
        );

        return new WebsiteImportReview([], types, vocabularies, workFiles, images, posts, WordingImportPlanner.Plan(manifest.Wording, target.Wording));
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
            var existing = index >= 0 ? changed[index] : new VocabularySetup(id, change.Name, change.IsMutuallyExclusive, [], []);

            var updated = existing with
            {
                WorkTypeIds = [.. existing.WorkTypeIds, .. change.AddedWorkTypes.Select(type => type.Id)],
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

    // A work as it would be once the works are imported: one here already (Existing), or one
    // a file adds, with a placeholder id
    private record WorkAfterImport(WorkId Id, WorkTypeName TypeName, WorkName Title, WorkSlug Slug, Work? Existing);

    // Each file in the manifest's order, against the catalog as the files before it would leave it:
    // a collection one file creates isn't new again for the next. Returns every work that would be
    // here after, by WorkKey (a title can be on several works of a type), and every collection
    private static (List<WebsiteImportWorkFile> Files, Dictionary<string, List<WorkAfterImport>> AfterImport, List<Collection> Collections) PlanWorks(
        WebsiteImportFolder folder,
        WebsiteImportManifest manifest,
        CatalogSetupSnapshot setup,
        WebsiteImportTarget target
    )
    {
        var settings = WorkSettings(target.ProductTypes, manifest.ListSeparator);
        var collections = target.Collections.ToList();
        var afterImport = new Dictionary<string, List<WorkAfterImport>>(ImportNames.Comparer);
        var files = new List<WebsiteImportWorkFile>();
        var nextWorkId = -1;
        var nextCollectionId = -1;

        void Add(WorkAfterImport work)
        {
            var key = WorkKey(work.TypeName.Value, work.Title.Value);

            if (afterImport.TryGetValue(key, out var sameTitle))
            {
                sameTitle.Add(work);
            }
            else
            {
                afterImport[key] = [work];
            }
        }

        foreach (var work in target.Works)
        {
            Add(new WorkAfterImport(work.Id, work.Type.Name, work.Name, work.Slug, work));
        }

        foreach (var (fileName, typeName) in manifest.WorkFiles)
        {
            var type = setup.Types.FirstOrDefault(type => ImportNames.Comparer.Equals(type.Name.Value, typeName));

            if (type is null)
            {
                files.Add(
                    new WebsiteImportWorkFile(
                        fileName,
                        new WorkTypeName(typeName),
                        WorkImportPlan.WithErrors(
                            [new ImportError(1, null, $"\"{typeName}\" isn't a work type here, and {CatalogExportArchive.WorkTypesFileName} doesn't add it.")]
                        )
                    )
                );
                continue;
            }

            var snapshot = new WorkImportCatalogSnapshot(
                type,
                [.. setup.VocabulariesFor(type.Id).Select(vocabulary => new VocabularyWithTerms(vocabulary.Id, vocabulary.Name, vocabulary.IsMutuallyExclusive, vocabulary.Terms))],
                [.. setup.Vocabularies.Select(vocabulary => new Vocabulary(vocabulary.Id, vocabulary.Name))],
                [.. collections],
                target.ProductTypes,
                [
                    .. target.Works
                        .Where(work => work.Type.Id == type.Id)
                        .Select(work => new WorkTitleAndSlug(work.Name.Value, work.Slug.Value)),
                ]
            );
            var plan = WorkImportPlanner.Plan(folder.WorkCsvsByFileName[fileName], settings, snapshot);
            files.Add(new WebsiteImportWorkFile(fileName, type.Name, plan));

            collections.AddRange(
                plan.NewCollections.Select(newCollection =>
                    new Collection(new CollectionId(nextCollectionId--), new CollectionName(newCollection.Name), CollectionSlug.FromName(newCollection.Name))
                )
            );

            // a plan with errors imports nothing
            if (plan.Errors.Count > 0)
            {
                continue;
            }

            foreach (var addition in plan.Additions.Select(addition => addition.Addition))
            {
                Add(new WorkAfterImport(new WorkId(nextWorkId--), type.Name, addition.Name, addition.CandidateSlug, Existing: null));
            }
        }

        return (files, afterImport, collections);
    }

    private static WebsiteImportImagesPlan PlanImages(
        WebsiteImportFolder folder,
        WebsiteImportTarget target,
        Dictionary<string, List<WorkAfterImport>> afterImport
    )
    {
        var rows = ReadImageList(folder.ImageList, out var errors);
        var byWork = new Dictionary<WorkAfterImport, (List<WebsiteImportImage> ToAdd, int AlreadyThere)>();
        var withoutWork = new List<string>();
        var missingFiles = new List<string>();

        // the download's own slugs for each title, to tell apart two works of a type sharing one
        var slugsByKey = rows
            .GroupBy(row => WorkKey(row.WorkType, row.Title), ImportNames.Comparer)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Slug).Distinct().Count(), ImportNames.Comparer);

        foreach (var row in rows)
        {
            if (!folder.ImageFileIdsByPath.TryGetValue(row.File, out var fileId))
            {
                missingFiles.Add(row.File);
                continue;
            }

            if (WorkFor(row, slugsByKey[WorkKey(row.WorkType, row.Title)], afterImport) is not { } work)
            {
                withoutWork.Add(row.File);
                continue;
            }

            var (toAdd, alreadyThere) = byWork.GetValueOrDefault(work, ([], 0));
            var hasIt = work.Existing?.Images.Any(image =>
                target.Sha256ByStorageKey.TryGetValue(image.StorageKey, out var sha256) && sha256 == row.Sha256
            ) is true;

            if (hasIt)
            {
                alreadyThere++;
            }
            // the same image listed twice for one work is added once
            else if (toAdd.All(image => image.Sha256 != row.Sha256))
            {
                toAdd.Add(new WebsiteImportImage(row.File, fileId, row.Sha256));
            }

            byWork[work] = (toAdd, alreadyThere);
        }

        var listed = rows.Select(row => row.File).ToHashSet(StringComparer.Ordinal);

        return new WebsiteImportImagesPlan(
            [
                .. byWork.Select(entry =>
                    new WebsiteImportWorkImages(entry.Key.Id, entry.Key.TypeName, entry.Key.Title, entry.Value.ToAdd, entry.Value.AlreadyThere)
                ),
            ],
            errors,
            withoutWork,
            missingFiles,
            [.. folder.ImageFileIdsByPath.Keys.Where(path => path != ImageListFileName && !listed.Contains(path)).Order(StringComparer.Ordinal)]
        );
    }

    // The work a row's image goes to: by type and title, since slugs can differ between websites.
    // When the title is on several works, of the download or here, only the one with the row's
    // slug; with none, nothing rather than a guess. The work import skips a repeated title, so
    // the second "Dawn" isn't imported and its images mustn't land on the first
    private static WorkAfterImport? WorkFor(
        ImageListRow row,
        int slugCountInDownload,
        Dictionary<string, List<WorkAfterImport>> afterImport
    )
    {
        if (!afterImport.TryGetValue(WorkKey(row.WorkType, row.Title), out var sameTitle))
        {
            return null;
        }

        return slugCountInDownload == 1 && sameTitle.Count == 1
            ? sameTitle[0]
            : sameTitle.FirstOrDefault(work => work.Slug.Value == row.Slug);
    }

    // What a post's work picture would link to once the import has run: a work here with the
    // same image, as the post import finds it, or else the work images.csv says the image is
    // for, if that work will be here. The link's storage key is a placeholder, since the image
    // has no key here until it's uploaded
    private sealed class WorksAfterImport(
        WebsiteImportTarget target,
        Dictionary<string, List<WorkAfterImport>> afterImport,
        WebsiteImportImagesPlan images
    ) : IPostImportWorks
    {
        private readonly WebsiteWorks _website = new(target.Works, target.Sha256ByStorageKey);

        private readonly Dictionary<string, WorkId> _workIdsBySha256 = images
            .Works.SelectMany(work => work.ToAdd.Select(image => (image.Sha256, Id: work.WorkId)))
            .DistinctBy(pair => pair.Sha256)
            .ToDictionary(pair => pair.Sha256, pair => pair.Id);

        public IReadOnlySet<string> Slugs { get; } = afterImport.Values.SelectMany(sameTitle => sameTitle).Select(work => work.Slug.Value).ToHashSet();

        public PostWorkLink? Find(PostWorkReference reference) =>
            _website.Find(reference)
            ?? (
                _workIdsBySha256.TryGetValue(reference.Sha256.ToLowerInvariant(), out var id)
                    ? new PostWorkLink(id, new string('0', 32))
                    : null
            );
    }

    // Slug is empty when the row has none
    private record ImageListRow(string File, string WorkType, string Title, string Slug, string Sha256);

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
                errors.Add(new ImportError(row.RowNumber, null, "Every row needs a file, work type, title and sha256."));
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

    // a type's name and a work's title, as one key compared the way import names are
    private static string WorkKey(string typeName, string title) => $"{typeName}\u0000{title}";
}
