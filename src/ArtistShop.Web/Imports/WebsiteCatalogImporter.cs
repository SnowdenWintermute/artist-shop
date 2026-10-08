using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Imports;

// the website's catalog, which the whole-website import adds to
public record CatalogRepositories(
    WorkFieldRepository Fields,
    WorkTypeRepository Types,
    VocabularyRepository Vocabularies,
    VocabularyTermRepository Terms,
    CollectionRepository Collections,
    ProductTypeRepository ProductTypes,
    WorkRepository Works
);

// one stage of the import done: what it was, and how many things it added
public record WebsiteImportStageDone(string Stage, int AddedCount);

// The catalog stages of the whole-website import: work types, then vocabularies, then each
// work file. Each is planned again against the website as the stage before left it, and
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
        // the stages are named in the website's words
        SiteWording wording,
        Func<WebsiteImportStageDone, Task> onStageDone
    )
    {
        if (!WebsiteImportPlanner.TryCheckFolder(folder, await repositories.ProductTypes.GetAllAsync(), out var manifest, out var problems))
        {
            return string.Join(" ", problems);
        }

        try
        {
            var types = WorkTypeImportPlanner.Plan(
                Unwrap.Value(folder.WorkTypesCsv),
                manifest.ListSeparator,
                await SetupAsync(repositories)
            );

            if (types.Errors.Count > 0)
            {
                return ChangedSinceReviewProblem;
            }

            await WorkTypeImportPlanner.ApplyAsync(types, repositories.Types);
            await onStageDone(new WebsiteImportStageDone($"{wording.Work.SingularHeading} types", types.Additions.Count));

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
            // a change can be new terms or work types for a vocabulary already here, so the
            // vocabularies added are only the new ones
            await onStageDone(new WebsiteImportStageDone("Vocabularies", vocabularies.Changes.Count(change => change.ExistingId is null)));
            await onStageDone(new WebsiteImportStageDone("Vocabulary terms", vocabularies.Changes.Sum(change => change.AddedTerms.Count)));

            var settings = WebsiteImportPlanner.WorkSettings(await repositories.ProductTypes.GetAllAsync(), manifest.ListSeparator);
            var allTypes = await repositories.Types.GetAllAsync();

            foreach (var (fileName, typeName) in manifest.WorkFiles)
            {
                var type = allTypes.FirstOrDefault(type => ImportNames.Comparer.Equals(type.Name.Value, typeName));
                var snapshot = type is null
                    ? null
                    : await WorkImportCatalogSnapshot.LoadAsync(
                        type.Id,
                        repositories.Types,
                        repositories.Vocabularies,
                        repositories.Collections,
                        repositories.ProductTypes,
                        repositories.Works
                    );

                if (snapshot is null)
                {
                    return ChangedSinceReviewProblem;
                }

                var works = WorkImportPlanner.Plan(folder.WorkCsvsByFileName[fileName], settings, snapshot);

                if (works.Errors.Count > 0)
                {
                    return ChangedSinceReviewProblem;
                }

                // one transaction a file, so a file is in whole or not at all
                if (works.Additions.Count > 0)
                {
                    await repositories.Works.AddManyAsync([.. works.Additions.Select(addition => addition.Addition)]);
                }

                await onStageDone(new WebsiteImportStageDone($"{wording.Work.PluralHeading}: {snapshot.WorkType.Name.Value}", works.Additions.Count));
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
