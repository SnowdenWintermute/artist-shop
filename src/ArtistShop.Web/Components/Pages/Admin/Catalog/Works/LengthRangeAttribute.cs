using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Works;

// a [Range] whose limits depend on the unit the form takes lengths in
[AttributeUsage(AttributeTargets.Property)]
public sealed class LengthRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not decimal length || validationContext.ObjectInstance is not WorkForm form)
        {
            return ValidationResult.Success;
        }

        var maximum = Lengths.Maximum(form.LengthUnit);

        if (length >= Lengths.Minimum && length <= maximum)
        {
            return ValidationResult.Success;
        }

        var message = string.Create(CultureInfo.InvariantCulture, $"Use a number from {Lengths.Minimum} to {maximum}.");
        return new ValidationResult(message, validationContext.MemberName is string member ? [member] : null);
    }
}
