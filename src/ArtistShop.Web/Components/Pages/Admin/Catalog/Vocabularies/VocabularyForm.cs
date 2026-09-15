using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Components.Forms;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public class VocabularyForm
{
    private readonly HashSet<ShopItemTypeId> _shopItemTypeIds;
    private readonly string? _savedName;
    private readonly HashSet<ShopItemTypeId> _savedShopItemTypeIds;

    // for errors only the database can find, like a duplicate name
    private readonly ValidationMessageStore _serverMessages;

    // @QUESTION what is this? look like anonymous field of the same type as the parent class? or this is the constructor declared below some fields?
    private VocabularyForm(string? name, IEnumerable<ShopItemTypeId> shopItemTypeIds)
    {
        Name = name;
        _savedName = name;
        _shopItemTypeIds = [.. shopItemTypeIds];
        _savedShopItemTypeIds = [.. shopItemTypeIds];

        EditContext = new EditContext(this);
        _serverMessages = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += (_, _) => _serverMessages.Clear();
        EditContext.OnFieldChanged += (_, changed) =>
            _serverMessages.Clear(changed.FieldIdentifier);
    }

    public static VocabularyForm ForNew() => new(null, []);

    public static VocabularyForm ForExisting(VocabularyWithShopItemTypes vocabulary) =>
        new(vocabulary.Name.Value, vocabulary.ShopItemTypeIds);

    public EditContext EditContext { get; }

    [Required]
    [StringLength(CatalogLimits.VocabularyNameMaximumLength)]
    public string? Name { get; set; }

    public IReadOnlySet<ShopItemTypeId> ShopItemTypeIds => _shopItemTypeIds;

    public bool HasChanges =>
        Name != _savedName || !_shopItemTypeIds.SetEquals(_savedShopItemTypeIds);

    public void ToggleShopItemType(ShopItemTypeId shopItemTypeId)
    {
        if (!_shopItemTypeIds.Remove(shopItemTypeId))
        {
            _shopItemTypeIds.Add(shopItemTypeId);
        }
    }

    public bool WasUnselected(ShopItemTypeId shopItemTypeId) =>
        _savedShopItemTypeIds.Contains(shopItemTypeId)
        && !_shopItemTypeIds.Contains(shopItemTypeId);

    public void AddNameTakenError(string name)
    {
        _serverMessages.Add(() => Name, $"A vocabulary called \"{name}\" already exists."); // Possible null reference return.
        EditContext.NotifyValidationStateChanged();
    }

    public VocabularyName ToVocabularyName() => new(Unwrap.Value(Name).Trim());
}
