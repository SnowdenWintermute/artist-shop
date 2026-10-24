using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Works;

// a [Range] whose limits depend on the unit the form takes lengths in
[AttributeUsage(AttributeTargets.Property)]
public sealed class LengthRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not WorkForm form)
        {
            throw new InvalidOperationException($"{validationContext.ObjectType.Name} isn't a {nameof(WorkForm)}.");
        }

        if (value is not decimal length)
        {
            return ValidationResult.Success;
        }

        // 0.01 of either unit is at least the centimetres' minimum
        var minimum = ArtistShopLimits.MinimumDimensionCm;
        var maximum = Lengths.Maximum(form.LengthUnit);

        if (length >= minimum && length <= maximum)
        {
            return ValidationResult.Success;
        }

        var message = string.Create(CultureInfo.InvariantCulture, $"Use a number from {minimum} to {maximum}.");
        return new ValidationResult(message, validationContext.MemberName is string member ? [member] : null);
    }
}
