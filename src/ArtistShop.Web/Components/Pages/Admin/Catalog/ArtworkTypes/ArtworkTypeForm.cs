using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.ArtworkTypes;

public class ArtworkTypeForm : ServerValidatedForm
{
    private readonly HashSet<ArtworkField> _fields;
    private readonly string? _savedName;
    private readonly HashSet<ArtworkField> _savedFields;

    private ArtworkTypeForm(string? name, IEnumerable<ArtworkField> fields)
    {
        Name = name;
        _savedName = name;
        _fields = [.. fields];
        _savedFields = [.. fields];
    }

    public static ArtworkTypeForm ForNew() => new(null, []);

    public static ArtworkTypeForm ForExisting(ArtworkTypeWithFields artworkType) =>
        new(artworkType.Name.Value, artworkType.Fields);

    [Required]
    [StringLength(CatalogLimits.ArtworkTypeNameMaximumLength)]
    public string? Name { get; set; }

    public IReadOnlySet<ArtworkField> Fields => _fields;

    public bool HasChanges => Name != _savedName || !_fields.SetEquals(_savedFields);

    public void ToggleField(
        ArtworkFieldDefinition toggled,
        IEnumerable<ArtworkFieldDefinition> fieldDefinitions
    )
    {
        if (_fields.Add(toggled.Field))
        {
            return;
        }

        // a field requires itself, so this switches it off along with the fields that need it
        foreach (var definition in fieldDefinitions.Where(definition => definition.RequiredField == toggled.Field))
        {
            _fields.Remove(definition.Field);
        }
    }

    public bool WasSwitchedOff(ArtworkField field) =>
        _savedFields.Contains(field) && !_fields.Contains(field);

    public void AddNameTakenError(string name)
    {
        AddServerError(nameof(Name), $"An artwork type called \"{name}\" already exists.");
    }

    public ArtworkTypeName ToArtworkTypeName() => new(Unwrap.Value(Name).Trim());
}
