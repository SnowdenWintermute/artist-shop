using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using ArtistShop.Web.Components;
using ArtistShop.Web.Components.Forms.FileUpload;
using ArtistShop.Web.Database;
using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Catalog;
using ArtistShop.Web.Domain.Commerce;
using ArtistShop.Web.Domain.Publishing;
using ArtistShop.Web.Domain.Sites;
using ArtistShop.Web.Exports;
using ArtistShop.Web.Images;
using ArtistShop.Web.Imports;
using ArtistShop.Web.Sites;
using ArtistShop.Web.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArtistShop.Web.Tests.App;

// The Export page's downloads, and what importing the catalog into another website gives back
[Collection(TestAppCollection.Name)]
public sealed class ExportTests(TestApp app)
{
    [Fact]
    public async Task AnAdminDownloadsTheCatalogZip()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        await catalog.Artworks.AddAsync(
            Addition(await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [])
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"{site.Host}-catalog-", response.Content.Headers.ContentDisposition?.FileNameStar);

        var files = await ReadZipAsync(response);
        Assert.Contains(ExportZip.ReadmeFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.ArtworkTypesFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.VocabulariesFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.ProductsFileName, files.Keys);
        // only the types that have artworks
        Assert.Equal(
            [$"{CatalogExportArchive.ArtworksFolder}/Painting.csv"],
            files.Keys.Where(path => path.StartsWith($"{CatalogExportArchive.ArtworksFolder}/"))
        );
    }

    [Fact]
    public async Task AnAdminDownloadsATypesImagesNamedAfterTheirArtworks()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        var gardens = await catalog.Series.AddAsync(new SeriesName("Gardens"), new SeriesSlug("gardens"));
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0, 4, 5, 6];
        await catalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, jpeg), await SaveOriginalAsync(site.Id, tiff)]
                ),
                // in its own download
                Addition(
                    painting, "Rose", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(site.Id, jpeg)]
                ),
            ]
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync($"{ExportEndpoints.ImagesPath}?type={painting.Value}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"{site.Host}-images-Painting-", response.Content.Headers.ContentDisposition?.FileNameStar);
        Assert.Equal(["no"], response.Headers.GetValues("X-Accel-Buffering"));

        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Equal(["Painting/Dawn (2).tiff", "Painting/Dawn.jpg", ImageExportArchive.ImageListFileName], files.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(jpeg, files["Painting/Dawn.jpg"]);
        Assert.Equal(tiff, files["Painting/Dawn (2).tiff"]);
        Assert.Equal(
            [
                "file,artworkType,title,slug,sha256",
                $"Painting/Dawn.jpg,Painting,Dawn,dawn,{Sha256(jpeg)}",
                $"Painting/Dawn (2).tiff,Painting,Dawn,dawn,{Sha256(tiff)}",
            ],
            CsvLines(files[ImageExportArchive.ImageListFileName])
        );
    }

    [Fact]
    public async Task DownloadAllHoldsEveryTypeAndSeriesInOneFolder()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        var sculpture = await TypeIdAsync(catalog, "Sculpture");
        var gardens = await catalog.Series.AddAsync(new SeriesName("Gardens"), new SeriesSlug("gardens"));
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        await catalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
                Addition(
                    painting, "Rose", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
                Addition(
                    sculpture, "Stone", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
            ]
        );
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);

        var response = await client.GetAsync(ExportEndpoints.AllImagesPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Matches($@"^{System.Text.RegularExpressions.Regex.Escape(site.Host)}-images-\d{{4}}-\d{{2}}-\d{{2}}\.zip$", response.Content.Headers.ContentDisposition?.FileNameStar);
        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Equal(
            ["Painting/Dawn.png", "Painting/Gardens/Rose.png", "Sculpture/Stone.png", ImageExportArchive.ImageListFileName],
            files.Keys.Order(StringComparer.Ordinal)
        );
    }

    // each download in its own folder, and a post's artwork picture names the same hash images.csv
    // gives its image, which is how the whole-website import finds the artwork before uploading anything
    [Fact]
    public async Task TheEverythingDownloadHoldsTheCatalogImagesAndPostsInOneFolder()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 7, 8, 9];
        var dawnImage = await SaveOriginalAsync(site.Id, jpeg);
        await SaveWebCopyAsync(site.Id, dawnImage.StorageKey);
        var dawn = await catalog.Artworks.AddAsync(
            Addition(
                await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [dawnImage]
            )
        );
        var body = new PostBody(
            """{"ops":[{"insert":{"artshop-artwork":{"artworkId":ARTWORK_ID,"storageKey":"ARTWORK_KEY"}}},{"insert":"\n"}]}"""
                .Replace("ARTWORK_ID", $"{dawn.Id.Value}")
                .Replace("ARTWORK_KEY", dawnImage.StorageKey)
        );
        await Posts(site.Id).AddAsync(new PostTitle("Morning"), new PostSlug("morning"), body, PostStatus.Published);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.EverythingPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith($"{site.Host}-website-", response.Content.Headers.ContentDisposition?.FileNameStar);
        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Contains(ExportZip.ReadmeFileName, files.Keys);
        Assert.Contains($"catalog/{CatalogExportArchive.ArtworkTypesFileName}", files.Keys);
        Assert.Contains($"catalog/{CatalogExportArchive.ArtworksFolder}/Painting.csv", files.Keys);
        Assert.Equal(jpeg, files["images/Painting/Dawn.jpg"]);
        Assert.Contains("posts/index.html", files.Keys);

        Assert.Equal($"Painting/Dawn.jpg,Painting,Dawn,dawn,{Sha256(jpeg)}", CsvLines(files[$"images/{ImageExportArchive.ImageListFileName}"])[1]);
        Assert.Contains($"\"{PostExportJson.ImageSha256Property}\": \"{Sha256(jpeg)}\"", Encoding.UTF8.GetString(files["posts/morning/post.json"]));
    }

    [Fact]
    public async Task ASecondImageDownloadWaitsForTheFirst()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        await catalog.Artworks.AddAsync(
            Addition(
                painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF])]
            )
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));
        var url = $"{ExportEndpoints.ImagesPath}?type={painting.Value}";

        HttpResponseMessage response;
        using (app.Services.GetRequiredService<ExportLock>().TryAcquire(site.Id))
        {
            response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        }

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ExportEndpoints.ExportRunningMessage, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        // and once it has finished
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url, TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task ThePostDownloadHasAPageForEachPostWithItsImagesBesideIt()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var dawnImage = await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF]);
        await SaveWebCopyAsync(site.Id, dawnImage.StorageKey);
        var dawn = await catalog.Artworks.AddAsync(
            Addition(
                await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [dawnImage]
            )
        );
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0, 4, 5, 6];
        var upload = await SaveOriginalAsync(site.Id, tiff);
        var webCopy = await SaveWebCopyAsync(site.Id, upload.StorageKey);
        var posts = Posts(site.Id);
        var body = new PostBody(
            """
            {"ops":[
              {"insert":"See "},{"insert":"the gardens","attributes":{"bold":true,"link":"/series/gardens"}},{"insert":"\n"},
              {"insert":{"artshop-artwork":{"artworkId":ARTWORK_ID,"storageKey":"ARTWORK_KEY","layout":"floatLeft"}}},
              {"insert":{"artshop-image":{"storageKey":"UPLOAD_KEY","width":10,"height":10,"alt":"A study","caption":"Early"}}},
              {"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ"}}},
              {"insert":"\n"}
            ]}
            """
                .Replace("ARTWORK_ID", $"{dawn.Id.Value}")
                .Replace("ARTWORK_KEY", dawnImage.StorageKey)
                .Replace("UPLOAD_KEY", upload.StorageKey)
        );
        await posts.AddAsync(new PostTitle("Spring notes"), new PostSlug("spring-notes"), body, PostStatus.Published);
        await posts.AddAsync(new PostTitle("Unfinished"), new PostSlug("unfinished"), PostBody.Empty, PostStatus.Draft);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.PostsPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith($"{site.Host}-posts-", response.Content.Headers.ContentDisposition?.FileNameStar);
        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Equal(
            [
                "README.txt",
                "index.html",
                "spring-notes/image-1.webp",
                "spring-notes/image-2-original.tiff",
                "spring-notes/image-2.webp",
                "spring-notes/post.json",
                "spring-notes/spring-notes.html",
                "unfinished/post.json",
                "unfinished/unfinished.html",
            ],
            files.Keys.Order(StringComparer.Ordinal)
        );
        Assert.Equal(tiff, files["spring-notes/image-2-original.tiff"]);
        Assert.Equal(webCopy, files["spring-notes/image-2.webp"]);

        var page = Encoding.UTF8.GetString(files["spring-notes/spring-notes.html"]);
        Assert.Contains($"""<a href="http://{site.Host}/series/gardens"><strong>the gardens</strong></a>""", page);
        Assert.Contains("""<figure class="floatLeft" style="width: 400px"><img src="image-1.webp" alt="Dawn" width="400" height="400"></figure>""", page);
        Assert.Contains("""<a href="image-2-original.tiff"><img src="image-2.webp" alt="A study" """, page);
        Assert.Contains("<figcaption>Early</figcaption>", page);
        Assert.Contains("""<a href="https://www.youtube.com/watch?v=dQw4w9WgXcQ">""", page);

        var index = Encoding.UTF8.GetString(files["index.html"]);
        Assert.Contains("""<a href="spring-notes/spring-notes.html">Spring notes</a>""", index);
        Assert.Contains("""<h2>Drafts</h2>""", index);
        Assert.Contains("""<a href="unfinished/unfinished.html">Unfinished</a>""", index);
    }

    [Fact]
    public async Task AnImageShownTwiceIsOneFileAndOneWithoutAWebCopyIsLeftOut()
    {
        var site = await app.MakeSiteAsync();
        var shown = await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF]);
        await SaveWebCopyAsync(site.Id, shown.StorageKey);
        var withoutWebCopy = await SaveOriginalAsync(site.Id, [0xFF, 0xD8, 0xFF]);
        var body = new PostBody(
            """
            {"ops":[
              {"insert":{"artshop-image":{"storageKey":"SHOWN_KEY","width":10,"height":10,"alt":"First"}}},
              {"insert":{"artshop-image":{"storageKey":"MISSING_KEY","width":10,"height":10,"alt":"Gone"}}},
              {"insert":{"artshop-image":{"storageKey":"SHOWN_KEY","width":10,"height":10,"alt":"Again"}}},
              {"insert":"\n"}
            ]}
            """
                .Replace("SHOWN_KEY", shown.StorageKey)
                .Replace("MISSING_KEY", withoutWebCopy.StorageKey)
        );
        await Posts(site.Id).AddAsync(new PostTitle("Twice"), new PostSlug("twice"), body, PostStatus.Published);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var files = await ReadZipAsync(await client.GetAsync(ExportEndpoints.PostsPath, TestContext.Current.CancellationToken));

        Assert.Equal(
            ["README.txt", "index.html", "twice/image-1-original.jpg", "twice/image-1.webp", "twice/post.json", "twice/twice.html"],
            files.Keys.Order(StringComparer.Ordinal)
        );
        var page = files["twice/twice.html"];
        Assert.Contains("""<img src="image-1.webp" alt="First" """, page);
        Assert.Contains("""<img src="image-1.webp" alt="Again" """, page);
        Assert.DoesNotContain("Gone", page);
    }

    // Downloaded from one website and imported into another that has the same artwork, as the Import
    // page's post import does, with the browser's uploads written straight to the new website's disk
    [Fact]
    public async Task ImportedPostsKeepTheirDateAndLinkToTheArtworkWithTheSameImage()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 1, 2, 3];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var fromImage = await SaveOriginalAsync(from.Id, dawnBytes);
        await SaveWebCopyAsync(from.Id, fromImage.StorageKey);
        var fromDawn = await fromCatalog.Artworks.AddAsync(
            Addition(
                await TypeIdAsync(fromCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [fromImage]
            )
        );
        var upload = await SaveOriginalAsync(from.Id, [(byte)'I', (byte)'I', 0x2A, 0, 9]);
        await SaveWebCopyAsync(from.Id, upload.StorageKey);
        var body = new PostBody(
            """
            {"ops":[
              {"insert":{"artshop-artwork":{"artworkId":ARTWORK_ID,"storageKey":"ARTWORK_KEY","layout":"floatLeft","caption":"Early light"}}},
              {"insert":{"artshop-image":{"storageKey":"UPLOAD_KEY","width":10,"height":10,"alt":"A study"}}},
              {"insert":"More at "},{"insert":"the gardens","attributes":{"link":"/series/gardens"}},{"insert":"\n"}
            ]}
            """
                .Replace("ARTWORK_ID", $"{fromDawn.Id.Value}")
                .Replace("ARTWORK_KEY", fromImage.StorageKey)
                .Replace("UPLOAD_KEY", upload.StorageKey)
        );
        var fromPosts = Posts(from.Id);
        await fromPosts.AddAsync(new PostTitle("Spring notes"), new PostSlug("spring-notes"), body, PostStatus.Published);
        var published = (await fromPosts.GetBySlugAsync(new PostSlug("spring-notes")))?.PublishedAt;
        Assert.NotNull(published);
        var client = await app.SignedInClientAsync(from.Host, await app.MakeAdminAsync(from.Id));
        var files = await ReadZipAsync(await client.GetAsync(ExportEndpoints.PostsPath, TestContext.Current.CancellationToken), ReadBytesAsync);

        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        var toImage = await SaveOriginalAsync(to.Id, dawnBytes);
        var toDawn = await toCatalog.Artworks.AddAsync(
            Addition(
                await TypeIdAsync(toCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [toImage]
            )
        );
        var folder = new PostImportFolder(
            "spring-notes",
            Encoding.UTF8.GetString(files[$"spring-notes/{PostExportJson.FileName}"]),
            files.Keys.Where(path => path.StartsWith("spring-notes/")).ToDictionary(path => path["spring-notes/".Length..], path => path)
        );
        var toPosts = Posts(to.Id);
        var toStorage = ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), to.Id);

        var item = Assert.Single(
            PostImportPlanner.Plan([folder], await PostImportTarget.LoadAsync([folder], toPosts, toCatalog.Series, toCatalog.Artworks, toCatalog.Images, toStorage))
        );

        Assert.Equal(PostImportOutcome.WillAdd, item.Outcome);
        Assert.Equal(1, item.RelinkedArtworkCount);
        Assert.Equal(["/series/gardens"], item.BrokenLinks);
        var fileId = Assert.Single(item.FileIdsByPlaceholder.Values);
        Assert.Equal("spring-notes/image-2-original.tiff", fileId);
        var uploaded = await SaveOriginalAsync(to.Id, files[fileId]);
        var uploads = new Dictionary<string, ImageUploadResult>
        {
            [fileId] = new(uploaded.StorageKey, "image-2-original.tiff", 10, 10, "data:image/webp;base64,AAAA"),
        };

        Assert.Null(await PostImporter.SaveAsync(item, uploads, toPosts, toStorage));

        var imported = await toPosts.GetBySlugAsync(new PostSlug("spring-notes"));
        Assert.NotNull(imported);
        Assert.NotNull(imported.PublishedAt);
        Assert.Equal(DateText.DateTimeAttribute(published.Value), DateText.DateTimeAttribute(imported.PublishedAt.Value));
        var blocks = PostDocumentParser.Parse(imported.Body).Blocks;
        Assert.Equal(
            new ArtworkEmbedBlock(toDawn.Id, toImage.StorageKey, EmbedImageSize.Medium, EmbedLayout.FloatLeft, "Early light"),
            blocks.OfType<ArtworkEmbedBlock>().Single()
        );
        var image = blocks.OfType<PostImageEmbedBlock>().Single();
        Assert.Equal((uploaded.StorageKey, "A study"), (image.StorageKey, image.Alt));
        // the post shows on the artwork's page, as one written here would
        Assert.Single(await toPosts.GetPublishedMentioningArtworkAsync(toDawn.Id));
    }

    // Download everything from one website, reviewed on a new website and on the one it came from:
    // each stage is planned as though the ones before it had run, so a post's picture already links
    // to an artwork the import would add, and the website it came from has everything already
    [Fact]
    public async Task TheWholeWebsiteReviewPlansEachStageAsThoughTheOnesBeforeHadRun()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(new ArtworkTypeName("Installation"), [ArtworkField.DateCreated]);
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var gardens = await fromCatalog.Series.AddAsync(new SeriesName("Gardens"), new SeriesSlug("gardens"));
        var dawnImage = await SaveOriginalAsync(from.Id, [0xFF, 0xD8, 0xFF, 1]);
        await SaveWebCopyAsync(from.Id, dawnImage.StorageKey);
        await fromCatalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [oil], seriesIds: [gardens], products: [],
                    images: [dawnImage, await SaveOriginalAsync(from.Id, [0xFF, 0xD8, 0xFF, 2])]
                ),
                Addition(
                    installation, "Room", description: null, dimensions: null, duration: null, termIds: [oil], seriesIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(from.Id, [0xFF, 0xD8, 0xFF, 3])]
                ),
            ]
        );
        var dawn = (await fromCatalog.Artworks.GetAllAsync()).Single(artwork => artwork.Name.Value == "Dawn");
        var body = new PostBody(
            """
            {"ops":[
              {"insert":{"artshop-artwork":{"artworkId":ARTWORK_ID,"storageKey":"ARTWORK_KEY"}}},
              {"insert":"See "},{"insert":"Dawn","attributes":{"link":"/artworks/dawn"}},{"insert":" in "},
              {"insert":"the gardens","attributes":{"link":"/series/gardens"}},{"insert":"\n"}
            ]}
            """
                .Replace("ARTWORK_ID", $"{dawn.Id.Value}")
                .Replace("ARTWORK_KEY", dawnImage.StorageKey)
        );
        await Posts(from.Id).AddAsync(new PostTitle("Morning"), new PostSlug("morning"), body, PostStatus.Published);
        var folder = await DownloadEverythingAsync(from);

        var onNew = await ReviewAsync(folder, (await app.MakeSiteAsync()).Id);

        Assert.Empty(onNew.Problems);
        Assert.Equal(["Installation"], onNew.ArtworkTypes.Additions.Select(addition => addition.Name.Value));
        Assert.Equal(["Medium"], onNew.Vocabularies.Changes.Select(change => change.Name.Value));
        Assert.Equal(["Installation.csv", "Painting.csv"], onNew.ArtworkFiles.Select(file => file.FileName).Order(StringComparer.Ordinal));
        Assert.All(onNew.ArtworkFiles, file => Assert.Empty(file.Plan.Errors));
        Assert.All(onNew.ArtworkFiles, file => Assert.Single(file.Plan.Additions));
        // the first file creates it, and the second finds it
        Assert.Equal(["Gardens"], onNew.ArtworkFiles.SelectMany(file => file.Plan.NewSeries).Select(series => series.Name));
        Assert.Equal(3, onNew.Images.ToAddCount);
        Assert.Equal(2, onNew.Images.Artworks.Single(artwork => artwork.Title.Value == "Dawn").ToAdd.Count);
        Assert.Empty(onNew.Images.MissingFiles);
        Assert.Empty(onNew.Images.UnlistedFiles);
        var post = Assert.Single(onNew.Posts);
        Assert.Equal(PostImportOutcome.WillAdd, post.Outcome);
        Assert.Equal(1, post.RelinkedArtworkCount);
        Assert.Empty(post.PictureOnlyArtworks);
        Assert.Empty(post.BrokenLinks);

        var onSame = await ReviewAsync(folder, from.Id);

        Assert.Empty(onSame.ArtworkTypes.Additions);
        Assert.Empty(onSame.Vocabularies.Changes);
        Assert.All(onSame.ArtworkFiles, file => Assert.Empty(file.Plan.Additions));
        Assert.Equal(0, onSame.Images.ToAddCount);
        Assert.Equal(3, onSame.Images.Artworks.Sum(artwork => artwork.AlreadyThereCount));
        Assert.Equal(PostImportOutcome.Skipped, Assert.Single(onSame.Posts).Outcome);
    }

    // Each catalog stage runs against the website as the one before left it, and a second run adds nothing
    [Fact]
    public async Task TheWholeWebsiteImportBringsTheCatalogOverOnce()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 4];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(new ArtworkTypeName("Installation"), [ArtworkField.DateCreated]);
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var gardens = await fromCatalog.Series.AddAsync(new SeriesName("Gardens"), new SeriesSlug("gardens"));
        await fromCatalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", "Early light.", dimensions: null, duration: null, termIds: [oil], seriesIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(from.Id, dawnBytes)]
                ),
                Addition(installation, "Room", description: null, dimensions: null, duration: null, termIds: [oil], seriesIds: [gardens], products: []),
            ]
        );
        var folder = await DownloadEverythingAsync(from);
        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        var repositories = Repositories(toCatalog);
        var stages = new List<WebsiteImportStageDone>();

        var problem = await WebsiteCatalogImporter.ImportAsync(folder, repositories, stage => { stages.Add(stage); return Task.CompletedTask; });

        Assert.Null(problem);
        Assert.Equal(
            ["Artwork types: 1", "Vocabularies: 1", "Vocabulary terms: 1", "Artworks: Painting: 1", "Artworks: Installation: 1"],
            stages.Select(stage => $"{stage.Stage}: {stage.AddedCount}")
        );
        Assert.Equal(Describe(await fromCatalog.Artworks.GetAllAsync()), Describe(await toCatalog.Artworks.GetAllAsync()));

        stages.Clear();
        Assert.Null(await WebsiteCatalogImporter.ImportAsync(folder, repositories, stage => { stages.Add(stage); return Task.CompletedTask; }));
        Assert.All(stages, stage => Assert.Equal(0, stage.AddedCount));

        // the images stage then sends Dawn's image to the artwork now here, and a second run finds it there
        var toDawn = (await toCatalog.Artworks.GetAllAsync()).Single(artwork => artwork.Name.Value == "Dawn");
        var images = Assert.Single((await ReviewAsync(folder, to.Id)).Images.Artworks);
        Assert.Equal((toDawn.Id, 1), (images.ArtworkId, images.ToAdd.Count));

        await toCatalog.Images.AppendImageAsync(toDawn.Id, await SaveOriginalAsync(to.Id, dawnBytes), Sha256(dawnBytes));

        var again = Assert.Single((await ReviewAsync(folder, to.Id)).Images.Artworks);
        Assert.Equal((0, 1), (again.ToAdd.Count, again.AlreadyThereCount));
    }

    // The posts stage plans against the website as the images left it: a picture links to its
    // artwork only once the artwork's image is here, never to a placeholder
    [Fact]
    public async Task TheWholeWebsitePostsLinkOnlyToArtworksWhoseImagesAreHere()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 5];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var dawnImage = await SaveOriginalAsync(from.Id, dawnBytes);
        await SaveWebCopyAsync(from.Id, dawnImage.StorageKey);
        var dawn = await fromCatalog.Artworks.AddAsync(
            Addition(
                await TypeIdAsync(fromCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [],
                images: [dawnImage]
            )
        );
        var body = new PostBody(
            """{"ops":[{"insert":{"artshop-artwork":{"artworkId":ARTWORK_ID,"storageKey":"ARTWORK_KEY"}}},{"insert":"\n"}]}"""
                .Replace("ARTWORK_ID", $"{dawn.Id.Value}")
                .Replace("ARTWORK_KEY", dawnImage.StorageKey)
        );
        await Posts(from.Id).AddAsync(new PostTitle("Morning"), new PostSlug("morning"), body, PostStatus.Published);
        var folder = await DownloadEverythingAsync(from);

        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        var toPosts = Posts(to.Id);
        var toStorage = ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), to.Id);
        await WebsiteCatalogImporter.ImportAsync(
            folder,
            Repositories(toCatalog),
            _ => Task.CompletedTask
        );

        async Task<PostImportItem> PlanAsync() =>
            Assert.Single(PostImportPlanner.Plan(folder.Posts, await PostImportTarget.LoadAsync(folder.Posts, toPosts, toCatalog.Series, toCatalog.Artworks, toCatalog.Images, toStorage)));

        // Dawn is here, but not its image: the images stage didn't get it in
        var beforeImages = await PlanAsync();
        Assert.Equal(0, beforeImages.RelinkedArtworkCount);
        Assert.Equal(["Dawn"], beforeImages.PictureOnlyArtworks);

        var toDawn = (await toCatalog.Artworks.GetAllAsync()).Single(artwork => artwork.Name.Value == "Dawn");
        var toImage = await SaveOriginalAsync(to.Id, dawnBytes);
        await toCatalog.Images.AppendImageAsync(toDawn.Id, toImage, Sha256(dawnBytes));

        var run = new PostImportRun([await PlanAsync()]);
        Assert.Equal(1, run.Items[0].RelinkedArtworkCount);
        var batch = Assert.Single(run.Start());
        Assert.Empty(batch.FileIds);

        await run.PostUploadedAsync(batch.Index, toPosts, toStorage, NullLogger.Instance);

        Assert.Null(run.Results[batch.Index]);
        Assert.Empty(run.Start());
        var imported = await toPosts.GetBySlugAsync(new PostSlug("morning"));
        Assert.NotNull(imported);
        Assert.Equal(
            new ArtworkEmbedBlock(toDawn.Id, toImage.StorageKey, EmbedImageSize.Medium, EmbedLayout.Center, Caption: null),
            Assert.Single(PostDocumentParser.Parse(imported.Body).Blocks.OfType<ArtworkEmbedBlock>())
        );
    }

    [Fact]
    public async Task TheImportPageLeadsWithMovingAWholeWebsite()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var importPage = await client.GetStringAsync(PageUrls.Import, TestContext.Current.CancellationToken);
        var websitePage = await client.GetAsync(PageUrls.WebsiteImport, TestContext.Current.CancellationToken);

        Assert.Contains($"href=\"{PageUrls.WebsiteImport}\"", importPage);
        Assert.Equal(HttpStatusCode.OK, websitePage.StatusCode);
    }

    [Fact]
    public async Task SomeoneWhoIsntAnAdminIsDenied()
    {
        var site = await app.MakeSiteAsync();
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAccountAsync());

        var response = await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task SomeoneNotSignedInIsSentToSignIn()
    {
        var site = await app.MakeSiteAsync();

        var response = await app.ClientFor(site.Host).GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    // Exported from one website and imported into a new one in the README's order (types, then
    // vocabularies, then each type's artworks), the catalog comes back the same
    [Fact]
    public async Task ImportingTheExportIntoAnotherWebsiteGivesTheSameCatalog()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(
            new ArtworkTypeName("Installation"),
            [ArtworkField.DateCreated, ArtworkField.HeightAndWidth, ArtworkField.Depth, ArtworkField.Duration]
        );
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var bronze = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Bronze"));
        await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Unused"));
        var gardens = await fromCatalog.Series.AddAsync(new SeriesName("Gardens; Summer"), new SeriesSlug("gardens-summer"));
        var original = (await fromCatalog.ProductTypes.GetAllAsync()).Single(type => type.IsDefault);

        await fromCatalog.Artworks.AddManyAsync(
            [
                Addition(
                    painting,
                    "Roses, at dusk",
                    "Painted \"en plein air\".\nVarnished in 2020.",
                    new DimensionsCentimeters(new Dimensions(60.96m, 45.72m, null)),
                    duration: null,
                    termIds: [oil],
                    seriesIds: [gardens],
                    products: [new ProductAddition(original.Id, Label: null, 950m, EditionSize: 1, Stock: 1)]
                ),
                Addition(painting, "=Untitled", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: []),
                Addition(
                    installation,
                    "Room of echoes",
                    description: null,
                    new DimensionsCentimeters(new Dimensions(300m, 400m, 250.5m)),
                    TimeSpan.FromMinutes(12),
                    termIds: [bronze],
                    seriesIds: [gardens],
                    products: []
                ),
            ]
        );

        var client = await app.SignedInClientAsync(from.Host, from.OwnerEmail);
        var files = await ReadZipAsync(await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken));
        Assert.Contains("Painting,\"Roses, at dusk\",roses-at-dusk,Original,,950.00,1,1", files[CatalogExportArchive.ProductsFileName]);
        // a spreadsheet would run it as a formula without the '
        Assert.Contains("\"'=Untitled\"", files[$"{CatalogExportArchive.ArtworksFolder}/Painting.csv"]);

        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        // the series name has a semicolon, so the export chose the next separator
        const char listSeparator = '|';

        var typePlan = ArtworkTypeImportPlanner.Plan(files[CatalogExportArchive.ArtworkTypesFileName], listSeparator, await SetupAsync(toCatalog));
        Assert.Empty(typePlan.Errors);
        await ArtworkTypeImportPlanner.ApplyAsync(typePlan, toCatalog.Types);

        var vocabularyPlan = VocabularyImportPlanner.Plan(files[CatalogExportArchive.VocabulariesFileName], listSeparator, await SetupAsync(toCatalog));
        Assert.Empty(vocabularyPlan.Errors);
        await VocabularyImportPlanner.ApplyAsync(vocabularyPlan, toCatalog.Vocabularies, toCatalog.Terms);

        foreach (var typeName in (string[])["Painting", "Installation"])
        {
            var snapshot = await ArtworkImportCatalogSnapshot.LoadAsync(
                await TypeIdAsync(toCatalog, typeName),
                toCatalog.Types,
                toCatalog.Vocabularies,
                toCatalog.Series,
                toCatalog.ProductTypes,
                toCatalog.Artworks
            );
            Assert.NotNull(snapshot);
            var plan = ArtworkImportPlanner.Plan(
                files[$"{CatalogExportArchive.ArtworksFolder}/{typeName}.csv"],
                new ArtworkImportSettings(LengthUnit.Centimeters, original.Id, IsOneOfAKind: true, listSeparator),
                snapshot
            );
            Assert.Empty(plan.Errors);
            await toCatalog.Artworks.AddManyAsync([.. plan.Additions.Select(addition => addition.Addition)]);
        }

        var fromSetup = await SetupAsync(fromCatalog);
        var toSetup = await SetupAsync(toCatalog);
        Assert.Equal(CatalogSetupCsvExport.ArtworkTypes(fromSetup, listSeparator), CatalogSetupCsvExport.ArtworkTypes(toSetup, listSeparator));
        Assert.Equal(CatalogSetupCsvExport.Vocabularies(fromSetup, listSeparator), CatalogSetupCsvExport.Vocabularies(toSetup, listSeparator));
        Assert.Equal(Describe(await fromCatalog.Artworks.GetAllAsync()), Describe(await toCatalog.Artworks.GetAllAsync()));
    }

    // Two artworks of a type titled "Dawn", told apart by their slugs: both come over, and each
    // image goes only to the artwork with its images.csv slug, never to whichever Dawn comes first.
    // Back on the website it came from, each image is found on its own artwork
    [Fact]
    public async Task TheWholeWebsiteMoveKeepsTwoSameTitledArtworksApart()
    {
        byte[] firstBytes = [0xFF, 0xD8, 0xFF, 6];
        byte[] secondBytes = [0xFF, 0xD8, 0xFF, 7];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        await fromCatalog.Artworks.AddAsync(
            Addition(painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [], images: [await SaveOriginalAsync(from.Id, firstBytes)])
        );
        await fromCatalog.Artworks.AddAsync(
            Addition(painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [], images: [await SaveOriginalAsync(from.Id, secondBytes)])
        );
        var folder = await DownloadEverythingAsync(from);

        // the artwork file's slugs tell the two apart, so both come over, each with its own image
        var onNew = await ReviewAsync(folder, (await app.MakeSiteAsync()).Id);

        Assert.Equal(["dawn", "dawn-2"], Assert.Single(onNew.ArtworkFiles).Plan.Additions.Select(addition => addition.Addition.CandidateSlug.Value));
        Assert.Equal(
            [[Sha256(firstBytes)], [Sha256(secondBytes)]],
            onNew.Images.Artworks.Select(artwork => artwork.ToAdd.Select(image => image.Sha256).ToList()).ToList()
        );
        Assert.Empty(onNew.Images.WithoutArtwork);

        var to = await app.MakeSiteAsync();
        Assert.Null(await WebsiteCatalogImporter.ImportAsync(folder, Repositories(Catalog(to.Id)), _ => Task.CompletedTask));
        Assert.Equal(["dawn", "dawn-2"], (await Catalog(to.Id).Artworks.GetAllAsync()).Select(artwork => artwork.Slug.Value).Order(StringComparer.Ordinal));

        // a website with one Dawn of its own, under the first one's slug, takes only its image and
        // adds the second Dawn
        var withDawn = await app.MakeSiteAsync();
        var withDawnCatalog = Catalog(withDawn.Id);
        await withDawnCatalog.Artworks.AddAsync(
            Addition(await TypeIdAsync(withDawnCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [])
        );

        var onOneDawn = await ReviewAsync(folder, withDawn.Id);

        Assert.Equal(["dawn-2"], Assert.Single(onOneDawn.ArtworkFiles).Plan.Additions.Select(addition => addition.Addition.CandidateSlug.Value));
        Assert.Equal(2, onOneDawn.Images.ToAddCount);
        Assert.Empty(onOneDawn.Images.WithoutArtwork);

        var onSame = await ReviewAsync(folder, from.Id);

        Assert.Equal(0, onSame.Images.ToAddCount);
        Assert.All(onSame.Images.Artworks, artwork => Assert.Equal(1, artwork.AlreadyThereCount));
        Assert.Equal(2, onSame.Images.Artworks.Count);
    }

    // images.csv against the folder: a listed file that isn't there, a file that isn't listed, an
    // image for an artwork that won't be here, and rows it can't read. None stops the import
    [Fact]
    public async Task TheWholeWebsiteReviewListsTheImagesItCantImport()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 8];
        byte[] duskBytes = [0xFF, 0xD8, 0xFF, 9];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        await fromCatalog.Artworks.AddManyAsync(
            [
                Addition(painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [], images: [await SaveOriginalAsync(from.Id, dawnBytes)]),
                Addition(painting, "Dusk", description: null, dimensions: null, duration: null, termIds: [], seriesIds: [], products: [], images: [await SaveOriginalAsync(from.Id, duskBytes)]),
            ]
        );
        var downloaded = await DownloadEverythingAsync(from);
        var imageList = Unwrap.Value(downloaded.ImageList);
        var folder = downloaded with
        {
            ImageList =
                imageList
                + $"Painting/Ghost.jpg,Painting,Ghost,ghost,{Sha256([1])}\r\n"
                + "Painting/Blank.jpg,Painting,Dawn,dawn,\r\n"
                + $"Painting/Dusk.jpg,Painting,Dusk,dusk,{Sha256(duskBytes)}\r\n",
            ImageFileIdsByPath = new Dictionary<string, string>(downloaded.ImageFileIdsByPath)
            {
                ["Painting/Ghost.jpg"] = "ghost",
                ["Painting/Stray.jpg"] = "stray",
            }.Where(file => file.Key != "Painting/Dawn.jpg").ToDictionary(),
        };

        var review = await ReviewAsync(folder, (await app.MakeSiteAsync()).Id);

        Assert.True(review.CanImport);
        Assert.Equal(["Painting/Dawn.jpg"], review.Images.MissingFiles);
        Assert.Equal(["Painting/Stray.jpg"], review.Images.UnlistedFiles);
        Assert.Equal(["Painting/Ghost.jpg"], review.Images.WithoutArtwork);
        Assert.Equal(2, review.Images.Errors.Count);
        Assert.Contains(review.Images.Errors, error => error.Column == ImageExportArchive.ImageListHeaders.File);
        // Dusk's own row still counts
        Assert.Equal([Sha256(duskBytes)], Assert.Single(review.Images.Artworks).ToAdd.Select(image => image.Sha256));
    }

    [Fact]
    public async Task AFolderWithAFileMissingIsRefusedByTheReviewAndTheImport()
    {
        var from = await app.MakeSiteAsync();
        var folder = (await DownloadEverythingAsync(from)) with { VocabulariesCsv = null };
        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        var stages = new List<WebsiteImportStageDone>();

        var review = await ReviewAsync(folder, to.Id);
        var problem = await WebsiteCatalogImporter.ImportAsync(folder, Repositories(toCatalog), stage => { stages.Add(stage); return Task.CompletedTask; });

        const string missing = "catalog/vocabularies.csv is missing.";
        Assert.False(review.CanImport);
        Assert.Equal([missing], review.Problems);
        Assert.Equal(missing, problem);
        Assert.Empty(stages);
    }

    // re-planned against the website at each stage, a plan with errors stops the import there
    [Fact]
    public async Task TheWholeWebsiteImportStopsAtAStageThatNoLongerPlansCleanly()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var installation = await fromCatalog.Types.AddAsync(new ArtworkTypeName("Installation"), [ArtworkField.DateCreated]);
        await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [installation]);
        var downloaded = await DownloadEverythingAsync(from);
        // as though Installation were gone by the time the import ran
        var folder = downloaded with
        {
            ArtworkTypesCsv = string.Join("\r\n", Unwrap.Value(downloaded.ArtworkTypesCsv).Split("\r\n").Where(line => !line.StartsWith("Installation"))),
        };
        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);

        var problem = await WebsiteCatalogImporter.ImportAsync(folder, Repositories(toCatalog), _ => Task.CompletedTask);

        Assert.Equal(WebsiteCatalogImporter.ChangedSinceReviewProblem, problem);
        Assert.DoesNotContain((await toCatalog.Vocabularies.GetAllAsync()), vocabulary => vocabulary.Name.Value == "Medium");
    }

    // a vocabulary here already isn't added again, but its new terms are
    [Fact]
    public async Task TheVocabularyStageCountsNewVocabulariesAndTermsApart()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var fromMedium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [await TypeIdAsync(fromCatalog, "Painting")]);
        await fromCatalog.Terms.AddAsync(fromMedium, new VocabularyTermName("Oil"));
        await fromCatalog.Terms.AddAsync(fromMedium, new VocabularyTermName("Acrylic"));
        var folder = await DownloadEverythingAsync(from);
        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        var toMedium = await toCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [await TypeIdAsync(toCatalog, "Painting")]);
        await toCatalog.Terms.AddAsync(toMedium, new VocabularyTermName("Oil"));
        var stages = new List<WebsiteImportStageDone>();

        Assert.Null(await WebsiteCatalogImporter.ImportAsync(folder, Repositories(toCatalog), stage => { stages.Add(stage); return Task.CompletedTask; }));

        Assert.Equal(
            ["Artwork types: 0", "Vocabularies: 0", "Vocabulary terms: 1"],
            stages.Select(stage => $"{stage.Stage}: {stage.AddedCount}")
        );
    }

    private async Task<WebsiteImportFolder> DownloadEverythingAsync(TestSite site)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        return await WebsiteFolderAsync(await ReadZipAsync(await client.GetAsync(ExportEndpoints.EverythingPath, TestContext.Current.CancellationToken), ReadBytesAsync));
    }

    private static CatalogRepositories Repositories(SiteCatalog catalog) =>
        new(catalog.Fields, catalog.Types, catalog.Vocabularies, catalog.Terms, catalog.Series, catalog.ProductTypes, catalog.Artworks);

    private async Task<WebsiteImportReview> ReviewAsync(WebsiteImportFolder folder, SiteId siteId)
    {
        var catalog = Catalog(siteId);
        var target = await WebsiteImportTarget.LoadAsync(
            folder,
            catalog.Fields,
            catalog.Types,
            catalog.Vocabularies,
            catalog.Series,
            catalog.ProductTypes,
            catalog.Artworks,
            catalog.Images,
            Posts(siteId),
            ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), siteId)
        );

        return WebsiteImportPlanner.Plan(folder, target);
    }

    // the unzipped download as the Import page reads it, inside a folder of its own, with each
    // file's path in the download as its id
    private static Task<WebsiteImportFolder> WebsiteFolderAsync(Dictionary<string, byte[]> files) =>
        CollectedImportFolders.WebsiteAsync(
            [.. files.Select(file => new CollectedFile(file.Key, $"/download/{file.Key}", file.Value.Length, Type: ""))],
            (file, _) => Task.FromResult(Decode(files[file.Id]))
        );

    // as a StreamReader reads it, taking off a byte order mark
    private static string Decode(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes));
        return reader.ReadToEnd();
    }

    private static ArtworkCatalogAddition Addition(
        ArtworkTypeId typeId,
        string name,
        string? description,
        DimensionsCentimeters? dimensions,
        TimeSpan? duration,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<SeriesId> seriesIds,
        IReadOnlyList<ProductAddition> products,
        IReadOnlyList<ArtworkImage>? images = null
    ) =>
        new(
            typeId,
            new ArtworkName(name),
            ArtworkSlug.FromName(name),
            description,
            DateCreated: null,
            dimensions,
            duration,
            Images: images ?? [],
            MainImageIndex: 0,
            termIds,
            seriesIds,
            NewSeriesNames: [],
            products
        );

    private sealed record SiteCatalog(
        ArtworkRepository Artworks,
        ArtworkFieldRepository Fields,
        ArtworkTypeRepository Types,
        VocabularyRepository Vocabularies,
        VocabularyTermRepository Terms,
        SeriesRepository Series,
        ProductTypeRepository ProductTypes,
        ArtworkImageRepository Images
    );

    private SiteCatalog Catalog(SiteId siteId)
    {
        SiteDatabase database = app.Services.GetRequiredService<SiteDatabases>().For(siteId);
        return new SiteCatalog(
            new ArtworkRepository(database),
            new ArtworkFieldRepository(database),
            new ArtworkTypeRepository(database),
            new VocabularyRepository(database),
            new VocabularyTermRepository(database),
            new SeriesRepository(database),
            new ProductTypeRepository(database),
            new ArtworkImageRepository(database)
        );
    }

    // an original on the site's disk, as an upload leaves it
    private async Task<ArtworkImage> SaveOriginalAsync(SiteId siteId, byte[] bytes)
    {
        var storageKey = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(
            ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), siteId).OriginalPath(storageKey),
            bytes,
            TestContext.Current.CancellationToken
        );

        return new ArtworkImage(storageKey, OriginalFileName: null, Width: 10, Height: 10, BlurDataUri: null);
    }

    private PostRepository Posts(SiteId siteId) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(siteId));

    // the web copy an upload of an image 10 pixels wide gets, at its own width
    private async Task<byte[]> SaveWebCopyAsync(SiteId siteId, string storageKey)
    {
        var directory = ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), siteId).VariantDirectory(storageKey);
        Directory.CreateDirectory(directory);
        byte[] bytes = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 1];
        await File.WriteAllBytesAsync(Path.Combine(directory, ImageVariants.FileName(10, ImageVariantFormat.Webp)), bytes, TestContext.Current.CancellationToken);

        return bytes;
    }

    private static Task<CatalogSetupSnapshot> SetupAsync(SiteCatalog catalog) =>
        CatalogSetupSnapshot.LoadAsync(catalog.Fields, catalog.Types, catalog.Vocabularies);

    private static async Task<ArtworkTypeId> TypeIdAsync(SiteCatalog catalog, string name) =>
        (await catalog.Types.GetAllAsync()).Single(type => type.Name.Value == name).Id;

    // what an artwork holds, less its ids, which differ between websites, and its products, which the
    // import can't bring back
    private static List<string> Describe(IEnumerable<Artwork> artworks) =>
        [
            .. artworks
                .Select(artwork => string.Join(
                    " | ",
                    artwork.Type.Name.Value,
                    artwork.Name.Value,
                    artwork.Slug.Value,
                    artwork.Description,
                    artwork.DateCreated?.Text,
                    artwork.Dimensions?.Text,
                    artwork.Duration,
                    string.Join(", ", artwork.Series.Select(series => series.Name.Value)),
                    string.Join(", ", artwork.VocabularyTerms.Select(term => $"{term.VocabularyName.Value}: {term.Name.Value}"))
                ))
                .Order(),
        ];

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // a CSV file's lines, after the byte order mark that tells Excel it's UTF-8
    private static List<string> CsvLines(byte[] file)
    {
        Assert.Equal(Encoding.UTF8.GetPreamble(), file[..3]);
        return [.. Encoding.UTF8.GetString(file[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)];
    }

    // each file by its path inside the zip's one folder, which is named after the download, so
    // unzipping it doesn't scatter files wherever it's unzipped
    private static Task<Dictionary<string, string>> ReadZipAsync(HttpResponseMessage response) =>
        ReadZipAsync(response, ReadTextAsync);

    private static async Task<string> ReadTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<byte[]> ReadBytesAsync(Stream stream)
    {
        using var bytes = new MemoryStream();
        await stream.CopyToAsync(bytes, TestContext.Current.CancellationToken);
        return bytes.ToArray();
    }

    private static async Task<Dictionary<string, T>> ReadZipAsync<T>(HttpResponseMessage response, Func<Stream, Task<T>> read)
    {
        var folder = $"{Path.GetFileNameWithoutExtension(response.Content.Headers.ContentDisposition?.FileNameStar)}/";
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var files = new Dictionary<string, T>();

        foreach (var entry in zip.Entries)
        {
            Assert.StartsWith(folder, entry.FullName);
            await using var entryStream = entry.Open();
            files[entry.FullName[folder.Length..]] = await read(entryStream);
        }

        return files;
    }
}
