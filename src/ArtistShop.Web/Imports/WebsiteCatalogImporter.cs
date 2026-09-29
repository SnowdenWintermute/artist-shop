using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Imports;

// the website's catalog, which the whole-website import adds to
public record CatalogRepositories(
    ArtworkFieldRepository Fields,
    ArtworkTypeRepository Types,
    VocabularyRepository Vocabularies,
    VocabularyTermRepository Terms,
    SeriesRepository Series,
    ProductTypeRepository ProductTypes,
    ArtworkRepository Artworks
);

// one stage of the import done: what it was, and how many things it added
public record WebsiteImportStageDone(string Stage, int AddedCount);

// The catalog stages of the whole-website import: artwork types, then vocabularies, then each
// artwork file. Each is planned again against the website as the stage before left it, and
// applied, rather than applying the review's plans, whose ids are placeholders. A stage whose plan
// has errors stops the import there: the website changed since the review. What the stages before
// it added stays, and importing again skips it
public static class WebsiteCatalogImporter
{
    public const string ChangedSinceReviewProblem = "The catalog changed since the review. Choose the folder again to check it.";

    // null once every stage has run, otherwise why it stopped. OnStageDone hears each stage as it finishes
    public static async Task<string?> ImportAsync(
        WebsiteImportFolder folder,
        CatalogRepositories repositories,
        Func<WebsiteImportStageDone, Task> onStageDone
    )
    {
        if (!WebsiteImportManifest.TryRead(folder.Manifest, out var manifest, out var manifestProblem))
        {
            return manifestProblem;
        }

        try
        {
            var types = ArtworkTypeImportPlanner.Plan(
                Unwrap.Value(folder.ArtworkTypesCsv),
                manifest.ListSeparator,
                await SetupAsync(repositories)
            );

            if (types.Errors.Count > 0)
            {
                return ChangedSinceReviewProblem;
            }

            await ArtworkTypeImportPlanner.ApplyAsync(types, repositories.Types);
            await onStageDone(new WebsiteImportStageDone("Artwork types", types.Additions.Count));

            var vocabularies = VocabularyImportPlanner.Plan(
                Unwrap.Value(folder.VocabulariesCsv),
                manifest.ListSeparator,
                await SetupAsync(repositories)
            );

            if (vocabularies.Errors.Count > 0)
            {
                return ChangedSinceReviewProblem;
            }

            await VocabularyImportPlanner.ApplyAsync(vocabularies, repositories.Vocabularies, repositories.Terms);
            await onStageDone(new WebsiteImportStageDone("Vocabularies", vocabularies.Changes.Count));

            var settings = WebsiteImportPlanner.ArtworkSettings(await repositories.ProductTypes.GetAllAsync(), manifest.ListSeparator);
            var allTypes = await repositories.Types.GetAllAsync();

            foreach (var (fileName, typeName) in manifest.ArtworkFiles)
            {
                var type = allTypes.FirstOrDefault(type => ImportNames.Comparer.Equals(type.Name.Value, typeName));
                var snapshot = type is null
                    ? null
                    : await ArtworkImportCatalogSnapshot.LoadAsync(
                        type.Id,
                        repositories.Types,
                        repositories.Vocabularies,
                        repositories.Series,
                        repositories.ProductTypes,
                        repositories.Artworks
                    );

                if (snapshot is null)
                {
                    return ChangedSinceReviewProblem;
                }

                var artworks = ArtworkImportPlanner.Plan(folder.ArtworkCsvsByFileName[fileName], settings, snapshot);

                if (artworks.Errors.Count > 0)
                {
                    return ChangedSinceReviewProblem;
                }

                // one transaction a file, so a file is in whole or not at all
                if (artworks.Additions.Count > 0)
                {
                    await repositories.Artworks.AddManyAsync([.. artworks.Additions.Select(addition => addition.Addition)]);
                }

                await onStageDone(new WebsiteImportStageDone($"Artworks: {snapshot.ArtworkType.Name.Value}", artworks.Additions.Count));
            }
        }
        catch (Exception exception) when (exception is NameAlreadyInUseException or ChangedSincePageLoadException)
        {
            return ChangedSinceReviewProblem;
        }

        return null;
    }

    private static Task<CatalogSetupSnapshot> SetupAsync(CatalogRepositories repositories) =>
        CatalogSetupSnapshot.LoadAsync(repositories.Fields, repositories.Types, repositories.Vocabularies);
}
