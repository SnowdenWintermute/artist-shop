using ArtistShop.Web.Components.Pages.Admin.Catalog.Artworks;

namespace ArtistShop.Web.Tests.Components;

public class ArtworkFormTests
{
    // What the binder does when the form posts no entry for a list: the edit page hits this by
    // removing every image, and the page then renders the empty list back into the images island
    [Fact]
    public void ReadsAListTheFormDidNotPostAsEmpty()
    {
        var form = new ArtworkForm
        {
            Images = null!,
            VocabularyTermIds = null!,
            SeriesIds = null!,
        };

        Assert.Empty(form.Images);
        Assert.Empty(form.ToArtworkImages());
        Assert.Empty(form.VocabularyTermIds);
        Assert.Empty(form.SeriesIds);
    }
}
