using System.Globalization;

namespace ArtistShop.Web.Utilities;

// A price is an amount in a currency, the way a dimension is a number of centimetres, but the
// column holds only the amount. Until the currency belongs to the value, and the visitor's own
// currency is worked out, every price on the site is dollars, spelled here and nowhere else
public static class PriceText
{
    public static string Of(decimal price) =>
        price == decimal.Truncate(price)
            ? price.ToString("$#,##0", CultureInfo.InvariantCulture)
            : price.ToString("$#,##0.00", CultureInfo.InvariantCulture);
}
