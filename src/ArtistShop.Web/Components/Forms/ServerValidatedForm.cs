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

    // a post that left a select or radio group out, as a script might, would otherwise save its type's
    // default of 0
    protected bool CheckSelects(params (bool IsDefined, string Field)[] selects)
    {
        var isValid = true;

        foreach (var (isDefined, field) in selects)
        {
            if (!isDefined)
            {
                AddServerError(field, "Choose one.");
                isValid = false;
            }
        }

        return isValid;
    }
}
