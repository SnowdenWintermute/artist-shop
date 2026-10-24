using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public class VocabularyForm : ServerValidatedForm
{
    private readonly HashSet<WorkTypeId> _workTypeIds = [];
    private string? _savedName;
    private bool _savedIsMutuallyExclusive;
    private readonly HashSet<WorkTypeId> _savedWorkTypeIds = [];

    // Loads into this form rather than making a new one, so the EditContext stays the same:
    // EditForm rebuilds every element inside it when handed a new one, which takes the focus out
    // of the field the artist just pressed Enter in. An unsaved name edit is kept, as a checkbox's save
    // reloads the page without saving the name
    public void Load(VocabularyWithWorkTypes saved)
    {
        if (Name == _savedName)
        {
            Name = saved.Name.Value;
        }

        _savedName = saved.Name.Value;
        IsMutuallyExclusive = saved.IsMutuallyExclusive;
        _savedIsMutuallyExclusive = saved.IsMutuallyExclusive;
        _workTypeIds.Clear();
        _workTypeIds.UnionWith(saved.WorkTypeIds);
        _savedWorkTypeIds.Clear();
        _savedWorkTypeIds.UnionWith(saved.WorkTypeIds);
    }

    [Required]
    [StringLength(ArtistShopLimits.VocabularyNameMaximumLength)]
    public string? Name { get; set; }

    public bool IsMutuallyExclusive { get; set; }

    public bool BecameMutuallyExclusive => IsMutuallyExclusive && !_savedIsMutuallyExclusive;

    public IReadOnlySet<WorkTypeId> WorkTypeIds => _workTypeIds;

    public bool NameHasChanges => Name != _savedName;

    public void ToggleWorkType(WorkTypeId workTypeId)
    {
        _workTypeIds.Toggle(workTypeId);
    }

    // after the artist cancels a checkbox change that would take terms off works
    public void RevertCheckboxes()
    {
        IsMutuallyExclusive = _savedIsMutuallyExclusive;
        _workTypeIds.Clear();
        _workTypeIds.UnionWith(_savedWorkTypeIds);
    }

    public bool WasUnselected(WorkTypeId workTypeId) =>
        _savedWorkTypeIds.Contains(workTypeId) && !_workTypeIds.Contains(workTypeId);

    public void AddNameTakenError(string name)
    {
        AddServerError(nameof(Name), $"A vocabulary called \"{name}\" already exists.");
    }

    public VocabularyName ToVocabularyName() => new(Unwrap.Value(Name).Trim());
}
