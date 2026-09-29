using System.Globalization;
using System.Text;

namespace ArtistShop.Web.Domain;

public static class ArtistShopSlug
{
    public static string FromName(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                slug.Append(char.ToLowerInvariant(character));
            }
            else if (slug.Length > 0 && slug[^1] is not '-')
            {
                slug.Append('-');
            }
        }

        if (slug.Length > ArtistShopLimits.BaseSlugMaximumLength)
        {
            slug.Length = ArtistShopLimits.BaseSlugMaximumLength;
        }

        return slug.ToString().Trim('-');
    }

    // one a slug could be: lower case letters, numbers and single dashes between them, no longer
    // than the column. It may be longer than FromName makes one, as a numbered slug is
    public static bool IsWellFormed(string slug) =>
        slug.Length is > 0 and <= ArtistShopLimits.SlugMaximumLength
        && slug.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-')
        && !slug.StartsWith('-')
        && !slug.EndsWith('-')
        && !slug.Contains("--", StringComparison.Ordinal);
}
