using System.Runtime.CompilerServices;

namespace ArtistShop.Web.Utilities;

public static class Unwrap
{
    public static T Value<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null
    )
        where T : struct =>
        value ?? throw new InvalidOperationException($"{expression} has no value.");

    public static T Value<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null
    )
        where T : class =>
        value ?? throw new InvalidOperationException($"{expression} has no value.");
}
