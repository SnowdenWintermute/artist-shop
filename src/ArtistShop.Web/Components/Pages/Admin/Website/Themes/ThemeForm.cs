using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.Themes;

// The theme open on the Theme page: its name and every role, in ColorRoles' order. Form posts
// create this, so it keeps a single public constructor
public class ThemeForm : ServerValidatedForm, IValidatableObject
{
    [StringLength(ArtistShopLimits.ThemeNameMaximumLength)]
    public string? Name { get; set; }

    public List<ColorChoiceInput> Roles { get; set; } = [];

    // set by Save, which keeps the changes in the theme open; any other submit, like picking a
    // colour, only previews
    public bool Save { get; set; }

    // set by Save as new theme, which keeps them in a new one
    public bool SaveAsNew { get; set; }

    // set by a role's Reset, which puts that role back as the open theme has it saved
    public ColorRole? Reset { get; set; }

    // set by Overwrite with derived colors, which derives every role that can be
    public bool DeriveAll { get; set; }

    public static ThemeForm From(string name, ThemeColors colors) =>
        new() { Name = name, Roles = [.. ColorRoles.All.Select(definition => ColorChoiceInput.From(definition.Role, colors))] };

    public void AddNameTakenError(string name) => AddServerError(nameof(Name), $"Another theme is already called \"{name}\".");

    public string ToName() => (Name ?? "").Trim();

    // a role the post left out, or sent a colour for that can't be read, keeps its saved colour, and
    // Validate says why
    public ThemeColors ToColors(ThemeColors saved)
    {
        var chosen = new Dictionary<ColorRole, RgbColor>();

        foreach (var definition in ColorRoles.All)
        {
            if (ChosenColor(definition, saved) is { } color)
            {
                chosen.Add(definition.Role, color);
            }
        }

        return new ThemeColors(chosen);
    }

    private RgbColor? ChosenColor(ColorRoleDefinition definition, ThemeColors saved)
    {
        var choice = Roles.FirstOrDefault(choice => choice.Role == definition.Role);

        if (choice is null || definition.Role == Reset)
        {
            return saved.ChosenFor(definition.Role);
        }

        if (DeriveAll && !definition.IsBase)
        {
            return null;
        }

        return choice.IsChosen ? choice.ChosenColor() ?? saved.ChosenFor(definition.Role) : null;
    }

    // only a hand-written post gets the roles wrong, since the page's own inputs can't
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

        if ((Save || SaveAsNew) && ToName() == "")
        {
            yield return new ValidationResult("Give the theme a name.", [nameof(Name)]);
        }
    }
}

public class ColorChoiceInput
{
    public ColorRole Role { get; set; }

    // not chosen: derived from the base roles. A base role is always chosen
    public bool IsDerived { get; set; }

    // what the colour picker holds, "#rrggbb"
    public string? Color { get; set; }

    // the colour the page showed, so a role whose picker now holds another was just picked
    public string? ShownColor { get; set; }

    [Range(0, 100)]
    public int OpacityPercent { get; set; }

    public int ShownOpacityPercent { get; set; }

    public static ColorChoiceInput From(ColorRole role, ThemeColors colors)
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
        ColorRoles.For(Role).IsBase
        || !IsDerived
        || Color != ShownColor
        || (ColorRoles.For(Role).AllowsOpacity && OpacityPercent != ShownOpacityPercent);

    // null when the picker sent something that isn't an opaque colour
    public RgbColor? ChosenColor() =>
        RgbColor.TryParse(Color, out var color) && color.IsOpaque
            ? ColorRoles.For(Role).AllowsOpacity ? color.WithOpacityPercent(OpacityPercent) : color
            : null;
}
