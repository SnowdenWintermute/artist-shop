using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.CatalogImport;

// the file part of an import's check form, which ImportFileFields renders. Each form declares these
// itself, since the artwork import's check form already inherits its settings
public interface IImportFileForm
{
    // a browser never keeps a chosen file across a page load, so this is null on every post after the
    // first unless the artist picks the file again
    IFormFile? File { get; }

    // the last file this form read, carried in hidden fields so a failed check doesn't lose it
    string? KeptCsvText { get; set; }

    string? KeptFileName { get; set; }

    void AddFileError(string message);
}

public static class ImportFileForm
{
    // the newly chosen file, or else the one kept from an earlier check; null once an error is added
    public static async Task<string?> ReadCheckedFileAsync(this IImportFileForm form)
    {
        // a form posted with no file chosen still sends an empty, unnamed file
        if (form.File is not { FileName.Length: > 0 } file)
        {
            if (form.KeptCsvText is null)
            {
                form.AddFileError("Choose a CSV file.");
            }

            return form.KeptCsvText;
        }

        // a new file replaces the kept one even when it can't be read
        form.KeptFileName = null;
        form.KeptCsvText = null;

        switch (await ImportFileText.ReadAsync(file))
        {
            case ImportFileText.Read read:
                form.KeptFileName = file.FileName;
                form.KeptCsvText = read.Text;
                return read.Text;
            case ImportFileText.Refused refused:
                form.AddFileError(refused.Problem);
                return null;
            default:
                throw new InvalidOperationException("An import file is either read or refused.");
        }
    }
}
