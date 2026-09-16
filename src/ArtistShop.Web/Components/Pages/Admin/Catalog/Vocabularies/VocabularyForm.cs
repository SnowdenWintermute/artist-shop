using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Components.Forms;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.Vocabularies;

public class VocabularyForm
{
    private readonly HashSet<ArtworkTypeId> _artworkTypeIds;
    private readonly string? _savedName;
    private readonly HashSet<ArtworkTypeId> _savedArtworkTypeIds;

    // for errors only the database can find, like a duplicate name
    private readonly ValidationMessageStore _serverMessages;

    // @QUESTION what is this? look like anonymous field of the same type as the parent class? or this is the constructor declared below some fields?
    private VocabularyForm(string? name, IEnumerable<ArtworkTypeId> artworkTypeIds)
    {
        Name = name;
        _savedName = name;
        _artworkTypeIds = [.. artworkTypeIds];
        _savedArtworkTypeIds = [.. artworkTypeIds];

        EditContext = new EditContext(this);
        _serverMessages = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += (_, _) => _serverMessages.Clear();
        EditContext.OnFieldChanged += (_, changed) =>
            _serverMessages.Clear(changed.FieldIdentifier);
    }

    public static VocabularyForm ForNew() => new(null, []);

    public static VocabularyForm ForExisting(VocabularyWithArtworkTypes vocabulary) =>
        new(vocabulary.Name.Value, vocabulary.ArtworkTypeIds);

    public EditContext EditContext { get; }

    [Required]
    [StringLength(CatalogLimits.VocabularyNameMaximumLength)]
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
        _savedArtworkTypeIds.Contains(artworkTypeId)
        && !_artworkTypeIds.Contains(artworkTypeId);

    public void AddNameTakenError(string name)
    {
        _serverMessages.Add(() => Name, $"A vocabulary called \"{name}\" already exists."); // Possible null reference return.
        EditContext.NotifyValidationStateChanged();
    }

    public VocabularyName ToVocabularyName() => new(Unwrap.Value(Name).Trim());
}
