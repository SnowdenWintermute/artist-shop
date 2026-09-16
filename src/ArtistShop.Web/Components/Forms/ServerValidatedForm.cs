namespace ArtistShop.Web.Components.Forms;

using Microsoft.AspNetCore.Components.Forms;

// a form class that owns its EditContext, plus messages for errors only the database can find,
// like a duplicate name
public abstract class ServerValidatedForm
{
    private readonly ValidationMessageStore _serverMessages;

    protected ServerValidatedForm()
    {
        EditContext = new EditContext(this);
        _serverMessages = new ValidationMessageStore(EditContext);
        EditContext.OnValidationRequested += (_, _) => _serverMessages.Clear();
        EditContext.OnFieldChanged += (_, changed) =>
            _serverMessages.Clear(changed.FieldIdentifier);
    }

    public EditContext EditContext { get; }

    protected void AddServerError(string propertyName, string message)
    {
        _serverMessages.Add(new FieldIdentifier(this, propertyName), message);
        EditContext.NotifyValidationStateChanged();
    }
}
