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
using ArtistShop.Web.Domain.Website;
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
        await catalog.Works.AddAsync(
            Addition(await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [])
        );
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith($"{site.Host}-catalog-", response.Content.Headers.ContentDisposition?.FileNameStar);

        var files = await ReadZipAsync(response);
        Assert.Contains(ExportZip.ReadmeFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.WorkTypesFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.VocabulariesFileName, files.Keys);
        Assert.Contains(CatalogExportArchive.ProductsFileName, files.Keys);
        // only the types that have works
        Assert.Equal(
            [$"{CatalogExportArchive.WorksFolder}/Painting.csv"],
            files.Keys.Where(path => path.StartsWith($"{CatalogExportArchive.WorksFolder}/"))
        );
    }

    [Fact]
    public async Task AnAdminDownloadsATypesImagesNamedAfterTheirWorks()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        var gardens = await catalog.Collections.AddAsync(new CollectionName("Gardens"), new CollectionSlug("gardens"));
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        byte[] tiff = [(byte)'I', (byte)'I', 0x2A, 0, 4, 5, 6];
        await catalog.Works.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, jpeg), await SaveOriginalAsync(site.Id, tiff)]
                ),
                // in its own download
                Addition(
                    painting, "Rose", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [gardens], products: [],
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
                "file,workType,title,slug,sha256",
                $"Painting/Dawn.jpg,Painting,Dawn,dawn,{Sha256(jpeg)}",
                $"Painting/Dawn (2).tiff,Painting,Dawn,dawn,{Sha256(tiff)}",
            ],
            CsvLines(files[ImageExportArchive.ImageListFileName])
        );
    }

    [Fact]
    public async Task DownloadAllHoldsEveryTypeAndCollectionInOneFolder()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        var painting = await TypeIdAsync(catalog, "Painting");
        var sculpture = await TypeIdAsync(catalog, "Sculpture");
        var gardens = await catalog.Collections.AddAsync(new CollectionName("Gardens"), new CollectionSlug("gardens"));
        byte[] png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        await catalog.Works.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
                Addition(
                    painting, "Rose", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(site.Id, png)]
                ),
                Addition(
                    sculpture, "Stone", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
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

    // each download in its own folder, and a post's work picture names the same hash images.csv
    // gives its image, which is how the whole-website import finds the work before uploading anything
    [Fact]
    public async Task TheEverythingDownloadHoldsTheCatalogImagesAndPostsInOneFolder()
    {
        var site = await app.MakeSiteAsync();
        var catalog = Catalog(site.Id);
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 7, 8, 9];
        var dawnImage = await SaveOriginalAsync(site.Id, jpeg);
        await SaveWebCopyAsync(site.Id, dawnImage.StorageKey);
        var dawn = await catalog.Works.AddAsync(
            Addition(
                await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
                images: [dawnImage]
            )
        );
        var body = new PostBody(
            """{"ops":[{"insert":{"artshop-work":{"workId":WORK_ID,"storageKey":"WORK_KEY"}}},{"insert":"\n"}]}"""
                .Replace("WORK_ID", $"{dawn.Id.Value}")
                .Replace("WORK_KEY", dawnImage.StorageKey)
        );
        await Posts(site.Id).AddAsync(new PostTitle("Morning"), new PostSlug("morning"), body, PostStatus.Published);
        var client = await app.SignedInClientAsync(site.Host, await app.MakeAdminAsync(site.Id));

        var response = await client.GetAsync(ExportEndpoints.EverythingPath, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith($"{site.Host}-website-", response.Content.Headers.ContentDisposition?.FileNameStar);
        var files = await ReadZipAsync(response, ReadBytesAsync);
        Assert.Contains(ExportZip.ReadmeFileName, files.Keys);
        Assert.Contains($"catalog/{CatalogExportArchive.WorkTypesFileName}", files.Keys);
        Assert.Contains($"catalog/{CatalogExportArchive.WorksFolder}/Painting.csv", files.Keys);
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
        await catalog.Works.AddAsync(
            Addition(
                painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
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
        var dawn = await catalog.Works.AddAsync(
            Addition(
                await TypeIdAsync(catalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
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
              {"insert":"See "},{"insert":"the gardens","attributes":{"bold":true,"link":"/collections/gardens"}},{"insert":"\n"},
              {"insert":{"artshop-work":{"workId":WORK_ID,"storageKey":"WORK_KEY","layout":"floatLeft"}}},
              {"insert":{"artshop-image":{"storageKey":"UPLOAD_KEY","width":10,"height":10,"alt":"A study","caption":"Early"}}},
              {"insert":{"artshop-video":{"provider":"youtube","videoId":"dQw4w9WgXcQ"}}},
              {"insert":"\n"}
            ]}
            """
                .Replace("WORK_ID", $"{dawn.Id.Value}")
                .Replace("WORK_KEY", dawnImage.StorageKey)
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
        Assert.Contains($"""<a href="http://{site.Host}/collections/gardens"><strong>the gardens</strong></a>""", page);
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

    // Downloaded from one website and imported into another that has the same work, as the Import
    // page's post import does, with the browser's uploads written straight to the new website's disk
    [Fact]
    public async Task ImportedPostsKeepTheirDateAndLinkToTheWorkWithTheSameImage()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 1, 2, 3];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var fromImage = await SaveOriginalAsync(from.Id, dawnBytes);
        await SaveWebCopyAsync(from.Id, fromImage.StorageKey);
        var fromDawn = await fromCatalog.Works.AddAsync(
            Addition(
                await TypeIdAsync(fromCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
                images: [fromImage]
            )
        );
        var upload = await SaveOriginalAsync(from.Id, [(byte)'I', (byte)'I', 0x2A, 0, 9]);
        await SaveWebCopyAsync(from.Id, upload.StorageKey);
        var body = new PostBody(
            """
            {"ops":[
              {"insert":{"artshop-work":{"workId":WORK_ID,"storageKey":"WORK_KEY","layout":"floatLeft","caption":"Early light"}}},
              {"insert":{"artshop-image":{"storageKey":"UPLOAD_KEY","width":10,"height":10,"alt":"A study"}}},
              {"insert":"More at "},{"insert":"the gardens","attributes":{"link":"/collections/gardens"}},{"insert":"\n"}
            ]}
            """
                .Replace("WORK_ID", $"{fromDawn.Id.Value}")
                .Replace("WORK_KEY", fromImage.StorageKey)
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
        var toDawn = await toCatalog.Works.AddAsync(
            Addition(
                await TypeIdAsync(toCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
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
            PostImportPlanner.Plan([folder], await PostImportTarget.LoadAsync([folder], toPosts, toCatalog.Collections, toCatalog.Works, toCatalog.Images, toStorage))
        );

        Assert.Equal(PostImportOutcome.WillAdd, item.Outcome);
        Assert.Equal(1, item.RelinkedWorkCount);
        Assert.Equal(["/collections/gardens"], item.BrokenLinks);
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
            new WorkEmbedBlock(toDawn.Id, toImage.StorageKey, EmbedImageSize.Medium, EmbedLayout.FloatLeft, "Early light"),
            blocks.OfType<WorkEmbedBlock>().Single()
        );
        var image = blocks.OfType<PostImageEmbedBlock>().Single();
        Assert.Equal((uploaded.StorageKey, "A study"), (image.StorageKey, image.Alt));
        // the post shows on the work's page, as one written here would
        Assert.Single(await toPosts.GetPublishedMentioningWorkAsync(toDawn.Id));
    }

    // Download everything from one website, reviewed on a new website and on the one it came from:
    // each stage is planned as though the ones before it had run, so a post's picture already links
    // to a work the import would add, and the website it came from has everything already
    [Fact]
    public async Task TheWholeWebsiteReviewPlansEachStageAsThoughTheOnesBeforeHadRun()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(new WorkTypeName("Installation"), [WorkField.DateCreated]);
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var gardens = await fromCatalog.Collections.AddAsync(new CollectionName("Gardens"), new CollectionSlug("gardens"));
        var dawnImage = await SaveOriginalAsync(from.Id, [0xFF, 0xD8, 0xFF, 1]);
        await SaveWebCopyAsync(from.Id, dawnImage.StorageKey);
        await fromCatalog.Works.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [oil], collectionIds: [gardens], products: [],
                    images: [dawnImage, await SaveOriginalAsync(from.Id, [0xFF, 0xD8, 0xFF, 2])]
                ),
                Addition(
                    installation, "Room", description: null, dimensions: null, duration: null, termIds: [oil], collectionIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(from.Id, [0xFF, 0xD8, 0xFF, 3])]
                ),
            ]
        );
        var dawn = (await fromCatalog.Works.GetAllAsync()).Single(work => work.Name.Value == "Dawn");
        var body = new PostBody(
            """
            {"ops":[
              {"insert":{"artshop-work":{"workId":WORK_ID,"storageKey":"WORK_KEY"}}},
              {"insert":"See "},{"insert":"Dawn","attributes":{"link":"/works/dawn"}},{"insert":" in "},
              {"insert":"the gardens","attributes":{"link":"/collections/gardens"}},{"insert":"\n"}
            ]}
            """
                .Replace("WORK_ID", $"{dawn.Id.Value}")
                .Replace("WORK_KEY", dawnImage.StorageKey)
        );
        await Posts(from.Id).AddAsync(new PostTitle("Morning"), new PostSlug("morning"), body, PostStatus.Published);
        var folder = await DownloadEverythingAsync(from);

        var onNew = await ReviewAsync(folder, (await app.MakeSiteAsync()).Id);

        Assert.Empty(onNew.Problems);
        Assert.Equal(["Installation"], onNew.WorkTypes.Additions.Select(addition => addition.Name.Value));
        Assert.Equal(["Medium"], onNew.Vocabularies.Changes.Select(change => change.Name.Value));
        Assert.Equal(["Installation.csv", "Painting.csv"], onNew.WorkFiles.Select(file => file.FileName).Order(StringComparer.Ordinal));
        Assert.All(onNew.WorkFiles, file => Assert.Empty(file.Plan.Errors));
        Assert.All(onNew.WorkFiles, file => Assert.Single(file.Plan.Additions));
        // the first file creates it, and the second finds it
        Assert.Equal(["Gardens"], onNew.WorkFiles.SelectMany(file => file.Plan.NewCollections).Select(collection => collection.Name));
        Assert.Equal(3, onNew.Images.ToAddCount);
        Assert.Equal(2, onNew.Images.Works.Single(work => work.Title.Value == "Dawn").ToAdd.Count);
        Assert.Empty(onNew.Images.MissingFiles);
        Assert.Empty(onNew.Images.UnlistedFiles);
        var post = Assert.Single(onNew.Posts);
        Assert.Equal(PostImportOutcome.WillAdd, post.Outcome);
        Assert.Equal(1, post.RelinkedWorkCount);
        Assert.Empty(post.PictureOnlyWorks);
        Assert.Empty(post.BrokenLinks);

        var onSame = await ReviewAsync(folder, from.Id);

        Assert.Empty(onSame.WorkTypes.Additions);
        Assert.Empty(onSame.Vocabularies.Changes);
        Assert.All(onSame.WorkFiles, file => Assert.Empty(file.Plan.Additions));
        Assert.Equal(0, onSame.Images.ToAddCount);
        Assert.Equal(3, onSame.Images.Works.Sum(work => work.AlreadyThereCount));
        Assert.Equal(PostImportOutcome.Skipped, Assert.Single(onSame.Posts).Outcome);
    }

    // the wording goes only to a website still on the defaults: words a website chose itself are
    // kept, as the import only adds
    [Fact]
    public async Task TheWholeWebsiteImportBringsTheWordingToAWebsiteOnTheDefaults()
    {
        var from = await app.MakeSiteAsync();
        var wording = new SiteWording(new NounChoice("Project", "Projects", KeepsCase: false), new NounChoice("NFT", "NFTs", KeepsCase: true));
        await Wording(from.Id).UpdateAsync(wording);
        var folder = await DownloadEverythingAsync(from);
        var fresh = await app.MakeSiteAsync();
        var own = await app.MakeSiteAsync();
        var ownWording = new SiteWording(new NounChoice("Body of work", "Bodies of work", KeepsCase: false), NounChoice.Default);
        await Wording(own.Id).UpdateAsync(ownWording);

        var onFresh = (await ReviewAsync(folder, fresh.Id)).Wording;
        var onOwn = (await ReviewAsync(folder, own.Id)).Wording;
        await WordingImportPlanner.ImportAsync(onFresh.Wording, Wording(fresh.Id));
        await WordingImportPlanner.ImportAsync(onOwn.Wording, Wording(own.Id));

        Assert.Equal(WordingImportOutcome.Same, (await ReviewAsync(folder, from.Id)).Wording.Outcome);
        Assert.Equal((WordingImportOutcome.Set, wording), (onFresh.Outcome, await Wording(fresh.Id).GetAsync()));
        Assert.Equal((WordingImportOutcome.Kept, ownWording), (onOwn.Outcome, await Wording(own.Id).GetAsync()));
    }

    // Each catalog stage runs against the website as the one before left it, and a second run adds nothing
    [Fact]
    public async Task TheWholeWebsiteImportBringsTheCatalogOverOnce()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 4];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(new WorkTypeName("Installation"), [WorkField.DateCreated]);
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var gardens = await fromCatalog.Collections.AddAsync(new CollectionName("Gardens"), new CollectionSlug("gardens"));
        await fromCatalog.Works.AddManyAsync(
            [
                Addition(
                    painting, "Dawn", "Early light.", dimensions: null, duration: null, termIds: [oil], collectionIds: [gardens], products: [],
                    images: [await SaveOriginalAsync(from.Id, dawnBytes)]
                ),
                Addition(installation, "Room", description: null, dimensions: null, duration: null, termIds: [oil], collectionIds: [gardens], products: []),
            ]
        );
        var folder = await DownloadEverythingAsync(from);
        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        var repositories = Repositories(toCatalog);
        var stages = new List<WebsiteImportStageDone>();

        var problem = await WebsiteCatalogImporter.ImportAsync(folder, repositories, SiteWording.Default, stage => { stages.Add(stage); return Task.CompletedTask; });

        Assert.Null(problem);
        Assert.Equal(
            ["Work types: 1", "Vocabularies: 1", "Vocabulary terms: 1", "Works: Painting: 1", "Works: Installation: 1"],
            stages.Select(stage => $"{stage.Stage}: {stage.AddedCount}")
        );
        Assert.Equal(Describe(await fromCatalog.Works.GetAllAsync()), Describe(await toCatalog.Works.GetAllAsync()));

        stages.Clear();
        Assert.Null(await WebsiteCatalogImporter.ImportAsync(folder, repositories, SiteWording.Default, stage => { stages.Add(stage); return Task.CompletedTask; }));
        Assert.All(stages, stage => Assert.Equal(0, stage.AddedCount));

        // the images stage then sends Dawn's image to the work now here, and a second run finds it there
        var toDawn = (await toCatalog.Works.GetAllAsync()).Single(work => work.Name.Value == "Dawn");
        var images = Assert.Single((await ReviewAsync(folder, to.Id)).Images.Works);
        Assert.Equal((toDawn.Id, 1), (images.WorkId, images.ToAdd.Count));

        await toCatalog.Images.AppendImageAsync(toDawn.Id, await SaveOriginalAsync(to.Id, dawnBytes), Sha256(dawnBytes));

        var again = Assert.Single((await ReviewAsync(folder, to.Id)).Images.Works);
        Assert.Equal((0, 1), (again.ToAdd.Count, again.AlreadyThereCount));
    }

    // The posts stage plans against the website as the images left it: a picture links to its
    // work only once the work's image is here, never to a placeholder
    [Fact]
    public async Task TheWholeWebsitePostsLinkOnlyToWorksWhoseImagesAreHere()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 5];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var dawnImage = await SaveOriginalAsync(from.Id, dawnBytes);
        await SaveWebCopyAsync(from.Id, dawnImage.StorageKey);
        var dawn = await fromCatalog.Works.AddAsync(
            Addition(
                await TypeIdAsync(fromCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [],
                images: [dawnImage]
            )
        );
        var body = new PostBody(
            """{"ops":[{"insert":{"artshop-work":{"workId":WORK_ID,"storageKey":"WORK_KEY"}}},{"insert":"\n"}]}"""
                .Replace("WORK_ID", $"{dawn.Id.Value}")
                .Replace("WORK_KEY", dawnImage.StorageKey)
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
            SiteWording.Default,
            _ => Task.CompletedTask
        );

        async Task<PostImportItem> PlanAsync() =>
            Assert.Single(PostImportPlanner.Plan(folder.Posts, await PostImportTarget.LoadAsync(folder.Posts, toPosts, toCatalog.Collections, toCatalog.Works, toCatalog.Images, toStorage)));

        // Dawn is here, but not its image: the images stage didn't get it in
        var beforeImages = await PlanAsync();
        Assert.Equal(0, beforeImages.RelinkedWorkCount);
        Assert.Equal(["Dawn"], beforeImages.PictureOnlyWorks);

        var toDawn = (await toCatalog.Works.GetAllAsync()).Single(work => work.Name.Value == "Dawn");
        var toImage = await SaveOriginalAsync(to.Id, dawnBytes);
        await toCatalog.Images.AppendImageAsync(toDawn.Id, toImage, Sha256(dawnBytes));

        var run = new PostImportRun([await PlanAsync()]);
        Assert.Equal(1, run.Items[0].RelinkedWorkCount);
        var batch = Assert.Single(run.Start());
        Assert.Empty(batch.FileIds);

        await run.PostUploadedAsync(batch.Index, toPosts, toStorage, NullLogger.Instance);

        Assert.Null(run.Results[batch.Index]);
        Assert.Empty(run.Start());
        var imported = await toPosts.GetBySlugAsync(new PostSlug("morning"));
        Assert.NotNull(imported);
        Assert.Equal(
            new WorkEmbedBlock(toDawn.Id, toImage.StorageKey, EmbedImageSize.Medium, EmbedLayout.Center, Caption: null),
            Assert.Single(PostDocumentParser.Parse(imported.Body).Blocks.OfType<WorkEmbedBlock>())
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
    // vocabularies, then each type's works), the catalog comes back the same
    [Fact]
    public async Task ImportingTheExportIntoAnotherWebsiteGivesTheSameCatalog()
    {
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        var installation = await fromCatalog.Types.AddAsync(
            new WorkTypeName("Installation"),
            [WorkField.DateCreated, WorkField.HeightAndWidth, WorkField.Depth, WorkField.Duration]
        );
        var medium = await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [painting, installation]);
        var oil = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Oil"));
        var bronze = await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Bronze"));
        await fromCatalog.Terms.AddAsync(medium, new VocabularyTermName("Unused"));
        var gardens = await fromCatalog.Collections.AddAsync(new CollectionName("Gardens; Summer"), new CollectionSlug("gardens-summer"));
        var original = (await fromCatalog.ProductTypes.GetAllAsync()).Single(type => type.IsDefault);

        await fromCatalog.Works.AddManyAsync(
            [
                Addition(
                    painting,
                    "Roses, at dusk",
                    "Painted \"en plein air\".\nVarnished in 2020.",
                    new DimensionsCentimeters(new Dimensions(60.96m, 45.72m, null)),
                    duration: null,
                    termIds: [oil],
                    collectionIds: [gardens],
                    products: [new ProductAddition(original.Id, Label: null, 950m, EditionSize: 1, Stock: 1)]
                ),
                Addition(painting, "=Untitled", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: []),
                Addition(
                    installation,
                    "Room of echoes",
                    description: null,
                    new DimensionsCentimeters(new Dimensions(300m, 400m, 250.5m)),
                    TimeSpan.FromMinutes(12),
                    termIds: [bronze],
                    collectionIds: [gardens],
                    products: []
                ),
            ]
        );

        var client = await app.SignedInClientAsync(from.Host, from.OwnerEmail);
        var files = await ReadZipAsync(await client.GetAsync(ExportEndpoints.CatalogPath, TestContext.Current.CancellationToken));
        Assert.Contains("Painting,\"Roses, at dusk\",roses-at-dusk,Original,,950.00,1,1", files[CatalogExportArchive.ProductsFileName]);
        // a spreadsheet would run it as a formula without the '
        Assert.Contains("\"'=Untitled\"", files[$"{CatalogExportArchive.WorksFolder}/Painting.csv"]);

        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);
        // the collection name has a semicolon, so the export chose the next separator
        const char listSeparator = '|';

        var typePlan = WorkTypeImportPlanner.Plan(files[CatalogExportArchive.WorkTypesFileName], listSeparator, await SetupAsync(toCatalog));
        Assert.Empty(typePlan.Errors);
        await WorkTypeImportPlanner.ApplyAsync(typePlan, toCatalog.Types);

        var vocabularyPlan = VocabularyImportPlanner.Plan(files[CatalogExportArchive.VocabulariesFileName], listSeparator, await SetupAsync(toCatalog));
        Assert.Empty(vocabularyPlan.Errors);
        await VocabularyImportPlanner.ApplyAsync(vocabularyPlan, toCatalog.Vocabularies, toCatalog.Terms);

        foreach (var typeName in (string[])["Painting", "Installation"])
        {
            var snapshot = await WorkImportCatalogSnapshot.LoadAsync(
                await TypeIdAsync(toCatalog, typeName),
                toCatalog.Types,
                toCatalog.Vocabularies,
                toCatalog.Collections,
                toCatalog.ProductTypes,
                toCatalog.Works
            );
            Assert.NotNull(snapshot);
            var plan = WorkImportPlanner.Plan(
                files[$"{CatalogExportArchive.WorksFolder}/{typeName}.csv"],
                new WorkImportSettings(LengthUnit.Centimeters, original.Id, IsOneOfAKind: true, listSeparator),
                snapshot
            );
            Assert.Empty(plan.Errors);
            await toCatalog.Works.AddManyAsync([.. plan.Additions.Select(addition => addition.Addition)]);
        }

        var fromSetup = await SetupAsync(fromCatalog);
        var toSetup = await SetupAsync(toCatalog);
        Assert.Equal(CatalogSetupCsvExport.WorkTypes(fromSetup, listSeparator), CatalogSetupCsvExport.WorkTypes(toSetup, listSeparator));
        Assert.Equal(CatalogSetupCsvExport.Vocabularies(fromSetup, listSeparator), CatalogSetupCsvExport.Vocabularies(toSetup, listSeparator));
        Assert.Equal(Describe(await fromCatalog.Works.GetAllAsync()), Describe(await toCatalog.Works.GetAllAsync()));
    }

    // Two works of a type titled "Dawn", told apart by their slugs: both come over, and each
    // image goes only to the work with its images.csv slug, never to whichever Dawn comes first.
    // Back on the website it came from, each image is found on its own work
    [Fact]
    public async Task TheWholeWebsiteMoveKeepsTwoSameTitledWorksApart()
    {
        byte[] firstBytes = [0xFF, 0xD8, 0xFF, 6];
        byte[] secondBytes = [0xFF, 0xD8, 0xFF, 7];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        await fromCatalog.Works.AddAsync(
            Addition(painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [], images: [await SaveOriginalAsync(from.Id, firstBytes)])
        );
        await fromCatalog.Works.AddAsync(
            Addition(painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [], images: [await SaveOriginalAsync(from.Id, secondBytes)])
        );
        var folder = await DownloadEverythingAsync(from);

        // the work file's slugs tell the two apart, so both come over, each with its own image
        var onNew = await ReviewAsync(folder, (await app.MakeSiteAsync()).Id);

        Assert.Equal(["dawn", "dawn-2"], Assert.Single(onNew.WorkFiles).Plan.Additions.Select(addition => addition.Addition.CandidateSlug.Value));
        Assert.Equal(
            [[Sha256(firstBytes)], [Sha256(secondBytes)]],
            onNew.Images.Works.Select(work => work.ToAdd.Select(image => image.Sha256).ToList()).ToList()
        );
        Assert.Empty(onNew.Images.WithoutWork);

        var to = await app.MakeSiteAsync();
        Assert.Null(await WebsiteCatalogImporter.ImportAsync(folder, Repositories(Catalog(to.Id)), SiteWording.Default, _ => Task.CompletedTask));
        Assert.Equal(["dawn", "dawn-2"], (await Catalog(to.Id).Works.GetAllAsync()).Select(work => work.Slug.Value).Order(StringComparer.Ordinal));

        // a website with one Dawn of its own, under the first one's slug, takes only its image and
        // adds the second Dawn
        var withDawn = await app.MakeSiteAsync();
        var withDawnCatalog = Catalog(withDawn.Id);
        await withDawnCatalog.Works.AddAsync(
            Addition(await TypeIdAsync(withDawnCatalog, "Painting"), "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [])
        );

        var onOneDawn = await ReviewAsync(folder, withDawn.Id);

        Assert.Equal(["dawn-2"], Assert.Single(onOneDawn.WorkFiles).Plan.Additions.Select(addition => addition.Addition.CandidateSlug.Value));
        Assert.Equal(2, onOneDawn.Images.ToAddCount);
        Assert.Empty(onOneDawn.Images.WithoutWork);

        var onSame = await ReviewAsync(folder, from.Id);

        Assert.Equal(0, onSame.Images.ToAddCount);
        Assert.All(onSame.Images.Works, work => Assert.Equal(1, work.AlreadyThereCount));
        Assert.Equal(2, onSame.Images.Works.Count);
    }

    // images.csv against the folder: a listed file that isn't there, a file that isn't listed, an
    // image for a work that won't be here, and rows it can't read. None stops the import
    [Fact]
    public async Task TheWholeWebsiteReviewListsTheImagesItCantImport()
    {
        byte[] dawnBytes = [0xFF, 0xD8, 0xFF, 8];
        byte[] duskBytes = [0xFF, 0xD8, 0xFF, 9];
        var from = await app.MakeSiteAsync();
        var fromCatalog = Catalog(from.Id);
        var painting = await TypeIdAsync(fromCatalog, "Painting");
        await fromCatalog.Works.AddManyAsync(
            [
                Addition(painting, "Dawn", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [], images: [await SaveOriginalAsync(from.Id, dawnBytes)]),
                Addition(painting, "Dusk", description: null, dimensions: null, duration: null, termIds: [], collectionIds: [], products: [], images: [await SaveOriginalAsync(from.Id, duskBytes)]),
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
        Assert.Equal(["Painting/Ghost.jpg"], review.Images.WithoutWork);
        Assert.Equal(2, review.Images.Errors.Count);
        Assert.Contains(review.Images.Errors, error => error.Column == ImageExportArchive.ImageListHeaders.File);
        // Dusk's own row still counts
        Assert.Equal([Sha256(duskBytes)], Assert.Single(review.Images.Works).ToAdd.Select(image => image.Sha256));
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
        var problem = await WebsiteCatalogImporter.ImportAsync(folder, Repositories(toCatalog), SiteWording.Default, stage => { stages.Add(stage); return Task.CompletedTask; });

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
        var installation = await fromCatalog.Types.AddAsync(new WorkTypeName("Installation"), [WorkField.DateCreated]);
        await fromCatalog.Vocabularies.AddAsync(new VocabularyName("Medium"), isMutuallyExclusive: false, [installation]);
        var downloaded = await DownloadEverythingAsync(from);
        // as though Installation were gone by the time the import ran
        var folder = downloaded with
        {
            WorkTypesCsv = string.Join("\r\n", Unwrap.Value(downloaded.WorkTypesCsv).Split("\r\n").Where(line => !line.StartsWith("Installation"))),
        };
        var to = await app.MakeSiteAsync();
        var toCatalog = Catalog(to.Id);

        var problem = await WebsiteCatalogImporter.ImportAsync(folder, Repositories(toCatalog), SiteWording.Default, _ => Task.CompletedTask);

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

        Assert.Null(await WebsiteCatalogImporter.ImportAsync(folder, Repositories(toCatalog), SiteWording.Default, stage => { stages.Add(stage); return Task.CompletedTask; }));

        Assert.Equal(
            ["Work types: 0", "Vocabularies: 0", "Vocabulary terms: 1"],
            stages.Select(stage => $"{stage.Stage}: {stage.AddedCount}")
        );
    }

    private async Task<WebsiteImportFolder> DownloadEverythingAsync(TestSite site)
    {
        var client = await app.SignedInClientAsync(site.Host, site.OwnerEmail);
        return await WebsiteFolderAsync(await ReadZipAsync(await client.GetAsync(ExportEndpoints.EverythingPath, TestContext.Current.CancellationToken), ReadBytesAsync));
    }

    private static CatalogRepositories Repositories(SiteCatalog catalog) =>
        new(catalog.Fields, catalog.Types, catalog.Vocabularies, catalog.Terms, catalog.Collections, catalog.ProductTypes, catalog.Works);

    private async Task<WebsiteImportReview> ReviewAsync(WebsiteImportFolder folder, SiteId siteId)
    {
        var catalog = Catalog(siteId);
        var target = await WebsiteImportTarget.LoadAsync(
            folder,
            catalog.Fields,
            catalog.Types,
            catalog.Vocabularies,
            catalog.Collections,
            catalog.ProductTypes,
            catalog.Works,
            catalog.Images,
            Posts(siteId),
            Wording(siteId),
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

    private static WorkCatalogAddition Addition(
        WorkTypeId typeId,
        string name,
        string? description,
        DimensionsCentimeters? dimensions,
        TimeSpan? duration,
        IReadOnlyList<VocabularyTermId> termIds,
        IReadOnlyList<CollectionId> collectionIds,
        IReadOnlyList<ProductAddition> products,
        IReadOnlyList<WorkImage>? images = null
    ) =>
        new(
            typeId,
            new WorkName(name),
            WorkSlug.FromName(name),
            description,
            DateCreated: null,
            dimensions,
            duration,
            Images: images ?? [],
            MainImageIndex: 0,
            termIds,
            collectionIds,
            NewCollectionNames: [],
            products
        );

    private sealed record SiteCatalog(
        WorkRepository Works,
        WorkFieldRepository Fields,
        WorkTypeRepository Types,
        VocabularyRepository Vocabularies,
        VocabularyTermRepository Terms,
        CollectionRepository Collections,
        ProductTypeRepository ProductTypes,
        WorkImageRepository Images
    );

    private SiteCatalog Catalog(SiteId siteId)
    {
        SiteDatabase database = app.Services.GetRequiredService<SiteDatabases>().For(siteId);
        return new SiteCatalog(
            new WorkRepository(database),
            new WorkFieldRepository(database),
            new WorkTypeRepository(database),
            new VocabularyRepository(database),
            new VocabularyTermRepository(database),
            new CollectionRepository(database),
            new ProductTypeRepository(database),
            new WorkImageRepository(database)
        );
    }

    // an original on the site's disk, as an upload leaves it
    private async Task<WorkImage> SaveOriginalAsync(SiteId siteId, byte[] bytes)
    {
        var storageKey = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(
            ImageStorage.ForSite(app.Services.GetRequiredService<ImageStorageSettings>(), siteId).OriginalPath(storageKey),
            bytes,
            TestContext.Current.CancellationToken
        );

        return new WorkImage(storageKey, OriginalFileName: null, Width: 10, Height: 10, BlurDataUri: null);
    }

    private PostRepository Posts(SiteId siteId) =>
        new(app.Services.GetRequiredService<SiteDatabases>().For(siteId));

    private WordingRepository Wording(SiteId siteId) =>
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

    private static async Task<WorkTypeId> TypeIdAsync(SiteCatalog catalog, string name) =>
        (await catalog.Types.GetAllAsync()).Single(type => type.Name.Value == name).Id;

    // what a work holds, less its ids, which differ between websites, and its products, which the
    // import can't bring back
    private static List<string> Describe(IEnumerable<Work> works) =>
        [
            .. works
                .Select(work => string.Join(
                    " | ",
                    work.Type.Name.Value,
                    work.Name.Value,
                    work.Slug.Value,
                    work.Description,
                    work.DateCreated?.Text,
                    work.Dimensions?.Text,
                    work.Duration,
                    string.Join(", ", work.Collections.Select(collection => collection.Name.Value)),
                    string.Join(", ", work.VocabularyTerms.Select(term => $"{term.VocabularyName.Value}: {term.Name.Value}"))
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
