using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Utilities;
using Microsoft.AspNetCore.Components.Forms;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.VocabularyTerms;

public class VocabularyTermNameForm
{
    private readonly ValidationMessageStore _serverMessages;

    // form posts create this through a parameterless constructor; ": this(null)" runs the other one
    public VocabularyTermNameForm()
        : this(null) { }

    public VocabularyTermNameForm(string? name)
    {
        Name = name;
        EditContext = new EditContext(this);
        _serverMessages = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += (_, _) => _serverMessages.Clear();
        EditContext.OnFieldChanged += (_, changed) =>
            _serverMessages.Clear(changed.FieldIdentifier);
    }

    public EditContext EditContext { get; }

    [Required]
    [StringLength(CatalogLimits.VocabularyTermNameMaximumLength)]
    public string? Name { get; set; }

    public void AddNameTakenError(string name)
    {
        _serverMessages.Add(
            new FieldIdentifier(this, nameof(Name)),
            $"This vocabulary already has a term called \"{name}\"."
        );
        EditContext.NotifyValidationStateChanged();
    }

    public VocabularyTermName ToVocabularyTermName() => new(Unwrap.Value(Name).Trim());
}
