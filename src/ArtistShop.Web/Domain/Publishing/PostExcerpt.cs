namespace ArtistShop.Web.Domain.Publishing;

using System.Text.RegularExpressions;

// The opening words of a post for the blog list: its text as plain text, embeds skipped, cut on a
// whole word. The list clamps it to a few lines, so this only needs to be enough to fill them
public static partial class PostExcerpt
{
    // about three lines of the post column, with some to spare for a wide screen
    public const int MaximumLength = 300;

    // null when the post has no text at all, such as one that is only pictures
    public static string? From(PostDocument document)
    {
        var text = "";

        foreach (var piece in TextPieces(document))
        {
            text = text is "" ? piece : $"{text} {piece}";

            if (text.Length > MaximumLength)
            {
                return CutOnWord(text);
            }
        }

        return text is "" ? null : text;
    }

    // each block's text on its own, with runs of spaces and line breaks made single spaces
    private static IEnumerable<string> TextPieces(PostDocument document) =>
        document
            .Blocks.SelectMany(TextRunsOf)
            .Select(runs => WhitespacePattern().Replace(string.Concat(runs.Select(run => run.Text)), " ").Trim())
            .Where(piece => piece is not "");

    // a list gives one piece per item; an embed gives none
    private static IEnumerable<IReadOnlyList<PostText>> TextRunsOf(PostBlock block)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                yield return paragraph.Text;
                break;
            case HeadingBlock heading:
                yield return heading.Text;
                break;
            case BlockquoteBlock blockquote:
                yield return blockquote.Text;
                break;
            case ListBlock list:
                foreach (var item in list.Items)
                {
                    yield return item;
                }
                break;
        }
    }

    // at the last space that fits, or mid-word when a single word is longer than the whole excerpt
    private static string CutOnWord(string text)
    {
        var lastSpace = text.LastIndexOf(' ', MaximumLength);
        var cut = lastSpace > 0 ? text[..lastSpace] : text[..MaximumLength];

        return $"{cut.TrimEnd()}…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
