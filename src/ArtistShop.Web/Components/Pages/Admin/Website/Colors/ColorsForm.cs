using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.Colors;

// Every role, in ColorRoles' order. Form posts create this, so it keeps a single public constructor
public class ColorsForm : IValidatableObject
{
    public List<ColorChoiceInput> Roles { get; set; } = [];

    // set by the Save button; any other submit, like picking a colour, only previews
    public bool Save { get; set; }

    // set by a role's Reset button, which puts that role back to its default
    public ColorRole? Reset { get; set; }

    // set by Reset all, which puts every role back
    public bool ResetAll { get; set; }

    public static ColorsForm From(SiteColors colors) =>
        new() { Roles = [.. ColorRoles.All.Select(definition => ColorChoiceInput.From(definition.Role, colors))] };

    // a role whose colour can't be read is left derived, and Validate says why
    public SiteColors ToColors()
    {
        var chosen = new Dictionary<ColorRole, RgbColor>();

        if (ResetAll)
        {
            return SiteColors.Default;
        }

        foreach (var choice in Roles)
        {
            if (Enum.IsDefined(choice.Role) && choice.Role != Reset && choice.IsChosen && choice.ChosenColor() is { } color)
            {
                chosen.TryAdd(choice.Role, color);
            }
        }

        return new SiteColors(chosen);
    }

    // only a hand-written post gets these wrong, since the page's own inputs can't
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Roles.Select(choice => choice.Role).Order().SequenceEqual(ColorRoles.All.Select(definition => definition.Role).Order()))
        {
            yield return new ValidationResult("The colours sent don't match this page. Reload it and try again.");
        }
        else if (Roles.Any(choice => choice.IsChosen && choice.ChosenColor() is null))
        {
            yield return new ValidationResult("A colour sent isn't one this page could have picked. Reload it and try again.");
        }
    }
}

public class ColorChoiceInput
{
    public ColorRole Role { get; set; }

    // not chosen: derived from the base roles, or for a base role, the platform's colour
    public bool IsDerived { get; set; }

    // what the colour picker holds, "#rrggbb"
    public string? Color { get; set; }

    // the colour the page showed, so a role whose picker now holds another was just picked
    public string? ShownColor { get; set; }

    [Range(0, 100)]
    public int OpacityPercent { get; set; }

    public int ShownOpacityPercent { get; set; }

    public static ColorChoiceInput From(ColorRole role, SiteColors colors)
    {
        var resolved = colors.Resolve(role);
        // the picker takes no opacity; the slider beside it carries that
        var hex = (resolved with { Alpha = 255 }).Hex;

        return new()
        {
            Role = role,
            IsDerived = colors.ChosenFor(role) is null,
            Color = hex,
            ShownColor = hex,
            OpacityPercent = resolved.OpacityPercent,
            ShownOpacityPercent = resolved.OpacityPercent,
        };
    }

    public bool IsChosen =>
        !IsDerived
        || Color != ShownColor
        || (ColorRoles.For(Role).AllowsOpacity && OpacityPercent != ShownOpacityPercent);

    // null when the picker sent something that isn't an opaque colour
    public RgbColor? ChosenColor() =>
        RgbColor.TryParse(Color?.ToLowerInvariant(), out var color) && color.IsOpaque
            ? ColorRoles.For(Role).AllowsOpacity ? color.WithOpacityPercent(OpacityPercent) : color
            : null;
}
