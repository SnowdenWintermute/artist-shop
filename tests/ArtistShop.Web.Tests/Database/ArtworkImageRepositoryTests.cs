using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class ArtworkImageRepositoryTests(TestDatabaseFixture database)
{
    private readonly ArtworkImageRepository _images = new(database.DataSource);
    private readonly ArtworkRepository _artworks = new(database.DataSource);
    private readonly CatalogTestData _catalog = new(database.DataSource);

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
            image
        );

        Assert.Equal(ArtworkNameMatchType.OneImagelessArtwork, attachment.MatchType);
        Assert.Equal([painting.Id], attachment.ArtworkIds);

        var artwork = await GetExistingAsync(painting.Id);
        Assert.Equal([image], artwork.Images);
        Assert.Equal(0, artwork.MainImageIndex);
    }

    [Fact]
    public async Task ReportsANameNoArtworkHas()
    {
        var attachment = await _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
            await _catalog.GetPaintingTypeIdAsync(),
            new ArtworkName(UniqueName("Nothing")),
            CatalogTestData.CreateTestImage()
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
            CatalogTestData.CreateTestImage()
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
            CatalogTestData.CreateTestImage()
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
            CatalogTestData.CreateTestImage()
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
                        CatalogTestData.CreateTestImage()
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
        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _images.AttachPrimaryImageToImagelessArtworkByNameAsync(
                new ArtworkTypeId(int.MaxValue),
                new ArtworkName(UniqueName("Orphan")),
                CatalogTestData.CreateTestImage()
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
        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            _images.GetArtworkNameMatchesAsync(
                new ArtworkTypeId(int.MaxValue),
                [new ArtworkName(UniqueName("Orphan"))]
            )
        );
    }
}
