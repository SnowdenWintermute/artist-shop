using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkTypes;

public class WorkTypeForm : ServerValidatedForm
{
    private readonly HashSet<WorkField> _fields = [];
    private string? _savedName;
    private readonly HashSet<WorkField> _savedFields = [];

    // Loads into this form rather than making a new one, so the EditContext stays the same:
    // EditForm rebuilds every element inside it when handed a new one, which takes the focus out
    // of the field the artist just pressed Enter in
    public void Load(WorkTypeWithFields saved)
    {
        Name = saved.Name.Value;
        _savedName = saved.Name.Value;
        _fields.Clear();
        _fields.UnionWith(saved.Fields);
        _savedFields.Clear();
        _savedFields.UnionWith(saved.Fields);
    }

    [Required]
    [StringLength(ArtistShopLimits.WorkTypeNameMaximumLength)]
    public string? Name { get; set; }

    public IReadOnlySet<WorkField> Fields => _fields;

    public bool HasChanges => Name != _savedName || !_fields.SetEquals(_savedFields);

    public void ToggleField(
        WorkFieldDefinition toggled,
        IEnumerable<WorkFieldDefinition> fieldDefinitions
    )
    {
        if (_fields.Add(toggled.Field))
        {
            return;
        }

        _fields.Remove(toggled.Field);

        // fields that need this one can't stay on without it
        foreach (var definition in fieldDefinitions.Where(definition =>
            definition.HasRequirement && definition.RequiredField == toggled.Field))
        {
            _fields.Remove(definition.Field);
        }
    }

    public bool WasSwitchedOff(WorkField field) =>
        _savedFields.Contains(field) && !_fields.Contains(field);

    public void AddNameTakenError(string name)
    {
        AddServerError(nameof(Name), $"Another type is already called \"{name}\".");
    }

    public WorkTypeName ToWorkTypeName() => new(Unwrap.Value(Name).Trim());
}
