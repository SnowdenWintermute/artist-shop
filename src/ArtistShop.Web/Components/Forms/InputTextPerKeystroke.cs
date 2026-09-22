namespace ArtistShop.Web.Components.Forms;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

// InputText, but bound on input rather than change, which a text box raises only when it loses
// focus. Islands only: a static page's form sends no events
public class InputTextPerKeystroke : InputText
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "name", NameAttributeValue);
        builder.AddAttribute(3, "class", CssClass);
        builder.AddAttribute(4, "value", CurrentValueAsString);
        builder.AddAttribute(
            5,
            "oninput",
            EventCallback.Factory.CreateBinder<string?>(
                this,
                value => CurrentValueAsString = value,
                CurrentValueAsString
            )
        );
        builder.SetUpdatesAttributeName("value");
        builder.AddElementReferenceCapture(6, element => Element = element);
        builder.CloseElement();
    }
}
