using ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;
using ArtistShop.Web.Tests.Database;

namespace ArtistShop.Web.Tests.Components;

[Collection(DatabaseCollection.Name)]
public sealed class CustomComponentFormTests(TestDatabaseFixture database)
{
    private readonly UploadedImageRepository _library = new(database.Site);

    [Fact]
    public async Task RefusesATextComponentPostedWithoutItsText()
    {
        var form = new CustomComponentForm { Name = "Text 1" };

        Assert.False(await form.CheckBodyAsync(_library, new TextSettings(RichText.Empty, TextWidth.Narrow)));
        Assert.Single(form.EditContext.GetValidationMessages(form.EditContext.Field(nameof(CustomComponentForm.Body))));
    }

    [Fact]
    public async Task LeavesATypeWithoutTextAlone()
    {
        var form = new CustomComponentForm { Name = "Post list 1" };

        Assert.True(await form.CheckBodyAsync(_library, new PostListSettings()));
    }
}
