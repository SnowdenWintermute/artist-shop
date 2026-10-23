using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Components.Pages.Admin.Website.CustomPages;

// A component's link as its settings form posts it: the type of target, and the field for it. The work and
// the address are posted whatever the type, so switching back finds them still there
public class LinkTargetInput
{
    // null for no link, which only a heading offers
    public LinkTargetType? Type { get; set; }

    public int? PageId { get; set; }

    public int? WorkId { get; set; }

    public int? CollectionId { get; set; }

    public int? PostId { get; set; }

    public string? Address { get; set; }

    public static LinkTargetInput From(LinkTarget? target) =>
        new()
        {
            Type = target?.Type,
            PageId = target is LinkTarget.Page page ? page.Id.Value : null,
            WorkId = target is LinkTarget.Work work ? work.Id.Value : null,
            CollectionId = target is LinkTarget.Collection collection ? collection.Id.Value : null,
            PostId = target is LinkTarget.Post post ? post.Id.Value : null,
            Address = target is LinkTarget.Address address ? address.Value : null,
        };

    // a page, collection or post just chosen as the type, with none picked yet, starts at the home page or the
    // first one listed; null when there's none to link to, which the type isn't offered for
    public void ChooseDefaults(CustomPageId homeId, CollectionId? firstCollectionId, PostId? firstPostId)
    {
        switch (Type)
        {
            case LinkTargetType.Page:
                PageId ??= homeId.Value;
                break;
            case LinkTargetType.Collection:
                CollectionId ??= firstCollectionId?.Value;
                break;
            case LinkTargetType.Post:
                PostId ??= firstPostId?.Value;
                break;
        }
    }

    // why the post can't be saved as it is, or null
    public string? Problem() =>
        Type switch
        {
            null => null,
            LinkTargetType.Page when PageId is null => "Choose what to link to.",
            LinkTargetType.Work when WorkId is null => "Choose what to link to.",
            LinkTargetType.Collection when CollectionId is null => "Choose what to link to.",
            LinkTargetType.Post when PostId is null => "Choose what to link to.",
            LinkTargetType.Address when string.IsNullOrWhiteSpace(Address) => "Write the address.",
            LinkTargetType.Address when !LinkTarget.Address.Allows(Address?.Trim() ?? "") =>
                "Write the whole address, starting with https://, or mailto: for an email.",
            LinkTargetType.Page or LinkTargetType.Work or LinkTargetType.Collection or LinkTargetType.Post or LinkTargetType.Address => null,
            _ => "Choose what to link to.",
        };

    // the target Problem found nothing wrong with
    public LinkTarget? ToTarget() =>
        Type switch
        {
            null => null,
            LinkTargetType.Page => new LinkTarget.Page(new CustomPageId(Chosen(PageId))),
            LinkTargetType.Work => new LinkTarget.Work(new WorkId(Chosen(WorkId))),
            LinkTargetType.Collection => new LinkTarget.Collection(new CollectionId(Chosen(CollectionId))),
            LinkTargetType.Post => new LinkTarget.Post(new PostId(Chosen(PostId))),
            LinkTargetType.Address => new LinkTarget.Address((Address ?? throw new InvalidOperationException("Save checks the address first.")).Trim()),
            _ => throw new InvalidOperationException($"{Type} isn't a link target."),
        };

    private static int Chosen(int? id) => id ?? throw new InvalidOperationException("Save checks the link first.");
}
