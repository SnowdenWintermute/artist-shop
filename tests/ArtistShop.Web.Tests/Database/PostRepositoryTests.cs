using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using System.Text.Json;
using Npgsql;

namespace ArtistShop.Web.Tests.Database;

[Collection(DatabaseCollection.Name)]
public sealed class PostRepositoryTests(TestDatabaseFixture database)
{
    private readonly PostRepository _posts = new(database.DataSource);
    private readonly ArtworkRepository _artworks = new(database.DataSource);
    private readonly CatalogTestData _catalog = new(database.DataSource);

    private static readonly PostBody TextOnlyBody = new("""{"ops":[{"insert":"Hello\n"}]}""");

    private static PostBody BodyEmbedding(params ArtworkId[] artworkIds) =>
        new(
            JsonSerializer.Serialize(
                new
                {
                    ops = (object[])
                        [
                            .. artworkIds.Select(id => new
                            {
                                insert = new { artwork = new { artworkId = id.Value } },
                            }),
                            new { insert = "\n" },
                        ],
                }
            )
        );

    private Task<PostId> AddPostAsync(PostBody body, PostStatus status)
    {
        var title = $"Post {Guid.NewGuid():n}";
        return _posts.AddAsync(new PostTitle(title), PostSlug.FromTitle(title), body, status);
    }

    private async Task<Post> GetExistingAsync(PostId id) =>
        await _posts.GetAsync(id) ?? throw new InvalidOperationException("The post is missing.");

    private Task UpdateAsync(Post post, PostBody body, PostStatus status) =>
        _posts.UpdateAsync(post.Id, post.Title, post.Slug, body, status);

    private async Task<ArtworkId> AddArtworkAsync() =>
        (await _catalog.AddPaintingAsync($"Painting {Guid.NewGuid():n}", [], [], [])).Id;

    private async Task<List<PostId>> GetIdsMentioningAsync(ArtworkId artworkId) =>
        [.. (await _posts.GetPublishedMentioningArtworkAsync(artworkId)).Select(post => post.Id)];

    [Fact]
    public async Task KeepsWhatWasSaved()
    {
        var title = $"Post {Guid.NewGuid():n}";

        var id = await _posts.AddAsync(
            new PostTitle(title),
            PostSlug.FromTitle(title),
            TextOnlyBody,
            PostStatus.Draft
        );

        var post = await GetExistingAsync(id);
        Assert.Equal(new PostTitle(title), post.Title);
        Assert.Equal(PostSlug.FromTitle(title), post.Slug);
        Assert.Contains("Hello", post.Body.Json);
        Assert.Null(post.PublishedAt);
    }

