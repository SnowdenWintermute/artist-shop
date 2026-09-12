namespace ArtistShop.Web.Components.Forms.FileUpload;

public abstract class SelectedFile(string id, string name, long size)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public long Size { get; } = size;
    public int UploadPercentComplete { get; set; }
    public string? Error { get; set; }
}
