using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class WorkImageRepositoryTests(TestDatabaseFixture database)
{
    private readonly WorkImageRepository _images = new(database.Site);
    private readonly WorkRepository _works = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private async Task<Work> GetExistingAsync(WorkId id) =>
        await _works.GetByIdAsync(id) ?? throw new InvalidOperationException("The work is missing.");

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():n}";

    [Fact]
    public async Task AttachesAPrimaryImageIgnoringCase()
    {
        var name = UniqueName("Sunset");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var painting = await _catalog.AddWorkAsync(paintingTypeId, name);
        var image = CatalogTestData.CreateTestImage();

        var attachment = await _images.AttachPrimaryImageToImagelessWorkByNameAsync(
            paintingTypeId,
            new WorkName(name.ToUpperInvariant()),
            image,
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(WorkNameMatchType.OneImagelessWork, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.WorkIds);

        var work = await GetExistingAsync(painting.Id);
        Assert.Equal([image], work.Images);
        Assert.Equal(0, work.MainImageIndex);
    }

    // the first becomes the primary image, and each after it goes last, as the whole-website import adds them
    [Fact]
    public async Task AppendsAfterTheWorksOtherImages()
    {
        var painting = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Dawn"));
        var first = CatalogTestData.CreateTestImage();
        var second = CatalogTestData.CreateTestImage();

        Assert.True((await _images.AppendImageAsync(painting.Id, first, CatalogTestData.UniqueSha256())).Appended);
        Assert.True((await _images.AppendImageAsync(painting.Id, second, CatalogTestData.UniqueSha256())).Appended);

        var work = await GetExistingAsync(painting.Id);
        Assert.Equal([first, second], work.Images);
        Assert.Equal(0, work.MainImageIndex);
    }

    // a retried upload, or the same folder imported twice: nothing is added, and the answer is the
    // image already there
    [Fact]
    public async Task AppendingAnImageTheWorkHasAlreadyAddsNothingAndReturnsIt()
    {
        var painting = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Dawn"));
        var sha256 = CatalogTestData.UniqueSha256();
        var first = CatalogTestData.CreateTestImage();

        Assert.Equal(new ImageAppendResult(first, Appended: true), await _images.AppendImageAsync(painting.Id, first, sha256));
        Assert.Equal(
            new ImageAppendResult(first, Appended: false),
            await _images.AppendImageAsync(painting.Id, CatalogTestData.CreateTestImage(), sha256)
        );

        Assert.Equal([first], (await GetExistingAsync(painting.Id)).Images);
    }

    [Fact]
    public async Task ConcurrentAppendsToOneWorkTakeTurns()
    {
        var painting = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Race"));
        var sameSha256 = CatalogTestData.UniqueSha256();

        var appended = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(index => _images.AppendImageAsync(
                    painting.Id,
                    CatalogTestData.CreateTestImage(),
                    // two of each hash, so each pair adds once
                    index % 2 == 0 ? CatalogTestData.UniqueSha256() : sameSha256
                ))
        );

        // four different hashes and one more shared by the other four
        Assert.Equal(5, appended.Count(result => result.Appended));
        var work = await GetExistingAsync(painting.Id);
        Assert.Equal(5, work.Images.Count);
        Assert.Equal(0, work.MainImageIndex);
    }

    [Fact]
    public async Task ListsOnlyTheHashesItHas()
    {
        var painting = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Dawn"));
        var hashed = CatalogTestData.CreateTestImage();
        var sha256 = CatalogTestData.UniqueSha256();
        await _images.AppendImageAsync(painting.Id, hashed, sha256);
        var unhashed = await _catalog.AddPaintingAsync(UniqueName("Dusk"), termIds: [], collectionIds: [], images: [CatalogTestData.CreateTestImage()]);
        var unhashedKey = (await GetExistingAsync(unhashed.Id)).Images[0].StorageKey;

        var hashes = await _images.GetSha256ByStorageKeyAsync([hashed.StorageKey, unhashedKey]);

        Assert.Equal(new Dictionary<string, string> { [hashed.StorageKey] = sha256 }, hashes);
    }

    // the form sends no hashes, and a kept image keeps its own
    [Fact]
    public async Task SavingTheWorkFormKeepsItsImagesHashes()
    {
        var name = UniqueName("Dawn");
        var painting = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), name);
        var image = CatalogTestData.CreateTestImage();
        var sha256 = CatalogTestData.UniqueSha256();
        await _images.AppendImageAsync(painting.Id, image, sha256);
        var added = CatalogTestData.CreateTestImage();

        await _works.UpdateAsync(
            new WorkCatalogUpdate(
                new WorkDetailsUpdate(
                    painting.Id,
                    new WorkName(name),
                    WorkSlug.FromName(name),
                    Description: null,
                    DateCreated: null,
                    Dimensions: null,
                    Duration: null,
                    VocabularyTermIds: [],
                    CollectionIds: []
                ),
                Images: [added, image],
                MainImageIndex: 1
            )
        );

        Assert.Equal([added, image], (await GetExistingAsync(painting.Id)).Images);
        Assert.Equal(
            new Dictionary<string, string> { [image.StorageKey] = sha256 },
            await _images.GetSha256ByStorageKeyAsync([image.StorageKey, added.StorageKey])
        );
    }

    // the bulk upload's check that a work doesn't already have an image from a file
    [Fact]
    public async Task ListsTheFileNamesEachWorksImagesWereUploadedUnder()
    {
        var typeId = await _catalog.GetPaintingTypeIdAsync();
        var dawn = await _catalog.AddWorkAsync(typeId, UniqueName("Dawn"));
        var dusk = await _catalog.AddWorkAsync(typeId, UniqueName("Dusk"));
        await _images.AppendImageAsync(dawn.Id, CatalogTestData.CreateTestImage() with { OriginalFileName = "Dawn.jpg" }, CatalogTestData.UniqueSha256());
        await _images.AppendImageAsync(dawn.Id, CatalogTestData.CreateTestImage() with { OriginalFileName = "Dawn (2).jpg" }, CatalogTestData.UniqueSha256());
        // saved without a name, so it isn't listed
        await _images.AppendImageAsync(dusk.Id, CatalogTestData.CreateTestImage(), CatalogTestData.UniqueSha256());

        var fileNames = await _images.GetFileNamesAsync([dawn.Id, dusk.Id]);

        Assert.Equal(["Dawn (2).jpg", "Dawn.jpg"], fileNames[dawn.Id].Order(StringComparer.Ordinal));
        Assert.Empty(fileNames[dusk.Id]);
    }

    [Fact]
    public async Task AppendingToAnWorkThatsGoneThrows()
    {
        var painting = await _catalog.AddWorkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Gone"));
        await _works.DeleteAsync(painting.Id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _images.AppendImageAsync(painting.Id, CatalogTestData.CreateTestImage(), CatalogTestData.UniqueSha256()));
    }

    [Fact]
    public async Task ReportsANameNoWorkHas()
    {
        var attachment = await _images.AttachPrimaryImageToImagelessWorkByNameAsync(
            await _catalog.GetPaintingTypeIdAsync(),
            new WorkName(UniqueName("Nothing")),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(WorkNameMatchType.NoWork, attachment.MatchType);
        Assert.Empty(attachment.WorkIds);
    }

    [Fact]
    public async Task AttachesNothingWhenSeveralWorksShareTheName()
    {
        var name = UniqueName("Twin");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var first = await _catalog.AddWorkAsync(paintingTypeId, name);
        var second = await _catalog.AddWorkAsync(paintingTypeId, name);

        var attachment = await _images.AttachPrimaryImageToImagelessWorkByNameAsync(
            paintingTypeId,
            new WorkName(name),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(WorkNameMatchType.SeveralWorks, attachment.MatchType);
        Assert.Equivalent(new[] { first.Id, second.Id }, attachment.WorkIds);
        Assert.Empty((await GetExistingAsync(first.Id)).Images);
        Assert.Empty((await GetExistingAsync(second.Id)).Images);
    }

    [Fact]
    public async Task SkipsAnWorkThatAlreadyHasAnImage()
    {
        var name = UniqueName("Framed");
        var existingImage = CatalogTestData.CreateTestImage();
        var painting = await _catalog.AddPaintingAsync(
            name,
            termIds: [],
            collectionIds: [],
            images: [existingImage]
        );

        var attachment = await _images.AttachPrimaryImageToImagelessWorkByNameAsync(
            await _catalog.GetPaintingTypeIdAsync(),
            new WorkName(name),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(WorkNameMatchType.WorkWithImages, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.WorkIds);
        Assert.Equal([existingImage], (await GetExistingAsync(painting.Id)).Images);
    }

    [Fact]
    public async Task IgnoresASameNamedWorkOfAnotherType()
    {
        var name = UniqueName("Wave");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var painting = await _catalog.AddWorkAsync(paintingTypeId, name);
        var sculpture = await _catalog.AddWorkAsync(await _catalog.GetTypeIdAsync("Sculpture"), name);

        var attachment = await _images.AttachPrimaryImageToImagelessWorkByNameAsync(
            paintingTypeId,
            new WorkName(name),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(WorkNameMatchType.OneImagelessWork, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.WorkIds);
        Assert.Empty((await GetExistingAsync(sculpture.Id)).Images);
    }

    // two files like "Sunset.jpg" and "sunset.png" uploading at the same time
    [Fact]
    public async Task ConcurrentAttachesToOneWorkAttachOnce()
    {
        var name = UniqueName("Race");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var painting = await _catalog.AddWorkAsync(paintingTypeId, name);

        var attachments = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(_ =>
                    _images.AttachPrimaryImageToImagelessWorkByNameAsync(
                        paintingTypeId,
                        new WorkName(name),
                        CatalogTestData.CreateTestImage(),
                        CatalogTestData.UniqueSha256()
                    )
                )
        );

        Assert.Single(attachments, attachment => attachment.MatchType == WorkNameMatchType.OneImagelessWork);
        Assert.Equal(
            7,
            attachments.Count(attachment =>
                attachment.MatchType == WorkNameMatchType.WorkWithImages
            )
        );
        Assert.Single((await GetExistingAsync(painting.Id)).Images);
    }

    [Fact]
    public async Task RejectsADeletedWorkType()
    {
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _images.AttachPrimaryImageToImagelessWorkByNameAsync(
                new WorkTypeId(int.MaxValue),
                new WorkName(UniqueName("Orphan")),
                CatalogTestData.CreateTestImage(),
                CatalogTestData.UniqueSha256()
            )
        );
    }

    [Fact]
    public async Task MatchesEachNameTheWayAttachingWould()
    {
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();

        var imagelessName = UniqueName("Imageless");
        var imageless = await _catalog.AddWorkAsync(paintingTypeId, imagelessName);

        var withImagesName = UniqueName("With images");
        var withImages = await _catalog.AddPaintingAsync(
            withImagesName,
            termIds: [],
            collectionIds: [],
            images: [CatalogTestData.CreateTestImage()]
        );

        var twinName = UniqueName("Twin");
        var firstTwin = await _catalog.AddWorkAsync(paintingTypeId, twinName);
        var secondTwin = await _catalog.AddWorkAsync(paintingTypeId, twinName);

        var sculptureName = UniqueName("Sculpture only");
        await _catalog.AddWorkAsync(await _catalog.GetTypeIdAsync("Sculpture"), sculptureName);

        var sentImagelessName = new WorkName(imagelessName.ToUpperInvariant());
        var missingName = new WorkName(UniqueName("Missing"));

        var matches = await _images.GetWorkNameMatchesAsync(
            paintingTypeId,
            [
                sentImagelessName,
                new WorkName(withImagesName),
                new WorkName(twinName),
                new WorkName(sculptureName),
                missingName,
            ]
        );

        Assert.Equal(5, matches.Count);

        Assert.Equal(WorkNameMatchType.OneImagelessWork, matches[sentImagelessName].Type);
        Assert.Equal([imageless.Id], matches[sentImagelessName].WorkIds);

        Assert.Equal(WorkNameMatchType.WorkWithImages, matches[new WorkName(withImagesName)].Type);
        Assert.Equal([withImages.Id], matches[new WorkName(withImagesName)].WorkIds);

        Assert.Equal(WorkNameMatchType.SeveralWorks, matches[new WorkName(twinName)].Type);
        Assert.Equivalent(
            new[] { firstTwin.Id, secondTwin.Id },
            matches[new WorkName(twinName)].WorkIds
        );

        Assert.Equal(WorkNameMatchType.NoWork, matches[new WorkName(sculptureName)].Type);
        Assert.Empty(matches[new WorkName(sculptureName)].WorkIds);

        Assert.Equal(WorkNameMatchType.NoWork, matches[missingName].Type);
        Assert.Empty(matches[missingName].WorkIds);
    }

    [Fact]
    public async Task MatchingRejectsNamesThatDifferOnlyInCase()
    {
        var name = UniqueName("Echo");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await _images.GetWorkNameMatchesAsync(
                await _catalog.GetPaintingTypeIdAsync(),
                [new WorkName(name), new WorkName(name.ToUpperInvariant())]
            )
        );
    }

    [Fact]
    public async Task MatchingRejectsADeletedWorkType()
    {
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _images.GetWorkNameMatchesAsync(
                new WorkTypeId(int.MaxValue),
                [new WorkName(UniqueName("Orphan"))]
            )
        );
    }
}