    [Fact]
    public async Task RejectsATitleWhoseSlugAnotherPostHas()
    {
        var title = $"Post {Guid.NewGuid():n}";
        await _posts.AddAsync(new PostTitle(title), PostSlug.FromTitle(title), TextOnlyBody, PostStatus.Draft);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _posts.AddAsync(new PostTitle($"{title}!"), PostSlug.FromTitle($"{title}!"), TextOnlyBody, PostStatus.Draft)
        );
    }

    [Fact]
    public async Task UpdateRejectsATitleWhoseSlugAnotherPostHas()
    {
        var taken = await GetExistingAsync(await AddPostAsync(TextOnlyBody, PostStatus.Draft));
        var id = await AddPostAsync(TextOnlyBody, PostStatus.Draft);

        await Assert.ThrowsAsync<NameAlreadyInUseException>(() =>
            _posts.UpdateAsync(id, taken.Title, taken.Slug, TextOnlyBody, PostStatus.Draft)
        );
    }

    [Fact]
    public async Task UpdatingADeletedPostReportsTheChange()
    {
        var post = await GetExistingAsync(await AddPostAsync(TextOnlyBody, PostStatus.Draft));
        await _posts.DeleteAsync(post.Id);

        await Assert.ThrowsAsync<CatalogChangedException>(() =>
            UpdateAsync(post, TextOnlyBody, PostStatus.Draft)
        );
    }

    [Fact]
    public async Task RefusesABodyThatIsNotADelta()
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            AddPostAsync(new PostBody("""{"text":"Hello"}"""), PostStatus.Draft)
        );

        Assert.Equal("check_posts_body_ops", exception.ConstraintName);
    }

    [Fact]
    public async Task SavingAPublishedPostKeepsItsDate()
    {
        var id = await AddPostAsync(TextOnlyBody, PostStatus.Published);
        var published = await GetExistingAsync(id);

        await UpdateAsync(published, TextOnlyBody, PostStatus.Published);

        Assert.Equal(published.PublishedAt, (await GetExistingAsync(id)).PublishedAt);
    }

    [Fact]
    public async Task UnpublishingMakesADraft()
    {
        var id = await AddPostAsync(TextOnlyBody, PostStatus.Published);

        await UpdateAsync(await GetExistingAsync(id), TextOnlyBody, PostStatus.Draft);

        Assert.Null((await GetExistingAsync(id)).PublishedAt);
    }

    [Fact]
    public async Task VisitorsCannotFindADraftBySlug()
    {
        var post = await GetExistingAsync(await AddPostAsync(TextOnlyBody, PostStatus.Draft));

        Assert.Null(await _posts.GetPublishedBySlugAsync(post.Slug));
    }

    [Fact]
    public async Task VisitorsFindAPublishedPostBySlug()
    {
        var post = await GetExistingAsync(await AddPostAsync(TextOnlyBody, PostStatus.Published));

        Assert.Equal(post.Id, (await _posts.GetPublishedBySlugAsync(post.Slug))?.Id);
    }

    [Fact]
    public async Task PublishedListLeavesOutDrafts()
    {
        var draftId = await AddPostAsync(TextOnlyBody, PostStatus.Draft);
        var publishedId = await AddPostAsync(TextOnlyBody, PostStatus.Published);

        var published = (await _posts.GetPublishedAsync()).Select(post => post.Id).ToList();

        Assert.Contains(publishedId, published);
        Assert.DoesNotContain(draftId, published);
    }

    [Fact]
    public async Task FullListPutsDraftsBeforePublishedPosts()
    {
        var publishedId = await AddPostAsync(TextOnlyBody, PostStatus.Published);
        var draftId = await AddPostAsync(TextOnlyBody, PostStatus.Draft);

        var all = (await _posts.GetAllAsync()).Select(post => post.Id).ToList();

        Assert.True(all.IndexOf(draftId) < all.IndexOf(publishedId));
    }

    [Fact]
    public async Task APublishedPostMentionsTheArtworksItEmbeds()
    {
        var artworkId = await AddArtworkAsync();

        var postId = await AddPostAsync(BodyEmbedding(artworkId, artworkId), PostStatus.Published);

        Assert.Equal([postId], await GetIdsMentioningAsync(artworkId));
    }

    [Fact]
    public async Task ADraftMentionsNothingToVisitors()
    {
        var artworkId = await AddArtworkAsync();

        await AddPostAsync(BodyEmbedding(artworkId), PostStatus.Draft);

        Assert.Empty(await GetIdsMentioningAsync(artworkId));
    }

    [Fact]
    public async Task RemovingAnEmbedRemovesTheMention()
    {
        var keptId = await AddArtworkAsync();
        var removedId = await AddArtworkAsync();
        var postId = await AddPostAsync(BodyEmbedding(keptId, removedId), PostStatus.Published);

        await UpdateAsync(await GetExistingAsync(postId), BodyEmbedding(keptId), PostStatus.Published);

        Assert.Equal([postId], await GetIdsMentioningAsync(keptId));
        Assert.Empty(await GetIdsMentioningAsync(removedId));
    }

    [Fact]
    public async Task AnEmbedOfADeletedArtworkStillSaves()
    {
        var artworkId = await AddArtworkAsync();
        await _artworks.DeleteAsync(artworkId);

        var postId = await AddPostAsync(BodyEmbedding(artworkId), PostStatus.Published);

        Assert.NotNull(await _posts.GetAsync(postId));
    }

    [Fact]
    public async Task AnEmbedWhoseIdIsTextIsIgnored()
    {
        var artworkId = await AddArtworkAsync();
        var body = new PostBody(
            JsonSerializer.Serialize(
                new { ops = new[] { new { insert = new { artwork = new { artworkId = $"{artworkId.Value}" } } } } }
            )
        );

        await AddPostAsync(body, PostStatus.Published);

        Assert.Empty(await GetIdsMentioningAsync(artworkId));
    }

    [Fact]
    public async Task DeletingAnArtworkKeepsThePostsThatEmbedIt()
    {
        var artworkId = await AddArtworkAsync();
        var postId = await AddPostAsync(BodyEmbedding(artworkId), PostStatus.Published);

        await _artworks.DeleteAsync(artworkId);

        Assert.NotNull(await _posts.GetAsync(postId));
    }

    [Fact]
    public async Task DeletingAPostRemovesItsMentions()
    {
        var artworkId = await AddArtworkAsync();
        var postId = await AddPostAsync(BodyEmbedding(artworkId), PostStatus.Published);

        await _posts.DeleteAsync(postId);

        Assert.Empty(await GetIdsMentioningAsync(artworkId));
    }
}
