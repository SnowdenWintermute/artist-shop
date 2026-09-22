using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public class VocabularyForm : ServerValidatedForm
{
    private readonly HashSet<ArtworkTypeId> _artworkTypeIds;
    private readonly string? _savedName;
    private readonly HashSet<ArtworkTypeId> _savedArtworkTypeIds;

    private VocabularyForm(string? name, IEnumerable<ArtworkTypeId> artworkTypeIds)
    {
        Name = name;
        _savedName = name;
        _artworkTypeIds = [.. artworkTypeIds];
        _savedArtworkTypeIds = [.. artworkTypeIds];
    }

    public static VocabularyForm ForNew() => new(null, []);

    public static VocabularyForm ForExisting(VocabularyWithArtworkTypes vocabulary) =>
        new(vocabulary.Name.Value, vocabulary.ArtworkTypeIds);

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
