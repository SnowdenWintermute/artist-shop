using ArtistShop.Web.Domain.Catalog;

namespace ArtistShop.Web.Components.Forms.FileUpload.Images;

// what ImageListEditor holds after each change. MainImageKey null means the first image
public record ImageListState(IReadOnlyList<ArtworkImage> Images, string? MainImageKey, bool IsUploading);
