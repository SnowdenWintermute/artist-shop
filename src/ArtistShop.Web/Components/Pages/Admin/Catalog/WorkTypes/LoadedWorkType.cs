using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Pages.Admin.Catalog.WorkTypes;

public record LoadedWorkType(WorkTypeWithFields WorkType, WorkTypeWorkCounts WorkCounts);
