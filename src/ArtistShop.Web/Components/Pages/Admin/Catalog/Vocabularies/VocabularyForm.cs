using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public class VocabularyForm : ServerValidatedForm
{
    private readonly HashSet<ArtworkTypeId> _artworkTypeIds = [];
    private string? _savedName;
    private readonly HashSet<ArtworkTypeId> _savedArtworkTypeIds = [];

    // Loads into this form rather than making a new one, so the EditContext stays the same:
    // EditForm rebuilds every element inside it when handed a new one, which takes the focus out
    // of the field the artist just pressed Enter in
    public void Load(VocabularyWithArtworkTypes saved)
    {
        Name = saved.Name.Value;
        _savedName = saved.Name.Value;
        _artworkTypeIds.Clear();
        _artworkTypeIds.UnionWith(saved.ArtworkTypeIds);
        _savedArtworkTypeIds.Clear();
        _savedArtworkTypeIds.UnionWith(saved.ArtworkTypeIds);
    }

    [Required]
    [StringLength(ArtistShopLimits.VocabularyNameMaximumLength)]
    public string? Name { get; set; }

    public IReadOnlySet<ArtworkTypeId> ArtworkTypeIds => _artworkTypeIds;

    public bool HasChanges =>
        Name != _savedName || !_artworkTypeIds.SetEquals(_savedArtworkTypeIds);

    public void ToggleArtworkType(ArtworkTypeId artworkTypeId)
    {
        if (!_artworkTypeIds.Remove(artworkTypeId))
        {
            _artworkTypeIds.Add(artworkTypeId);
        }
    }

    public bool WasUnselected(ArtworkTypeId artworkTypeId) =>
        _savedArtworkTypeIds.Contains(artworkTypeId) && !_artworkTypeIds.Contains(artworkTypeId);

    public void AddNameTakenError(string name)
    {
        AddServerError(nameof(Name), $"A vocabulary called \"{name}\" already exists.");
    }

    public VocabularyName ToVocabularyName() => new(Unwrap.Value(Name).Trim());
}
