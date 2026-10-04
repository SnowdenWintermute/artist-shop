using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace ArtistShop.Web.Components.Forms;

// A number typed into a text box, where InputNumber writes type="number". Firefox lets letters into
// a number box and then sends it as empty, so a typo would vanish without a word. A text box sends
// what was typed, and text that isn't a number comes back as an error
public sealed class TextNumberInput<TValue> : InputBase<TValue>
{
    private static readonly Type NumberType = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);

    // phones show a keypad, with a decimal point only where the number can have one
    private static readonly string InputMode =
        NumberType == typeof(int) || NumberType == typeof(long) || NumberType == typeof(short) ? "numeric" : "decimal";

    [Parameter]
    public string ParsingErrorMessage { get; set; } = "Enter a number.";

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "input");
        builder.AddMultipleAttributes(1, AdditionalAttributes);
        builder.AddAttribute(2, "type", "text");
        builder.AddAttribute(3, "inputmode", InputMode);
        builder.AddAttribute(4, "name", NameAttributeValue);
        builder.AddAttribute(5, "class", CssClass);
        builder.AddAttribute(6, "value", CurrentValueAsString);
        builder.AddAttribute(
            7,
            "onchange",
            EventCallback.Factory.CreateBinder<string?>(this, value => CurrentValueAsString = value, CurrentValueAsString)
        );
        builder.SetUpdatesAttributeName("value");
        builder.CloseElement();
    }

    // invariant, as InputNumber reads it: a decimal point, never a comma
    protected override bool TryParseValueFromString(
        string? value,
        [MaybeNullWhen(false)] out TValue result,
        [NotNullWhen(false)] out string? validationErrorMessage
    )
    {
        if (BindConverter.TryConvertTo(value, CultureInfo.InvariantCulture, out result))
        {
            validationErrorMessage = null;
            return true;
        }

        validationErrorMessage = ParsingErrorMessage;
        return false;
    }

    protected override string? FormatValueAsString(TValue? value) =>
        BindConverter.FormatValue(value, CultureInfo.InvariantCulture)?.ToString();
}
