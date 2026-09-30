using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;

namespace ArtistShop.Web.Components.Pages.Platform.Operator;

// form posts create this, and they need exactly one public constructor
public class SignUpCodeForm : ServerValidatedForm
{
    public const int MaximumDaysValid = 90;

    // who the code is for, if anyone needs reminding
    [StringLength(ArtistShopLimits.SignUpCodeNoteMaximumLength)]
    public string? Note { get; set; }

    [Range(1, MaximumDaysValid)]
    public int DaysValid { get; set; } = 14;

    public string TrimmedNote => (Note ?? "").Trim();
}
