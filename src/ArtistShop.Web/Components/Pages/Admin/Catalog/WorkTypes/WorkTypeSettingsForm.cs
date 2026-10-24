using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkTypes;

// form posts create this, so it keeps a single public constructor
public class WorkTypeSettingsForm : ServerValidatedForm
{
    public DimensionDisplay DimensionDisplay { get; set; }

    public static WorkTypeSettingsForm From(WorkTypeSettings settings) =>
        new() { DimensionDisplay = settings.DimensionDisplay };

    // what a post left out, as a script might, which would otherwise save 0. False when the form says by the
    // field why
    public bool Check()
    {
        if (Enum.IsDefined(DimensionDisplay))
        {
            return true;
        }

        AddServerError(nameof(DimensionDisplay), "Choose one.");
        return false;
    }

    public WorkTypeSettings ToSettings() => new(DimensionDisplay);
}
