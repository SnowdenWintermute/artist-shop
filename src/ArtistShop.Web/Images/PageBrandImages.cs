using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Images;

// The brand images a page's shell shows (the logo in the nav bar, the favicon in the head), read once
// per request for both. Not for the branding page, which must see each replacement
public class PageBrandImages(BrandImageStore store)
{
    private Task<Dictionary<BrandImageType, BrandImage>>? _images;

    public Task<Dictionary<BrandImageType, BrandImage>> GetAllAsync() => _images ??= store.GetAllAsync();
}
