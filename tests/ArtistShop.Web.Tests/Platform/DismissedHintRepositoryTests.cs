using ArtistShop.Web.Domain.Platform;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Platform;

[Collection(DatabaseCollection.Name)]
public sealed class DismissedHintRepositoryTests(TestDatabaseFixture database)
{
    private readonly AccountProfileRepository _profiles = new(database.PlatformDataSource);
    private readonly DismissedHintRepository _dismissedHints = new(database.PlatformDataSource);

    // unique to one test, since every test shares the platform test database
    private async Task<string> NewProfileAsync()
    {
        var userId = Guid.NewGuid().ToString();
        await _profiles.AddAsync(userId);
        return userId;
    }

    [Fact]
    public async Task ReadsBackTheHintsDismissedAndNotShownAgain()
    {
        var userId = await NewProfileAsync();

        await _dismissedHints.DismissAsync(userId, [HintType.Collections, HintType.Vocabularies, HintType.PostImport]);
        await _dismissedHints.DismissAsync(userId, [HintType.Collections]);
        await _dismissedHints.ShowAsync(userId, [HintType.Vocabularies]);

        Assert.Equal([HintType.Collections, HintType.PostImport], (await _dismissedHints.GetAsync(userId)).Order());
    }

    // a profile is made on signing in, so this is an account deleted since its page loaded
    [Fact]
    public async Task SavesNothingForAnAccountWithNoProfile()
    {
        var userId = Guid.NewGuid().ToString();

        await _dismissedHints.DismissAsync(userId, [HintType.Collections]);

        Assert.Empty(await _dismissedHints.GetAsync(userId));
    }

    [Fact]
    public async Task AddingAProfileAgainKeepsItsHints()
    {
        var userId = await NewProfileAsync();
        await _dismissedHints.DismissAsync(userId, [HintType.Collections]);

        await _profiles.AddAsync(userId);

        Assert.Equal([HintType.Collections], await _dismissedHints.GetAsync(userId));
    }

    [Fact]
    public async Task DeletingTheProfileDeletesItsHints()
    {
        var userId = await NewProfileAsync();
        var otherUserId = await NewProfileAsync();
        await _dismissedHints.DismissAsync(userId, [HintType.Collections]);
        await _dismissedHints.DismissAsync(otherUserId, [HintType.Collections]);

        await _profiles.DeleteAsync(userId);

        Assert.Empty(await _dismissedHints.GetAsync(userId));
        Assert.Equal([HintType.Collections], await _dismissedHints.GetAsync(otherUserId));
    }
}
