namespace ArtistShop.Web.Components.Tables;

// A row of a table whose fields save as they're left, such as the work table's. Saved through
// EditableTableRow.SaveAsync, so every such table saves and shows a save the same way
public abstract class EditableTableRow
{
    public bool IsSaving { get; set; }

    // a field changed while a save was running, so another save follows it
    public bool SaveAgain { get; set; }

    // what the database said on the last save, such as the row having been deleted
    public string? SaveError { get; set; }

    // counts up with each save, so SavedFlash plays again
    public int SaveCount { get; set; }

    // checked for EditableTable's bulk bar
    public bool IsSelected { get; set; }

    // false when a field holds something that can't be saved, which the row says by that field
    public virtual bool Validate() => true;

    // A change made while a save is running waits for it, then saves again, so saves of one row never
    // overlap and the last one sent holds the latest values. save returns the error to show, or null
    // once it saved. A row that doesn't validate isn't sent, and keeps its last save's error
    public static async Task SaveAsync(EditableTableRow row, Func<Task<string?>> save)
    {
        if (row.IsSaving)
        {
            row.SaveAgain = true;
            return;
        }

        row.IsSaving = true;

        try
        {
            do
            {
                row.SaveAgain = false;

                if (!row.Validate())
                {
                    continue;
                }

                row.SaveError = await save();

                if (row.SaveError is null)
                {
                    row.SaveCount++;
                }
            } while (row.SaveAgain);
        }
        finally
        {
            row.IsSaving = false;
        }
    }
}
