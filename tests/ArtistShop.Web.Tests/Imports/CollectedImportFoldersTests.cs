using ArtistShop.Web.Components.Forms.FileUpload;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

// A chosen folder's files read as the import pages read them, with each file's text given here
// rather than by the browser
public sealed class CollectedImportFoldersTests
{
    private readonly Dictionary<string, string> _texts = [];
    private readonly List<string> _read = [];

    // a file with this text, its path as its id
    private CollectedFile File(string path, string text = "", long? size = null)
    {
        _texts[path] = text;
        return new CollectedFile(path, path, size ?? text.Length, Type: "");
    }

    private Task<string> ReadTextAsync(CollectedFile file, long maximumBytes)
    {
        _read.Add(file.Path);
        return Task.FromResult(_texts[file.Path]);
    }

    [Fact]
    public async Task TheDownloadIsTheFolderWithTheShallowestWebsiteJson()
    {
        CollectedFile[] files =
        [
            File("/moving/site/website.json", "outer"),
            File("/moving/site/catalog/artworkTypes.csv", "types"),
            File("/moving/site/catalog/vocabularies.csv", "vocabularies"),
            File("/moving/site/catalog/artworks/Painting.csv", "paintings"),
            File("/moving/site/images/images.csv", "list"),
            File("/moving/site/images/Painting/Dawn.jpg"),
            // an older download kept inside it
            File("/moving/site/old/website.json", "inner"),
            File("/moving/site/old/catalog/artworkTypes.csv", "old types"),
            // beside the download, not in it
            File("/moving/notes.txt"),
        ];

        var folder = await CollectedImportFolders.WebsiteAsync(files, ReadTextAsync);

        Assert.Equal("outer", folder.Manifest);
        Assert.Equal("types", folder.ArtworkTypesCsv);
        Assert.Equal("vocabularies", folder.VocabulariesCsv);
        Assert.Equal(new Dictionary<string, string> { ["Painting.csv"] = "paintings" }, folder.ArtworkCsvsByFileName);
        Assert.Equal("list", folder.ImageList);
        Assert.Equal(
            new Dictionary<string, string> { ["images.csv"] = "/moving/site/images/images.csv", ["Painting/Dawn.jpg"] = "/moving/site/images/Painting/Dawn.jpg" },
            folder.ImageFileIdsByPath
        );
    }

    [Fact]
    public async Task WithoutAWebsiteJsonNothingIsRead()
    {
        var folder = await CollectedImportFolders.WebsiteAsync([File("/site/catalog/artworkTypes.csv", "types")], ReadTextAsync);

        Assert.Null(folder.Manifest);
        Assert.Null(folder.ArtworkTypesCsv);
        Assert.Empty(_read);
    }

    // only a folder directly in posts/ is a post; one too large to be a post.json reads as empty
    [Fact]
    public async Task APostIsAFolderInPostsWithItsImagesBesideIt()
    {
        CollectedFile[] files =
        [
            File("/site/website.json", "{}"),
            File("/site/posts/morning/post.json", "morning"),
            File("/site/posts/morning/image-1.webp"),
            File("/site/posts/huge/post.json", "", size: CollectedImportFolders.MaximumPostFileBytes + 1),
            File("/site/posts/morning/drafts/post.json", "nested"),
        ];

        var folder = await CollectedImportFolders.WebsiteAsync(files, ReadTextAsync);

        Assert.Equal(["morning", "huge"], folder.Posts.Select(post => post.Name));
        var morning = folder.Posts[0];
        Assert.Equal("morning", morning.Json);
        Assert.Equal(new Dictionary<string, string> { ["image-1.webp"] = "/site/posts/morning/image-1.webp" }, morning.FileIdsByName);
        Assert.Equal("", folder.Posts[1].Json);
        Assert.DoesNotContain("/site/posts/huge/post.json", _read);
    }

    // the post import page takes a post.json in any folder
    [Fact]
    public async Task ThePostImportTakesEveryFolderWithAPostJson()
    {
        CollectedFile[] files = [File("/a/post.json", "a"), File("/a/b/post.json", "b"), File("/a/b/photo.jpg")];

        var posts = await CollectedImportFolders.PostsAsync([.. files.Select(file => (file.Path, file))], _ => true, ReadTextAsync);

        Assert.Equal(["a", "b"], posts.Select(post => post.Name));
        Assert.Empty(posts[0].FileIdsByName);
        Assert.Equal(["photo.jpg"], posts[1].FileIdsByName.Keys);
    }
}
