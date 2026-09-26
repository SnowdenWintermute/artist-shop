using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Platform;

[Collection(DatabaseCollection.Name)]
public sealed class SignUpCodeRepositoryTests(TestDatabaseFixture database)
{
    private readonly SignUpCodeRepository _codes = new(database.PlatformDataSource);

    [Fact]
    public async Task ListsAnAddedCodeByItsNoteAndExpiry()
    {
        var expiresAt = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var id = await _codes.AddAsync(SignUpCode.New(), "Alice, met at the art fair", expiresAt);

        var listed = Assert.Single(await _codes.GetAllAsync(), code => code.Id == id);
        Assert.Equal("Alice, met at the art fair", listed.Note);
        Assert.Equal(expiresAt, listed.ExpiresAt);
    }

    [Fact]
    public async Task ARevokedCodeIsGone()
    {
        var id = await _codes.AddAsync(SignUpCode.New(), "Bob", DateTimeOffset.UtcNow.AddDays(1));

        await _codes.DeleteAsync(id);

        Assert.DoesNotContain(await _codes.GetAllAsync(), code => code.Id == id);
    }

    [Fact]
    public async Task DeletingExpiredCodesKeepsThoseExpiringLater()
    {
        var now = DateTimeOffset.UtcNow;
        var old = await _codes.AddAsync(SignUpCode.New(), "long expired", now.AddDays(-31));
        var recent = await _codes.AddAsync(SignUpCode.New(), "just expired", now.AddDays(-1));

        await _codes.DeleteExpiredBeforeAsync(now.AddDays(-30));

        var ids = (await _codes.GetAllAsync()).Select(code => code.Id).ToList();
        Assert.DoesNotContain(old, ids);
        Assert.Contains(recent, ids);
    }

    [Fact]
    public void AListedCodeIsExpiredFromItsExpiryOn()
    {
        var expiresAt = new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.Zero);
        var listing = new SignUpCodeListing(new SignUpCodeId(1), "Carol", expiresAt.AddDays(-14), expiresAt);

        Assert.False(listing.IsExpiredAt(expiresAt.AddSeconds(-1)));
        Assert.True(listing.IsExpiredAt(expiresAt));
    }
}
