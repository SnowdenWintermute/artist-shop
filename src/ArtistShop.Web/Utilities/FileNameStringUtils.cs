namespace ArtistShop.Web.Utilities;

public class FileNameStringUtils
{
    public static string NameFromFileName(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var spaced = withoutExtension.Replace('_', ' ').Replace('-', ' ');

        return string.Join(' ', spaced.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
