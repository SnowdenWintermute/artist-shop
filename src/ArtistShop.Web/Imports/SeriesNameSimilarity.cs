using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Imports;

// Name is a series the file would create; MatchedName is the existing series or other new name
// it resembles
public record SeriesNameLikeness(string Name, string MatchedName);

public record SeriesNameChecks(
    IReadOnlyList<SeriesNameLikeness> SlugCollisions,
    IReadOnlyList<SeriesNameLikeness> NearDuplicates
);

// Two ways a new series name can be a typo of another. A shared slug is certain -- the database
// refuses it -- and a small edit distance is a guess worth showing the artist
public static class SeriesNameSimilarity
{
    private const int MaximumEditDistance = 2;

    // "Blue" and "Blur" are two words, not one misspelt. A typo only becomes the likelier reading
    // once there is enough name for it to hide in
    private const int ShortestComparedName = 8;

    public static SeriesNameChecks Check(
        IReadOnlyList<string> newNames,
        IReadOnlyList<string> existingNames
    )
    {
        var slugCollisions = new List<SeriesNameLikeness>();
        var nearDuplicates = new List<SeriesNameLikeness>();

        for (var index = 0; index < newNames.Count; index++)
        {
            var name = newNames[index];

            // earlier new names only, so a pair is reported once
            foreach (var other in existingNames.Concat(newNames.Take(index)))
            {
                if (ShareASlug(name, other))
                {
                    slugCollisions.Add(new SeriesNameLikeness(name, other));
                }
                else if (AreNearDuplicates(name, other))
                {
                    nearDuplicates.Add(new SeriesNameLikeness(name, other));
                }
            }
        }

        return new SeriesNameChecks(slugCollisions, nearDuplicates);
    }

    // the slug is the whole difference the site can see: same address, same series as far as
    // unique_series_slug is concerned
    public static bool ShareASlug(string name, string other) =>
        SeriesSlug.FromName(name).Value == SeriesSlug.FromName(other).Value;

    public static bool AreNearDuplicates(string name, string other) =>
        Math.Max(name.Length, other.Length) >= ShortestComparedName
        && EditDistance(name.ToLowerInvariant(), other.ToLowerInvariant()) <= MaximumEditDistance;

    // how many single-character insertions, deletions or substitutions turn one into the other
    public static int EditDistance(string left, string right)
    {
        // two rows rather than the whole grid: a cell only ever reads the row above it
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1);
                var deletion = previous[column] + 1;
                var insertion = current[column - 1] + 1;

                current[column] = Math.Min(substitution, Math.Min(deletion, insertion));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
