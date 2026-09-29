using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Imports;

// one post the page's script is sent: its place in the plan, and its files to upload
public record PostImportBatch(int Index, IReadOnlyList<string> FileIds);

// A post import under way on a page: the planned posts, the uploads the page's script reported and
// each post's result. The post import page and the whole-website import both run one
public class PostImportRun(IReadOnlyList<PostImportItem> items)
{
    public const string SomethingWentWrongProblem = "Something went wrong saving this post. Press Import to try again.";

    private readonly Dictionary<string, ImageUploadResult> _uploadsByFileId = [];
    private readonly Dictionary<int, string?> _results = [];

    public IReadOnlyList<PostImportItem> Items => items;

    // by the item's place in Items: null once imported, otherwise why it wasn't
    public IReadOnlyDictionary<int, string?> Results => _results;

    public int ImportedCount => _results.Values.Count(problem => problem is null);

    // the posts to add that aren't in yet, including those that failed, so a run tries them again
    public List<int> IndexesToImport() =>
        [
            .. Enumerable
                .Range(0, items.Count)
                .Where(index => items[index].Outcome is PostImportOutcome.WillAdd && !(_results.TryGetValue(index, out var problem) && problem is null)),
        ];

    // what the script is sent. A failure from an earlier run is tried again, so it counts as not
    // done yet
    public IReadOnlyList<PostImportBatch> Start()
    {
        var indexes = IndexesToImport();

        foreach (var index in indexes)
        {
            _results.Remove(index);
        }

        return [.. indexes.Select(index => new PostImportBatch(index, [.. items[index].FileIdsByPlaceholder.Values]))];
    }

    // what the upload endpoint answered, as the browser reported it
    public void FileUploaded(string fileId, ImageUploadResult result) => _uploadsByFileId[fileId] = result;

    public void PostFailed(int index, string problem) => _results[index] = problem;

    // Saved before the script goes on to the next post, since it waits for this. The database
    // failing, say, is logged and shown: thrown back to the script, it would end the run with no
    // word to the artist
    public async Task PostUploadedAsync(int index, PostRepository posts, ImageStorage imageStorage, ILogger logger)
    {
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        try
        {
            _results[index] = await PostImporter.SaveAsync(items[index], _uploadsByFileId, posts, imageStorage);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Saving an imported post failed.");
            _results[index] = SomethingWentWrongProblem;
        }
    }
}
