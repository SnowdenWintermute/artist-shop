using ArtistShop.Web.Components.Pages.Admin.Catalog.Works;

namespace ArtistShop.Web.Tests.Components;

public class WorkFormTests
{
    // What the binder does when the form posts no entry for a list: the edit page hits this by
    // removing every image, and the page then renders the empty list back into the images island
    [Fact]
    public void ReadsAListTheFormDidNotPostAsEmpty()
    {
        var form = new WorkForm
        {
            Images = null!,
            VocabularyTermIds = null!,
            CollectionIds = null!,
        };

        Assert.Empty(form.Images);
        Assert.Empty(form.ToWorkImages());
        Assert.Empty(form.VocabularyTermIds);
        Assert.Empty(form.CollectionIds);
    }
}
