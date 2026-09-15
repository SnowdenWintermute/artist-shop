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

        if (slug.Length > CatalogLimits.BaseSlugMaximumLength)
        {
            slug.Length = CatalogLimits.BaseSlugMaximumLength;
        }

        return slug.ToString().Trim('-');
    }
}
