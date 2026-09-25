using System.ComponentModel.DataAnnotations;
using ArtistShop.Web.Components.Forms;
using ArtistShop.Web.Domain;
using ArtistShop.Web.Utilities;

namespace ArtistShop.Web.Components.Pages.Platform.Operator;

// form posts create this, and they need exactly one public constructor
public class SignUpCodeForm : ServerValidatedForm
{
    public const int MaximumDaysValid = 90;

    // who the code is for
    [Required]
    [StringLength(ArtistShopLimits.SignUpCodeNoteMaximumLength)]
    public string? Note { get; set; }

    [Range(1, MaximumDaysValid)]
    public int DaysValid { get; set; } = 14;

    public string TrimmedNote => Unwrap.Value(Note).Trim();
}
