using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ArtworkImageRepositoryTests(TestDatabaseFixture database)
{
    private readonly ArtworkImageRepository _images = new(database.Site);
    private readonly ArtworkRepository _artworks = new(database.Site);
    private readonly CatalogTestData _catalog = new(database.Site);

    private async Task<Artwork> GetExistingAsync(ArtworkId id) =>
        await _artworks.GetByIdAsync(id) ?? throw new InvalidOperationException("The artwork is missing.");

    private static string UniqueName(string prefix) => $"{prefix} {Guid.NewGuid():n}";

    [Fact]
    public async Task AttachesAPrimaryImageIgnoringCase()
    {
        var name = UniqueName("Sunset");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var painting = await _catalog.AddArtworkAsync(paintingTypeId, name);
        var image = CatalogTestData.CreateTestImage();

        var attachment = await _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
            paintingTypeId,
            new ArtworkName(name.ToUpperInvariant()),
            image,
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(ArtworkNameMatchType.OneImagelessArtwork, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.ArtworkIds);

        var artwork = await GetExistingAsync(painting.Id);
        Assert.Equal([image], artwork.Images);
        Assert.Equal(0, artwork.MainImageIndex);
    }

    // the first becomes the primary image, and each after it goes last, as the whole-website import adds them
    [Fact]
    public async Task AppendsAfterTheArtworksOtherImages()
    {
        var painting = await _catalog.AddArtworkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Dawn"));
        var first = CatalogTestData.CreateTestImage();
        var second = CatalogTestData.CreateTestImage();

        Assert.True((await _images.AppendImageAsync(painting.Id, first, CatalogTestData.UniqueSha256())).Appended);
        Assert.True((await _images.AppendImageAsync(painting.Id, second, CatalogTestData.UniqueSha256())).Appended);

        var artwork = await GetExistingAsync(painting.Id);
        Assert.Equal([first, second], artwork.Images);
        Assert.Equal(0, artwork.MainImageIndex);
    }

    // a retried upload, or the same folder imported twice: nothing is added, and the answer is the
    // image already there
    [Fact]
    public async Task AppendingAnImageTheArtworkHasAlreadyAddsNothingAndReturnsIt()
    {
        var painting = await _catalog.AddArtworkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Dawn"));
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
    public async Task ConcurrentAppendsToOneArtworkTakeTurns()
    {
        var painting = await _catalog.AddArtworkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Race"));
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
        var artwork = await GetExistingAsync(painting.Id);
        Assert.Equal(5, artwork.Images.Count);
        Assert.Equal(0, artwork.MainImageIndex);
    }

    [Fact]
    public async Task ListsOnlyTheHashesItHas()
    {
        var painting = await _catalog.AddArtworkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Dawn"));
        var hashed = CatalogTestData.CreateTestImage();
        var sha256 = CatalogTestData.UniqueSha256();
        await _images.AppendImageAsync(painting.Id, hashed, sha256);
        var unhashed = await _catalog.AddPaintingAsync(UniqueName("Dusk"), termIds: [], seriesIds: [], images: [CatalogTestData.CreateTestImage()]);
        var unhashedKey = (await GetExistingAsync(unhashed.Id)).Images[0].StorageKey;

        var hashes = await _images.GetSha256ByStorageKeyAsync([hashed.StorageKey, unhashedKey]);

        Assert.Equal(new Dictionary<string, string> { [hashed.StorageKey] = sha256 }, hashes);
    }

    // the form sends no hashes, and a kept image keeps its own
    [Fact]
    public async Task SavingTheArtworkFormKeepsItsImagesHashes()
    {
        var name = UniqueName("Dawn");
        var painting = await _catalog.AddArtworkAsync(await _catalog.GetPaintingTypeIdAsync(), name);
        var image = CatalogTestData.CreateTestImage();
        var sha256 = CatalogTestData.UniqueSha256();
        await _images.AppendImageAsync(painting.Id, image, sha256);
        var added = CatalogTestData.CreateTestImage();

        await _artworks.UpdateAsync(
            new ArtworkCatalogUpdate(
                new ArtworkDetailsUpdate(
                    painting.Id,
                    new ArtworkName(name),
                    ArtworkSlug.FromName(name),
                    Description: null,
                    DateCreated: null,
                    Dimensions: null,
                    Duration: null,
                    VocabularyTermIds: [],
                    SeriesIds: []
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

    // the bulk upload's check that an artwork doesn't already have an image from a file
    [Fact]
    public async Task ListsTheFileNamesEachArtworksImagesWereUploadedUnder()
    {
        var typeId = await _catalog.GetPaintingTypeIdAsync();
        var dawn = await _catalog.AddArtworkAsync(typeId, UniqueName("Dawn"));
        var dusk = await _catalog.AddArtworkAsync(typeId, UniqueName("Dusk"));
        await _images.AppendImageAsync(dawn.Id, CatalogTestData.CreateTestImage() with { OriginalFileName = "Dawn.jpg" }, CatalogTestData.UniqueSha256());
        await _images.AppendImageAsync(dawn.Id, CatalogTestData.CreateTestImage() with { OriginalFileName = "Dawn (2).jpg" }, CatalogTestData.UniqueSha256());
        // saved without a name, so it isn't listed
        await _images.AppendImageAsync(dusk.Id, CatalogTestData.CreateTestImage(), CatalogTestData.UniqueSha256());

        var fileNames = await _images.GetFileNamesAsync([dawn.Id, dusk.Id]);

        Assert.Equal(["Dawn (2).jpg", "Dawn.jpg"], fileNames[dawn.Id].Order(StringComparer.Ordinal));
        Assert.Empty(fileNames[dusk.Id]);
    }

    [Fact]
    public async Task AppendingToAnArtworkThatsGoneThrows()
    {
        var painting = await _catalog.AddArtworkAsync(await _catalog.GetPaintingTypeIdAsync(), UniqueName("Gone"));
        await _artworks.DeleteAsync(painting.Id);

        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() => _images.AppendImageAsync(painting.Id, CatalogTestData.CreateTestImage(), CatalogTestData.UniqueSha256()));
    }

    [Fact]
    public async Task ReportsANameNoArtworkHas()
    {
        var attachment = await _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
            await _catalog.GetPaintingTypeIdAsync(),
            new ArtworkName(UniqueName("Nothing")),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(ArtworkNameMatchType.NoArtwork, attachment.MatchType);
        Assert.Empty(attachment.ArtworkIds);
    }

    [Fact]
    public async Task AttachesNothingWhenSeveralArtworksShareTheName()
    {
        var name = UniqueName("Twin");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var first = await _catalog.AddArtworkAsync(paintingTypeId, name);
        var second = await _catalog.AddArtworkAsync(paintingTypeId, name);

        var attachment = await _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
            paintingTypeId,
            new ArtworkName(name),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(ArtworkNameMatchType.SeveralArtworks, attachment.MatchType);
        Assert.Equivalent(new[] { first.Id, second.Id }, attachment.ArtworkIds);
        Assert.Empty((await GetExistingAsync(first.Id)).Images);
        Assert.Empty((await GetExistingAsync(second.Id)).Images);
    }

    [Fact]
    public async Task SkipsAnArtworkThatAlreadyHasAnImage()
    {
        var name = UniqueName("Framed");
        var existingImage = CatalogTestData.CreateTestImage();
        var painting = await _catalog.AddPaintingAsync(
            name,
            termIds: [],
            seriesIds: [],
            images: [existingImage]
        );

        var attachment = await _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
            await _catalog.GetPaintingTypeIdAsync(),
            new ArtworkName(name),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(ArtworkNameMatchType.ArtworkWithImages, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.ArtworkIds);
        Assert.Equal([existingImage], (await GetExistingAsync(painting.Id)).Images);
    }

    [Fact]
    public async Task IgnoresASameNamedArtworkOfAnotherType()
    {
        var name = UniqueName("Wave");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var painting = await _catalog.AddArtworkAsync(paintingTypeId, name);
        var sculpture = await _catalog.AddArtworkAsync(await _catalog.GetTypeIdAsync("Sculpture"), name);

        var attachment = await _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
            paintingTypeId,
            new ArtworkName(name),
            CatalogTestData.CreateTestImage(),
            CatalogTestData.UniqueSha256()
        );

        Assert.Equal(ArtworkNameMatchType.OneImagelessArtwork, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.ArtworkIds);
        Assert.Empty((await GetExistingAsync(sculpture.Id)).Images);
    }

    // two files like "Sunset.jpg" and "sunset.png" uploading at the same time
    [Fact]
    public async Task ConcurrentAttachesToOneArtworkAttachOnce()
    {
        var name = UniqueName("Race");
        var paintingTypeId = await _catalog.GetPaintingTypeIdAsync();
        var painting = await _catalog.AddArtworkAsync(paintingTypeId, name);

        var attachments = await Task.WhenAll(
            Enumerable
                .Range(0, 8)
                .Select(_ =>
                    _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
                        paintingTypeId,
                        new ArtworkName(name),
                        CatalogTestData.CreateTestImage(),
                        CatalogTestData.UniqueSha256()
                    )
                )
        );

        Assert.Single(attachments, attachment => attachment.MatchType == ArtworkNameMatchType.OneImagelessArtwork);
        Assert.Equal(
            7,
            attachments.Count(attachment =>
                attachment.MatchType == ArtworkNameMatchType.ArtworkWithImages
            )
        );
        Assert.Single((await GetExistingAsync(painting.Id)).Images);
    }

    [Fact]
    public async Task RejectsADeletedArtworkType()
    {
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
                new ArtworkTypeId(int.MaxValue),
                new ArtworkName(UniqueName("Orphan")),
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
        var imageless = await _catalog.AddArtworkAsync(paintingTypeId, imagelessName);

        var withImagesName = UniqueName("With images");
        var withImages = await _catalog.AddPaintingAsync(
            withImagesName,
            termIds: [],
            seriesIds: [],
            images: [CatalogTestData.CreateTestImage()]
        );

        var twinName = UniqueName("Twin");
        var firstTwin = await _catalog.AddArtworkAsync(paintingTypeId, twinName);
        var secondTwin = await _catalog.AddArtworkAsync(paintingTypeId, twinName);

        var sculptureName = UniqueName("Sculpture only");
        await _catalog.AddArtworkAsync(await _catalog.GetTypeIdAsync("Sculpture"), sculptureName);

        var sentImagelessName = new ArtworkName(imagelessName.ToUpperInvariant());
        var missingName = new ArtworkName(UniqueName("Missing"));

        var matches = await _images.GetArtworkNameMatchesAsync(
            paintingTypeId,
            [
                sentImagelessName,
                new ArtworkName(withImagesName),
                new ArtworkName(twinName),
                new ArtworkName(sculptureName),
                missingName,
            ]
        );

        Assert.Equal(5, matches.Count);

        Assert.Equal(ArtworkNameMatchType.OneImagelessArtwork, matches[sentImagelessName].Type);
        Assert.Equal([imageless.Id], matches[sentImagelessName].ArtworkIds);

        Assert.Equal(ArtworkNameMatchType.ArtworkWithImages, matches[new ArtworkName(withImagesName)].Type);
        Assert.Equal([withImages.Id], matches[new ArtworkName(withImagesName)].ArtworkIds);

        Assert.Equal(ArtworkNameMatchType.SeveralArtworks, matches[new ArtworkName(twinName)].Type);
        Assert.Equivalent(
            new[] { firstTwin.Id, secondTwin.Id },
            matches[new ArtworkName(twinName)].ArtworkIds
        );

        Assert.Equal(ArtworkNameMatchType.NoArtwork, matches[new ArtworkName(sculptureName)].Type);
        Assert.Empty(matches[new ArtworkName(sculptureName)].ArtworkIds);

        Assert.Equal(ArtworkNameMatchType.NoArtwork, matches[missingName].Type);
        Assert.Empty(matches[missingName].ArtworkIds);
    }

    [Fact]
    public async Task MatchingRejectsNamesThatDifferOnlyInCase()
    {
        var name = UniqueName("Echo");

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await _images.GetArtworkNameMatchesAsync(
                await _catalog.GetPaintingTypeIdAsync(),
                [new ArtworkName(name), new ArtworkName(name.ToUpperInvariant())]
            )
        );
    }

    [Fact]
    public async Task MatchingRejectsADeletedArtworkType()
    {
        await Assert.ThrowsAsync<ChangedSincePageLoadException>(() =>
            _images.GetArtworkNameMatchesAsync(
                new ArtworkTypeId(int.MaxValue),
                [new ArtworkName(UniqueName("Orphan"))]
            )
        );
    }
}
