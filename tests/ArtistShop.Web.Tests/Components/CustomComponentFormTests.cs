using ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Images;

namespace ArtistShop.Web.Tests.Components;

public sealed class CustomComponentFormTests : IDisposable
{
    private readonly DirectoryInfo _storageRoot;
    private readonly ImageStorage _imageStorage;

    public CustomComponentFormTests()
    {
        _storageRoot = Directory.CreateTempSubdirectory("artist-shop-tests-");
        _imageStorage = new ImageStorage(_storageRoot.FullName);
    }

    public void Dispose() => _storageRoot.Delete(recursive: true);

    [Fact]
    public void RefusesATextComponentPostedWithoutItsText()
    {
        var form = new CustomComponentForm { Name = "Text 1" };

        Assert.False(form.CheckBody(_imageStorage, new TextSettings(RichText.Empty, TextWidth.Narrow)));
        Assert.Single(form.EditContext.GetValidationMessages(form.EditContext.Field(nameof(CustomComponentForm.Body))));
    }

    [Fact]
    public void LeavesATypeWithoutTextAlone()
    {
        var form = new CustomComponentForm { Name = "Post list 1" };

        Assert.True(form.CheckBody(_imageStorage, new PostListSettings()));
    }
}
