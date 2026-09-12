using System.Collections;
using System.ComponentModel.DataAnnotations;

namespace ArtistShop.Web.Utilities;

public sealed class NonEmptyAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is ICollection { Count: > 0 };
}
