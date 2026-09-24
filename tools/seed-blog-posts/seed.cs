// Adds filler blog posts to the dev database, so /posts has enough to page through. Run from the
// repo root after `. ./env.sh`, with the app started at least once on the current code, since that
// is what installs the database functions this uses:
//
//   dotnet run tools/seed-blog-posts/seed.cs                  25 published posts, three days apart
//   dotnet run tools/seed-blog-posts/seed.cs -- --count 40
//   dotnet run tools/seed-blog-posts/seed.cs -- --clean       deletes them again
//
// Every filler post's slug starts with "filler-post-", which is how --clean finds them and how a
// second run skips the ones already there. Their dates go back from yesterday, so real posts
// written today stay at the top of the list.

#:project ../../src/ArtistShop.Web/ArtistShop.Web.csproj
// a file-based program is built for native AOT unless told otherwise, and Dapper, which the
// repository uses, generates code as it runs
#:property PublishAot=false

using System.Text.Json;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;

const string SlugPrefix = "filler-post-";
const int DaysApart = 3;

var count = 25;
var clean = false;

for (var index = 0; index < args.Length; index += 1)
{
    switch (args[index])
    {
        case "--count" when index + 1 < args.Length && int.TryParse(args[index + 1], out var parsed) && parsed > 0:
            count = parsed;
            index += 1;
            break;
        case "--clean":
            clean = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown option: {args[index]}");
            return 1;
    }
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ArtistShop");

if (string.IsNullOrEmpty(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__ArtistShop isn't set. Run `. ./env.sh` first.");
    return 1;
}

await using var dataSource = ShopDataSource.Create(connectionString);
var posts = new PostRepository(dataSource);

var existing = (await posts.GetAllAsync()).Where(post => post.Slug.Value.StartsWith(SlugPrefix)).ToList();

if (clean)
{
    foreach (var post in existing)
    {
        await posts.DeleteAsync(post.Id);
    }

    Console.WriteLine($"Deleted {existing.Count} filler posts.");
    return 0;
}

string[] firstWords = ["Lantern", "Bridge", "Tunnel", "Crystal", "Harbour", "Moonlit", "Silent", "Distant", "Frozen", "Hidden"];
string[] lastWords = ["Path", "Waterfall", "Chapel", "Market", "Ruins", "Crossing", "Watchtower", "Gate", "Hollow", "Spire"];

string[] sentences =
[
    "The light was low by the time I reached the ridge, and everything below had turned the colour of old brass.",
    "I went back three times before the fog lifted enough to see the far side of the valley.",
    "Most of the week went into underpainting, which never looks like much but decides everything that comes after.",
    "There's a particular blue the sky goes just after sunset that I've never managed to mix twice.",
    "A few people asked about the frames, so here is how they're made and why they're the width they are.",
    "The market was busier than I expected, and I ended up sketching the crowd rather than the stalls.",
    "Snow changes the whole composition: the shapes you relied on disappear and new ones take their place.",
    "I started this one in the spring and only finished it last week, after leaving it facing the wall for months.",
    "Nothing in this painting is quite where it is in real life, but it's closer to how the place felt.",
    "The paint dried faster than I could work it, which forced a looser hand than usual.",
];

// A heading on some posts, one to four paragraphs, and every seventh post a single short line, so
// the list shows excerpts of different lengths and one that ends well before its three lines
string BodyFor(int number)
{
    var ops = new List<object>();

    if (number % 7 == 0)
    {
        ops.Add(new { insert = "A short note this time.\n" });
        return JsonSerializer.Serialize(new { ops });
    }

    if (number % 3 == 0)
    {
        ops.Add(new { insert = "Notes from the week" });
        ops.Add(new { insert = "\n", attributes = new { header = 2 } });
    }

    var paragraphCount = number % 4 + 1;

    for (var paragraph = 0; paragraph < paragraphCount; paragraph += 1)
    {
        var first = sentences[(number + paragraph) % sentences.Length];
        var second = sentences[(number * 3 + paragraph) % sentences.Length];
        ops.Add(new { insert = $"{first} {second}\n" });
    }

    return JsonSerializer.Serialize(new { ops });
}

var added = 0;
var skipped = 0;

for (var number = 1; number <= count; number += 1)
{
    var title = $"Filler post {number}: {firstWords[number % firstWords.Length]} {lastWords[number / firstWords.Length % lastWords.Length]}";
    var slug = PostSlug.FromTitle(title);

    if (!slug.Value.StartsWith(SlugPrefix))
    {
        throw new InvalidOperationException($"\"{title}\" made the slug {slug.Value}, which --clean wouldn't find.");
    }

    if (existing.Any(post => post.Slug == slug))
    {
        skipped += 1;
        continue;
    }

    var id = await posts.AddAsync(new PostTitle(title), slug, new PostBody(BodyFor(number)), PostStatus.Published);

    // Publishing stamps the post with now. Written here directly, since nothing in the app sets a
    // date: the highest number is the newest, yesterday, and each one before it DaysApart older
    await using var command = dataSource.CreateCommand("UPDATE posts SET published_at = $1 WHERE id = $2");
    command.Parameters.AddWithValue(DateTime.UtcNow.AddDays(-1 - (count - number) * DaysApart));
    command.Parameters.AddWithValue(id.Value);
    await command.ExecuteNonQueryAsync();

    added += 1;
}

Console.WriteLine($"Added {added} filler posts{(skipped > 0 ? $", skipped {skipped} already there" : "")}.");
return 0;
