using ArtistShop.Web.Components.Catalog;
using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Tests.Components;

public class WorkListQueryTests
{
    // what the filter form sends back with nothing chosen but images: every field, empty or not
    private const string SubmittedWithImagesOnly =
        "/admin/posts/pick-work?mode=change-image&q=&collection=&images=yes&sale=&sort=added";

    [Theory]
    [InlineData("/admin/catalog/works", "/admin/catalog/works?q=&collection=&images=&sale=&sort=added")]
    [InlineData("/admin/catalog/works", "/admin/catalog/works?sort=title&page=3")]
    [InlineData("/admin/posts/pick-work?images=yes", SubmittedWithImagesOnly)]
    [InlineData("/admin/posts/pick-work?images=yes&mode=change-image", SubmittedWithImagesOnly)]
    [InlineData("/admin/catalog/works?type=1&type=2", "/admin/catalog/works?type=2&type=1")]
    public void AddressesShowingTheSameWorksHaveTheSameFilters(string cleared, string current)
    {
        Assert.True(WorkListQuery.ReadAddress(current).HasSameFiltersAs(WorkListQuery.ReadAddress(cleared)));
    }

    [Theory]
    [InlineData("/admin/catalog/works", "/admin/catalog/works?images=yes")]
    [InlineData("/admin/catalog/works", "/admin/catalog/works?q=rose")]
    [InlineData("/admin/posts/pick-work?images=yes", "/admin/posts/pick-work?q=&images=")]
    [InlineData("/admin/posts/pick-work?images=yes", "/admin/posts/pick-work?images=yes&collection=4")]
    [InlineData("/admin/catalog/works?term=1", "/admin/catalog/works?term=1&term=2")]
    [InlineData("/admin/catalog/works", "/admin/catalog/works?collection=none")]
    public void AddressesShowingOtherWorksHaveDifferentFilters(string cleared, string current)
    {
        Assert.False(WorkListQuery.ReadAddress(current).HasSameFiltersAs(WorkListQuery.ReadAddress(cleared)));
    }

    [Fact]
    public void NoneIsTheNoCollectionFilter()
    {
        Assert.Equal(
            new WorkCollectionFilter.InNoCollection(),
            WorkListQuery.ReadAddress("/admin/catalog/works?collection=none").Collection
        );
    }

    [Fact]
    public void ANumberIsThatCollection()
    {
        Assert.Equal(
            new WorkCollectionFilter.InCollection(new CollectionId(4)),
            WorkListQuery.ReadAddress("/admin/catalog/works?collection=4").Collection
        );
    }

    // the artist's order only sorts one collection's worth of works
    [Theory]
    [InlineData("/admin/catalog/works?collection=4&sort=collection", WorkListSort.CollectionOrder)]
    [InlineData("/admin/catalog/works?collection=none&sort=collection", WorkListSort.RecentlyAdded)]
    [InlineData("/admin/catalog/works?sort=collection", WorkListSort.RecentlyAdded)]
    public void SortsByCollectionOrderOnlyWithACollectionPicked(string address, WorkListSort expected)
    {
        Assert.Equal(expected, WorkListQuery.ReadAddress(address).Sort);
    }
}
