# Todo: interactive image upload on the add-artwork form

Goal: drop or pick multiple images on `/admin/catalog/artworks/add`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the artwork.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

## Where this stands — 2026-09-21, later: porting to Postgres

**SQL Server is being replaced by Postgres 18, on the `postgresPort` branch.** `main` keeps the
SQL Server version. The reason: the VPS has 2 GB of RAM, and SQL Server on Linux won't start in
less than that. Azure SQL's free serverless tier was the plan until the numbers came out. It allows
about 110 awake minutes a day, which public crawler traffic would use up, and each wake-up makes a
visitor wait about a minute. The full phased plan is at
`~/.claude/plans/refactored-percolating-backus.md`. **Read it before starting.** Claude writes the
port and Mike reviews it, with every place Postgres behaves differently called out. Names are
snake_case.

**Phase 1 is done (uncommitted on `postgresPort`), and the solution builds with no warnings.** It
has never run against Postgres:
- Npgsql, dbup-postgresql and the Npgsql EF provider replace the SQL Server packages.
- `NpgsqlDataSource` replaces `SqlConnectionFactory`, which is deleted.
- `DatabaseInitializer` is deleted. DbUp's `EnsureDatabase` creates the domain database, and EF's
  `MigrateAsync()` at startup creates and migrates the identity database, so the manual
  `dotnet ef database update` step is gone. The identity migration was regenerated for Npgsql.
- `SqlErrorNumbers` became `SqlStates`, holding SQLSTATE codes `SH001`–`SH014`. The SQL standard
  reserves the classes starting A–H, so ours is `SH`. `SqlErrors` matches on `SqlState` and
  `ConstraintName`, never on the message text.
- `DefaultTypeMap.MatchNamesWithUnderscores = true`.
- The test fixture uses `artist_shop_tests` and `DROP DATABASE … WITH (FORCE)`.
- Dev Postgres is `artist-shop-postgres` on host port **5434** (5432 and 5433 are speed-dungeon's
  and snowauth's). `POSTGRES_PASSWORD` is in `.env`. `env.sh`, `dev.sh`, `commands.md` and the
  formatter dialect are updated. If `artist-shop-mssql` is still running, stop it
  (`docker stop artist-shop-mssql`); its volume is kept for `main`.

**The app won't boot yet**, and that's expected. The schema scripts and the 50 procedures are still
T-SQL, and every repository still calls them with `CommandType.StoredProcedure`.

**Next: Phase 2, the schema.** Rewrite `Database/Scripts/` as a fresh Postgres set, since nothing
is released and nothing needs migrating. The T-SQL table types in 0002–0006 disappear. The two
parts to get right:
- The ICU collations, applied per column: `und-u-ks-level2` for case-insensitive names,
  `und-u-ks-level1` for the accent-insensitive search.
- `UNIQUE NULLS NOT DISTINCT` on Products.

Then Phase 3, one area at a time with its database tests green, **ProductTypes + ArtworkFields
first**. The unique-constraint names in the repositories (`UniqueNameConstraint` and the others)
change to the new snake_case names as each area is ported. Ask Mike before running tests.

## Where this stands — 2026-09-21, earlier

**The artwork page is built.** `/artworks/{slug}` is the big image, a thumbnail picker, a
full-screen view, and the panel of everything the work carries. It was done in four slices, each
one working before the next began, and everything below this section is history.

**The panel** (`ArtworkDetails`) shows the title, the work type, the products and their prices,
then date, size, duration, the vocabulary terms grouped under their vocabulary, and the series it
belongs to, linked. Only rows with a value appear — a field the work type has switched off is null
on the artwork anyway, so "show what is filled in" needs no knowledge of the type's fields.
Prices read `$1,200` through `Utilities/PriceText`. **Mike, 2026-09-21: a price is an amount in a
currency, the way a dimension is centimetres, and ought to be shown in the visitor's own. That is
its own session.** `PriceText` is the one place it is spelled, so that work has a single owner.
Products can still only be created by the CSV import — neither artwork form posts them and there
is no products admin, which is the next real gap on the commerce side.

**The images.** The stage keeps the shape of the artwork's *tallest* image, worked out on the
server, so clicking through the picker can never move the page; each picture sits in the middle of
it at its own shape, with its blur behind it, and the bars either side belong to the frame.
`TileImage` grew `IsEager` and `IsUncropped` for it. Every image is rendered into the stage hidden,
so the browser fetches one the first time it is shown and a repeat view is a repaint.

**The addresses.** `ArtworkPageQuery` owns the page's whole query string — `series` and `image` —
and builds every link to it: the series tiles, the picker, and the neighbour links. Images count
from one in the address and from zero everywhere else, converted at exactly two points. A thumbnail
is a link to *this page showing that image*, never to the image file: with the script it is caught
and swapped in place, and `history.replaceState` keeps the address accurate without stacking
history entries, so it can still be copied and sent.

**The full-screen view** is `Components/Dialogs/ImageLightbox`, not `ModalDialog` — that one opens
as soon as it is rendered and carries a title bar and padding a picture has no use for. It knows
nothing about artworks: it is handed an array of `<img>` and an index, and reports where it moved
with a bubbling `lightboxchange` event, so the gallery and the page follow it. Its picture copies
the `srcset` off the page's image rather than building one, so nothing downloads twice.
`LightboxTrigger` makes whatever should open it into a `<button>` — the picture's frame itself,
not a separate control — so the keyboard reaches it too. app.css gives it the zoom cursor only when
`html:has(image-lightbox:defined)`, asked of the document rather than tracked in a flag, because an
enhanced navigation connects the incoming lightbox and disconnects the outgoing one in whichever
order it likes. The gallery reads which image is showing off the page when it opens the lightbox,
never from a field: enhanced navigation patches `<artwork-gallery>` in place without connecting it
again, so a stored index survived into the next artwork (found and fixed 2026-09-21).

**Who owns left and right.** The lightbox owns them while it is open; the page owns them otherwise,
through `wwwroot/js/arrow-key-links.js`, which follows whichever links carry
`data-arrow-key-link`. It ignores held modifiers, anything typed in a field, and any moment a
`dialog[open]` exists. It clicks the link rather than setting the location, so enhanced navigation
patches the page as for any internal link.

**Moving through a series.** `dbo.GetArtworkNeighboursInSeries` filters the series to the places
a visitor may be sent once, in a CTE, then anchors on the artwork's own junction row and takes the
nearest of them either side with two `OUTER APPLY`s. `UNIQUE (SeriesId,
SortOrder)` is what makes strict `<` and `>` safe. `@OnlyArtworksWithImages` keeps the visitor rule
a parameter, as `GetSeriesWithCovers` does. The page falls back to the artwork's first series by
name (through `SeriesOrder`) when it was reached cold, so the arrows work from a shared link. An
artwork with no image still renders for anyone with its address, deliberately: an artist who lands
there sees an incomplete work to fix rather than a 404 for something that exists.

**Addresses have one spelling each.** `ArtworkPageQuery.Url` for the public artwork page and
`Components/PageUrls` for the series page and the artwork edit page; nothing else writes those
strings but the pages' own `@page` lines.
`tests/.../ArtworkNeighboursTests.cs` covers it, including a reordered series — **not yet run.**

### Next session: a first deployment (Mike, 2026-09-21)

The app in docker compose on the VPS, the database on **Azure SQL's free tier** (account set up
already), and the images on the VPS filesystem, reached from inside the container. The VPS has
2 GB of RAM and SQL Server in a container is what eats it, so renting the database out is what
makes the rest fit. See `deployment-notes.md`, and the notes on auto-pause, collation at creation
time, the firewall and the bind mount before starting.

If the free tier's shape doesn't suit the app, Mike's fallback is moving off T-SQL altogether — so
the first question is only whether it deploys at all, and it isn't worth sinking time into
Azure-specific tuning before that is answered.

### Worth doing next

- **A products admin**, so prices exist for work that wasn't imported.
- **The public artwork list**, the "search everything" way in. The admin list's query already takes
  every filter as a parameter; what's missing is a public page and a decision about search.
- The `@TODO` on `ImageVariants.Widths` is now answerable: the stage asks for
  `(min-width: 1200px) 760px, (min-width: 1024px) 63vw, 100vw`, so 1600 is the widest that earns
  its place until the layout changes.
- Small: no blur behind the lightbox picture, so a first look at an unseen image is blank against
  black; long titles in the neighbour links want truncating.

## Where this stands — 2026-09-20, end of the second session that day

**A visitor can now browse the catalog.** `/` is a grid of series cards (cover, name, count),
`/series/{slug}` is that series' artworks in the order the artist dragged them, and both lead to
`/artworks/{slug}`, which is still the old stub. Everything below the admin list in this file is
history; this section and the next are what the next session needs.

**The public rules, decided by Mike:** a visitor sees an artwork only when it has an image, and
sees it whether or not anything is for sale. A series with no cover — which is the same as a
series none of whose artworks are photographed — is hidden, and a card's count follows the same
rule, so it can't promise more than the page it opens. Artworks in no series stay unreachable
until there is a public version of the artwork list to link to.

The artist still sees everything, so those rules are parameters, never baked in.
`dbo.GetSeriesWithCovers` takes `@OnlyArtworksWithImages`, and `SeriesRepository` names the two
audiences: `GetAllWithCoversAsync` for the artist, `GetVisibleWithCoversAsync` for a visitor. The
series page calls `GetArtworkList` with `HasImages: true, IsForSale: null`, so the policy is one
filter object on one line.

`ArtworkListSort.SeriesOrder` was added for it: every other sort reads a column on `dbo.Artworks`,
so without it the public page would have thrown away the artist's dragging and the starred cover.
It reads `junction.SortOrder` for the chosen series through an `OUTER APPLY`, means nothing with
no series chosen, and the admin filter bar offers it only when a series is picked.
`ArtworkListItem` gained `Slug`, since public links need it, and `dbo.GetSeriesBySlug` is new.

**Shared, in `Components/Catalog/`:** `TileImage` (the box, the blur behind it while it loads, a
`srcset` over the variants the image actually has, AVIF), `TileGrid`, `LinkTile`, and
`ArtworkListQuery` + `ArtworkListPaging`, which moved out of the admin folder when the series page
became their second user. `SeriesGrid` is a section component, so the artist's name and anything
else goes above it in `Home.razor` without touching the grid. A masonry mode is a second grid
component and a different `BoxClass`; nothing else changes.

`ArtworkThumbnail` is now six lines over `TileImage` — once the blur went in, the admin thumbnail
and a browse tile differed only in width. It keeps its name and its `Class` default, and stays the
one place that names `AdminThumbnailWidth`.

## Next: the artwork page at `/artworks/{slug}` (Mike, 2026-09-20)

The stub there today is two `<h1>`s and an unstyled blur image. What it should be:

- **The image, large.** `ImageVariants.Widths` tops out at 1600, and the `@TODO` on that array —
  measure the real element and derive the widths — is finally answerable once this page is laid out.
- **A picker under it:** the artwork's other images as thumbnails, the size the admin sees.
- **Clicking the main image fills the screen**, and left and right move through that artwork's
  images. `ModalDialog` is the obvious start, but `.artist-shop-modal` is `max-h-[85vh]` with
  padding, so a full-bleed variant is needed.
- **Left and right on the page itself** go to the previous and next artwork **in the series**. An
  artwork can be in several series, so the page has to be told which one it was reached through —
  a query parameter from the series page is the obvious answer, with a fallback when someone
  arrives at the address cold. Decide that before writing the page.
- **A panel beside or under it** holding the title, the products and their prices, the date
  created, the dimensions, the description, and every vocabulary term the artwork carries. Only
  what the work type has switched on: a photograph with no depth shouldn't show an empty row.
  Worth deciding at the same time: the work type itself, which series it belongs to (linked), the
  duration for time-based work, and what a product row says when it is sold out or has no price.

Arrow keys mean a script, since the page is static SSR — a custom element in the house style, the
way `<modal-dialog>` and `<submit-on-change>` work.

### Also done 2026-09-20, after the admin list

**A review pass over the admin list**, all of it applied: the primary image is read the one way
(`IsPrimary = 1`, as the series procedures do) rather than by a second rule; the image count and
the for-sale test are worked out once in a `CROSS APPLY` instead of twice each; the sort numbers
are named constants like `GetArtworkNameMatches` does; `dbo.GetVocabulariesWithTerms` absorbed the
per-type copy through a nullable `@ArtworkTypeId`; vocabulary **terms** are sorted by name on the
filter bar, which they weren't, through the shared `VocabularyOrder`; `CheckboxGroup` and
`YesNoSelect` in `Components/Forms/` took about 70 lines of repeated markup out of
`ArtworkListFilters`; `.artist-shop-field-label` is one owner for the label styling.
`ArtworkListQuery.Read` is called with named arguments — six of its eight parameters are strings.

**Back after a CSV import no longer says "Document Expired".** Both import forms now post with
`Enhance`. Blazor's enhanced submit keeps a multipart body (`body = FormData`, so the file still
arrives) and pushes a history entry only for GET, so the review stops being a posted document that
the antiforgery headers forbid the browser to redisplay. The dialog still opens because
`<modal-dialog>` runs `showModal()` from `connectedCallback` and the review is rendered only when
there is a plan, so the enhanced patch *inserts* the element. That is what the comment on
`ModalDialog` now says: it opens when it arrives as a new element, however the page got there.

**The import's "already in the catalog" is per work type now.** It loaded every name in the
catalog, which the database never required (only `Slug` is unique) and which the bulk image
uploader never did either — it matches within a type. So a photograph and a screenshot can share a
title, while a repeat inside one type is still skipped, which is what keeps the uploader
unambiguous. `dbo.GetArtworkNames` takes `@ArtworkTypeId`, the snapshot field is `TypeArtworkNames`
beside `TypeVocabularies`, and the review says "Already in the catalog (Photograph)". Note that
slugs stay globally unique, so the second `Harbour` gets `/artworks/harbour-2` silently; if
cross-type repeats become normal, the public address is the next thing to think about.

**A clicked button goes busy where the artist is looking.** `ButtonBasic` carries a hidden spinner
that `ButtonBasic.razor.js` reveals by setting `data-busy` on `event.submitter` when an enhanced
form is submitted, cleared on `enhancedload`; CSS grays it and blocks further clicks. Immediate, no
delay. It covers every enhanced form at once (both import forms, add and edit artwork, add series,
add term). Not islands — their forms go over the circuit, never raise `enhancedload`, and would
spin for good; those want `IsWorking`, as `ConfirmDialog` has. Nothing was clicked when the filter
bar submits itself, so nothing spins there, and the artwork list keeps its own fade and indicator.
**Mike, 2026-09-20: no page-level loading indicator.** I put one in `MainLayout` and it was wrong —
indications belong on the button, in the table, where people expect to find them.

### The edit page, as built on 2026-09-19

- **Shared SQL, so add and edit can't drift.** `dbo.CheckArtworkChoicesAreCurrent` (type exists, no
  value for a switched-off field, terms and series still exist), `dbo.SetArtworkVocabularyTerms`
  (replace every row) and `dbo.SetArtworkSeries` (drop unticked, append newly ticked at `MAX + 1`).
  Both setters work unchanged on a brand-new artwork, where the `DELETE`s find nothing, so
  `AddArtwork` lost about 85 lines of checks and both junction writes to them. A table-valued
  parameter can be passed straight on to a nested procedure as `READONLY`, and deferred name
  resolution means DbUp's file order in the procedures pass doesn't matter.
- **The type is not a parameter to `UpdateArtwork`**: it can't change, so the procedure reads it off
  the row it locks with `UPDLOCK`, and a missing row is error 50003.
- **Slug rule**: keep the current slug when it equals the new name's slug or that slug plus a whole
  number, so an unchanged save doesn't walk `sunset-2` along; otherwise `ResolveArtworkSlug`.
- **`DeleteArtwork` is one `DELETE`.** Every table referencing an artwork cascades, and the image
  files are left for `OrphanedImageSweeper`, the same path an abandoned upload takes.
- **Error numbers are reused, lowest first, until release** — see the comment in
  `SqlErrorNumbers.cs`. This took the freed 50003.
- **One form class, `ArtworkForm`** (was `ArtworkCatalogAdditionForm`), with `FromArtwork` to seed it
  and `ToCatalogAddition`/`ToCatalogUpdate`. `[NonEmpty]` on `Images` couldn't survive that: it would
  fail every edit save. The rule is add-only anyway — the CSV import and the bulk uploader exist to
  catalogue first and photograph later — so it moved into `AddArtwork`'s submit handler beside the
  stale-upload check. `NonEmptyAttribute` had no other user and was deleted.
- **A duration input on both forms**, and `ArtworkDuration` holds the h:mm:ss / m:ss rule that the
  CSV import already used. Without it, saving the edit form would have wiped an imported duration.
- The add page, the pickers and the setup notice moved into
  `Components/Pages/Admin/Catalog/Artworks/`, next to the edit page, matching every other admin area.

**Images on the edit page, same day.** `dbo.SetArtworkImages` joins the other three shared
procedures: the form posts the whole list in the artist's order, so it replaces every row rather
than working out which moved. Nothing refers to an `ArtworkImages` row by its `Id`, so a kept image
taking a new one costs nothing, and a removed image's file is left with no row pointing at it, which
is exactly what `OrphanedImageSweeper` collects.

- `ImagesField` takes `SelectedImages` and `SelectedPrimaryImageKey` and seeds its list from them
  once. `SelectedImage.Result` is now an `ArtworkImage` rather than the endpoint's
  `ImageUploadResult`, whose `OriginalFileName` and `BlurDataUri` are non-nullable strings while
  both columns are nullable; a fresh upload is converted to one as it completes, and from then on a
  saved image and a new one are the same thing to that component.
- **This also fixed the add page**: an add that came back with an error (a term deleted in another
  tab, say) used to redraw the island empty and lose the uploaded images, since the island renders
  the hidden inputs that carry them. Both pages now hand it `Input.ToArtworkImages()`, which is the
  artwork's images on a fresh edit page and the posted ones after a failed submit.
- `ArtworkForm.FromArtwork` sets `PrimaryImageKey` only when the main image isn't the first one, so
  the list doesn't claim the artist hand-picked an image they never touched.
- **No minimum on the edit page.** Taking every image off an artwork is allowed, because an artwork
  with no images is a normal state — the CSV import makes them that way, and the bulk uploader
  attaches to exactly those.
- The artwork names in the series page's list link to their edit pages.

**`ProductTypes.IsDefault` (2026-09-19).** A form can't name a product type: the ids are IDENTITY
values, and an unused type will be deletable once the product-type admin exists, so
`WHERE Name = 'Original'` would be a rule the database is free to break — the same reasoning that
took `ShopItemTypeId.Painting` out of the artwork types. So the row says whether it is the usual
one, `UniqueIndex_ProductTypes_Default` (filtered, `WHERE IsDefault = 1`) allows only one, and the
seed sets Original. The CSV import page now preselects it, and the products island's first row will
use the same flag. When the product-type admin is built it needs to own this: a radio per type, and
deleting the default either refused or the flag moved first.

`GetProductTypes` returns rows in no defined order, so the import page sorts them by name itself,
the way a repository's caller decides display order everywhere else here.

### What was reviewed and fixed on 2026-09-19

The 2026-09-18 work was read back and these came out of it.

- **The upload run no longer rides on one interop call.** `StartUploadAsync` awaited
  `_uploader.InvokeVoidAsync("upload", …)`, and the JS did not resolve until every file was done.
  `CircuitOptions.JSInteropDefaultCallTimeout` defaults to **one minute** and `Program.cs` does not
  change it, so a long run faulted an `@onclick` handler, which tears the circuit down. `upload` now
  launches `run` and returns; the island hears the end through `OnUploadFinished` as before. Runs of
  1:05 and 1:25 have both been measured since, so this was not hypothetical.
- **A throw in a worker no longer strands the page.** `runWorker` and `upload` had no try/catch, so
  a non-JSON 200 body would reject `Promise.all` and skip `OnUploadFinished` — leaving the island
  "uploading" for good, with no way back but a reload. `run` ends in a `finally`.
- **A dropped folder during a run is refused.** The pickers and the drop zone now sit in a
  `<fieldset disabled="@_isUploading">`; `FileDropZoneFrame` already declines a drop onto a disabled
  input. `remember()` also bails, since clearing the JS file map under the workers was the real
  damage.
- **The name pre-check applies the same length rule as the endpoint.** `ArtworkName.CanMatchAnArtwork`
  holds it once. `dbo.ArtworkNameList` is `nvarchar(200)` and SQL Server truncates rather than
  refusing, so a long file name could have matched the wrong artwork, and two long names sharing
  their first 200 characters would have violated the type's primary key — an unhandled `SqlException`
  inside a `[JSInvokable]`, so a dead circuit.
- **The report shows the file's path, not just the derived name**, and links each matched artwork.
  Duplicate names are now included in the pre-check query, grouped by `DatabaseCollationComparer`
  rather than filtered out, because the duplicate case is exactly the one rule 6 wants a link for.
- `FileDropZoneFrame` drop handler checked the loose-file input's disabled state and then took the
  directory path; it now picks the input first and guards that one.
- HEIC files get `ImageProcessor.UnsupportedHeicMessage` in the pre-check rather than being lumped
  into "not an image format we accept". `ImageUploadValidation.IsHeic` is shared with `FindProblem`.
- `ExecuteAddAsync`'s `SqlTransaction` is no longer nullable: since `AddAsync` delegates to
  `AddManyAsync`, every caller passes one.
- `CollectedFile.Path`'s comment said the path was relative to the dropped folder. It includes that
  folder's own name, which is what makes `MaximumDirectoryDepth = 2` correct.

### What was built on 2026-09-18


*Steps 6 and 7 — the browser side and the page.*
- `FileDropZoneFrame` takes an optional `DirectoryButtonLabel` and renders a second button. **One
  drop zone handles both folders and loose files**: `webkitGetAsEntry()` returns either kind, so a
  drop needs no toggle. Two buttons exist only because a `webkitdirectory` input's dialog cannot
  pick loose files. The element decides which drop path to use by looking for a folder input among
  its own children — a zone that can pick a folder can take one from a drop — and dispatches a
  bubbling `entriesdropped` carrying the entries it grabbed before the handler returned.
- `Components/Pages/Admin/Catalog/ArtworkImages/`: `UploadArtworkImages.razor` (the page, `?type=`
  preselects), `BulkImageUpload.razor` (the island), `BulkImageUpload.razor.js` (walk, metadata
  stream, upload driver), `CollectedFile`, `BulkImageFile`, `BulkImageOutcome`, `BulkImageReport`.
  The dashboard links to it per work type.
- The page has no form, so it renders `<AntiforgeryToken />` for the uploader script to find.
- The artwork type is a plain `<select>` in the island, not `SelectField`, which needs an
  `EditContext`. Changing it re-runs the pre-check against what was already dropped.
- Cap of **5,000 files**, passed to JS from the island so it is written once; the stream's byte
  allowance is derived from it and is only a backstop. Over the cap, nothing is sent and the page
  says so.
- Driver: four at a time, one bar for the whole run (bytes for files in flight, whole file once its
  answer arrives), progress throttled to four updates a second, "Processing the last images…" at
  100% with answers pending. 429/503/504 back off up to four attempts honouring `Retry-After` with
  jitter; anything else fails the file, showing the server's message only when it is short
  `text/plain`. Stop aborts and empties the queue, leaving the remaining files as "will be added" so
  Upload picks up where it left off.

**Gotcha that cost real time:** `DotNet.createJSStreamReference` is for JS **calling into** .NET and
passing a stream as an argument. When .NET calls JS and asks for an `IJSStreamReference`, it wraps
the returned value itself, so the JS function must **return the Blob directly**. Wrapping it first
fails with "Supplied value is not a typed array or blob", surfacing as a `JSException` inside the
`[JSInvokable]` and appearing only in the browser console.

### Not built on the bulk image page

- **The browser's backoff is verified, by faking the 503.** A real one needs the limiter's slot queue
  full, and on a 20-core box that is `ProcessorCount - 1` = 19 in flight plus
  `slots × (ProxyTimeout / EstimatedTimePerImage)` = 114 waiting, while the page sends four at a
  time — unreachable. So on 2026-09-18 `UploadAndAttachByNameAsync` temporarily answered 503 with
  `Retry-After: 2` to every third upload, and 150 files were run through it. **That code is removed.**
  One file ended in "Didn't upload (503)", which only happens after four attempts, so the wait, the
  retry and the attempt cap all ran; the rest retried invisibly and were added. The bar stayed smooth
  because a retry only drops that one file's bytes, under a percent of the run.
- The add-artwork form still shows a bare "Upload failed (503)." (parked since 2026-09-17). The bulk
  page now has retry and backoff worth sharing with it.
- A second image per artwork goes on through the edit artwork page (built 2026-09-19).

### Test data (2026-09-18)

`tools/generate-test-artworks/generate.sh` builds a folder of ImageMagick images and a CSV that
matches them, salted with the cases the upload page reports: a name in two series folders, a
`thumbnails` folder one level too deep, a `notes.txt`, an image with no CSV row, a series name with a
comma and one with an apostrophe. It prints the counts the page should then show, and `--clean`
removes what it made. It writes to `test-upload-files/` at the repo root, which `.gitignore` covers,
and clears that folder first. `--size 8000x6000 --format jpg` makes 48-megapixel files, to give NetVips real
work; PNG at that size is well past the 25 MB upload limit.

**(From Claude, 2026-09-19)** `--size` applies to every image, which makes generating the folder
slow. `--big N` instead leaves the run quick and gives N of the artworks a `--big-size`
(8000x6000 by default) JPEG, always JPEG whatever `--format` says. They are spread across
different series folders on purpose: the upload walks folder by folder, so big images bunched in
one folder would all arrive together instead of stretching the run out.

The existing `test-upload-files/` was not regenerated — the catalog imported from it is still
valid — so **30 of its 151 artworks, five per series, had their PNG replaced in place with a
48-megapixel JPEG** (~14 MB each). The folder is now 681 MB. `Lantern Path` was left alone so the
two-series-folder duplicate stays a small file, and the counts the page should show are unchanged:
153 images found, 3 too deep, 1 not an image.

Megapixels are what the server pays for, not file count: `ImageProcessor` decodes and then writes
three widths in two formats plus the blur. So raising `--big` is what lengthens a run; raising
`--count` adds 1200x900 files that finish almost immediately. 12000x8000 (96 megapixels) was tried
and dropped: it is inside the 100-megapixel cap, but generating two of them did not finish within
400 seconds, so building the test data costs more than the test is worth.

### Don't pass `--nologo` to `dotnet test` (From Claude, 2026-09-19)

It is forwarded to the test app, which in Microsoft Testing Platform mode then runs **zero tests**
and exits 5 without a word on stdout or stderr. It looks exactly like a broken test project.
`-v q` and `--no-build` are fine; it is `--nologo` alone.

### Enum switches are exhaustive by the compiler (2026-09-18)

`_ => throw new ArgumentOutOfRangeException(...)` was removed from all seven enum switch expressions.
It only existed to silence **CS8524**, which complains that integers with no name in the enum aren't
handled — an arm for those is dead code. Silencing it that way also silences **CS8509**, which names
an enum member no arm handles, and that one is worth having. The csproj now sets `NoWarn` for CS8524
and `WarningsAsErrors` for CS8509, so a forgotten case fails the build naming the member. Verified by
deleting a case. A value that genuinely can't occur still throws, now as `SwitchExpressionException`.

### Database tests run in sequence (2026-09-18)

`SeriesRepositoryTests.GetAllListsSeriesInTheArtistsOrder` failed about one run in four. It reads
every series, reverses the list and reorders it, and `dbo.ReorderSeries` throws 50008 when the list
isn't exactly the series that exist — so a sibling class inserting a series in between broke it. The
three new series inserts in `ArtworkRepositoryTests` made a pre-existing race likely enough to see.

**This is not the production concern it looks like.** In the app the stale list comes from an artist
pausing between loading the page and dragging, which is human time and can't be held under a lock —
that is exactly what 50008 is for, and `SeriesOrderList.razor` already catches it, sets
`_changedElsewhere` and refreshes. The guard works; only the test's assumption was wrong.

Fixed with `Database/DatabaseCollection.cs` and `[Collection(DatabaseCollection.Name)]` on the nine
classes that take `TestDatabaseFixture`, so they run in sequence with each other while the tests that
touch no database stay parallel. Six consecutive green runs; 210 tests, ~2.3s.

### Series are created by the CSV import (2026-09-18)

Requiring the artist to create every series by hand before importing was too harsh, so **an unknown
series name in the CSV is now created rather than rejecting the file**. The typo defence moved into
the review:

- **Row counts per new series.** A typo has one row where the real series has twelve. This is the
  signal that actually catches it; a flat list of names does not get read.
- **A shared slug is an error.** `ArtistShopSlug.FromName` lowercases and strips everything that is
  not a letter or digit, so "Seascapes, 1990s" and "Seascapes 1990s" both become `seascapes-1990s`.
  `Unique_Series_Slug` would refuse the second, and `SeriesRepository.AddAsync` already treats a
  slug clash as "name already in use", so the import agrees with it. This doubles as the
  near-duplicate check and is not a heuristic: it is the rule the database enforces.
- **A small edit distance is a warning** and does not block the import. `Imports/SeriesNameSimilarity`
  holds both checks plus a two-row Levenshtein written by hand rather than taking a dependency — the
  distance is twenty lines and the part that needs tuning is the threshold, which is ours either way.
  Currently distance 1 or 2 with the longer name at least 8 characters ("Blue" and "Blur" are two
  words, not one misspelt).
- A name matching an existing series is not new at all. Two rows spelling one name with different
  capitals are one series, first spelling wins. A name with no letters or digits is an error.
- `ArtworkCatalogAddition` gained `NewSeriesNames`; `ArtworkRepository.AddAsync` now delegates to
  `AddManyAsync`, so one path handles both and a duplicated try/catch went away.
- `SeriesRepository.AddManyAsync(connection, transaction, names)` is static and runs on the caller's
  transaction, so the series and the artworks are saved together or not at all. Procedure
  `dbo.AddManySeries` over `dbo.SeriesNameAndSlugList` (script `0006`, applied automatically on the
  next start). **`SortOrder` is `UNIQUE` with no gap filling**, so a batch cannot have every row read
  the same `MAX` and ask for `MAX + 1`: it uses `MAX(SortOrder) + ROW_NUMBER()` under the same
  `UPDLOCK, HOLDLOCK` that `AddSeries` uses. A name taken between review and confirm comes back as
  `CatalogChangedException`, which the page already answers by re-reviewing.

**A concern I raised here that turned out to be unfounded:** a row listing the same series twice
(`Mines; Mines`) is fine. `ArtworkImportRowReader.List` already ends with
`.Distinct(ArtworkImportNames.Comparer)`, so duplicates never reach the planner. Same for
vocabulary terms.

### Image upload, as of 2026-09-13

Steps 0-7 are done and verified against the database. You can drop or pick multiple images on
`/admin/catalog/artworks/add`, watch each upload with a real progress bar, reorder by dragging,
star one as primary, and it all persists in order with the right primary.

Step 8 is done:
- **Orphan sweep:** `OrphanedImageSweepService` (a `BackgroundService` on a `PeriodicTimer`) runs
  `OrphanedImageSweeper` at startup and then every interval. It deletes unreferenced originals
  older than the grace period. The settings are `OrphanedImageSweep:GracePeriod` and
  `Interval`: 7 days / 1 day, and 5 minutes / 1 minute in Development.
- **Submit check:** the add-artwork submit rejects images whose original the sweep already deleted.
- **Storage split:** `ImageStorage` owns the file layout (paths, save original, delete variants
  then original). `ImageUploadStore` is the upload itself, shared by the endpoint and the tests,
  and is what bulk import should call.
- **Tests:** `tests/ArtistShop.Web.Tests` (xUnit v3) runs against a separate `ArtistShopTests`
  database with `FakeTimeProvider`. Run `source env.sh && dotnet test` from the repo root with SQL
  Server up. `global.json` switches `dotnet test` to Microsoft Testing Platform mode, which
  xUnit 4 requires on the .NET 10 SDK.

**Layout.** Uploads live at `<repo>/content/images/{originals,variants}` — deliberately outside
the project directory, because `dotnet watch` treats files under the project as project changes
and refreshes the browser, which kills the SignalR circuit mid-upload. Components are under
`src/ArtistShop.Web/Components/Forms/FileUpload/`: `FileDropZone.razor` (+ its `.razor.js`) is
file-type agnostic and is what the future CSV upload should compose; `Images/` holds
`ImagesField`, `ImageUploadRow` and `SelectedImage`.

**If something looks flaky, check these before theorising.** Both bugs this session were
invisible by default:
- `appsettings.json` pins `Microsoft.AspNetCore` to `Warning`, which hides SignalR and circuit
  diagnostics. Raise `Microsoft.AspNetCore.SignalR`, `.Http.Connections` and `.Components.Server`
  to Debug in Development first.
- A `dotnet watch ⌚ Files added:` line naming your own runtime output means the watcher is
  fighting you.
- Exceeding SignalR's 32KB `MaximumReceiveMessageSize` closes the circuit with **no exception**,
  so an empty server log does not mean nothing went wrong.

## Decided: shop items don't share storage keys

`Unique_ArtworkImages_StorageKey` makes a second row for the same file a loud error. It came up
because paintings 6 and 7 once shared keys by accident (the form didn't reset after a save, fixed
with `@key="AddedSlug"`). If postcards later need to reuse a painting's photo, dropping the
constraint and adding reference counting is a deliberate schema change.

## 0. Spike the boundary — DONE

- [x] Minimal `InteractiveServer` island inside the EditForm, rendering one hardcoded hidden input
- [x] Submit; confirm it model-binds into `PaintingCatalogAdditionForm`
- [x] Island state survives a failed-validation re-render, so the image list can live in the circuit

## 1. Storage and serving — DONE

- [x] `ImageStorage:RootPath` in config, resolved against `ContentRootPath`
- [x] Split into `originals/` (never served) and `variants/` (served at `/media`)
- [x] `UseStaticFiles` + `PhysicalFileProvider` on `variants/` only
- [x] Gitignore `src/ArtistShop.Web/content/`

Decided: files never move after upload. Key is a v7 GUID, orphans get swept later.

## 2. Upload endpoint — DONE

- [x] Minimal API `POST /admin/uploads`, admin-only, one file per request
- [x] Antiforgery — ended up as a **form field**, not a header; the server checks that first
- [x] Size and content-type filter (cheap; the real check is whether libvips can decode it)
- [x] Returns `{ storageKey, width, height, blurDataUri }`

## 3. Image processing — DONE

- [x] NetVips + `NetVips.Native.linux-x64` (MIT, AVIF in the box, streams so memory stays low)
- [x] Original master saved untouched, no extension
- [x] Variants at 400 / 800 / 1600, AVIF + WebP, never upscaled
- [x] Blur data URI — 20px WebP, base64, inlined
- [x] Width/height read after `.Autorot()` so EXIF orientation can't transpose them
- [x] Failed processing deletes both the original and any partial variant directory

Widths are placeholders until the gallery and detail pages exist. Changing them later
means reprocessing `originals/`.

## 4. JS interop — DONE

- [x] Drop zone: `dragover` + `drop`, `preventDefault` or the browser navigates away
- [x] Click-to-pick via hidden `<input type="file" multiple>`
- [x] `File` objects stay in a JS `Map`; only metadata crosses the circuit
- [x] XHR per file, `request.upload` progress (fetch still has no upload progress)
- [x] C# starts each upload after its row exists, so progress can't arrive first
- [x] Listeners and the `DotNetObjectReference` cleaned up on dispose
- [x] `jsconfig.json` + JSDoc for type checking without a Node toolchain

## 5. The island component — DONE

- [x] Row per file: name, progress bar, status, real thumbnail once processed
- [x] Distinct "Processing…" state between 100% and the server finishing
- [x] Per-file error message from the server's own text
- [x] Retry a failed upload (the `File` is still held in the JS Map for this)
- [x] Remove a file
- [x] Temporary upload scaffold deleted
- [x] Split into `FileUpload/` (shared) and `FileUpload/Images/` (image-specific)
- [x] `FileDropZone<TResult>` extracted — drop zone, XHR driver, progress plumbing,
      generic only at its public API so the JSInvokable stays non-generic
- [x] `.catch` on every JS→.NET call, after two silent-rejection debugging sessions

Dropped: `IBrowserFile.RequestImageFileAsync` preview. Unnecessary — the served
variant arrives fast enough that a client-side preview earns nothing.

## 6. Reorder and primary — DONE

- [x] BlazorBlueprint.Primitives `BbSortable` (SortableJS) — touch works, unlike raw HTML5 DnD
- [x] `BbProgress` for real `role="progressbar"` semantics
- [x] Native radio group for primary — grouping is by `name`, so nesting in sortable rows is fine;
      `Filter="input, button"` stops the controls initiating a drag
- [x] ↑/↓ buttons as the keyboard path (SortableJS has no keyboard reorder)
- Primitives only: 247 bytes of CSS, so no second Tailwind build. Components' 129KB sheet not taken.

## 7. Wire into the form — VERIFIED WORKING

- [x] Indexed hidden inputs `Input.Images[n].*`; the index carries the order
- [x] Original filename captured and persisted (the bulk-CSV join key)
- [x] `ToCatalogAddition()` builds real `ShopItemImage` records
- [x] `NonEmptyAttribute` — property-level so it doesn't hit the `IValidatableObject`
      short-circuit; `[MinLength]` alone was useless because every DataAnnotations
      validator except `[Required]` treats null as valid
- [x] `ValidationMessage For="() => Input.Images"` — no field component, so no home otherwise
- [x] Confirmed in the database: SortOrder 0,1,2 with the star on the right row

### Resolved from this step

- [x] Form reset after save — `@key="AddedSlug"` on `ImagesField`. Blazor rebuilds the component
      when the key changes, which also disposes the uploader and frees the JS `File` map.
      `forceLoad` was the heavier alternative and wasn't needed.
- [x] Frozen upload bars / circuit reconnects — two causes, both fixed: the content directory sat
      inside the project so `dotnet watch` refreshed the browser mid-upload, and the blur data URI
      carried the source EXIF, blowing SignalR's 32KB receive limit.
- [x] Metadata stripped: variants keep only ICC, the blur keeps nothing and is converted to sRGB.
      Fixes a real privacy leak — public variants were carrying camera GPS.
- [x] Error bodies only rendered when `text/plain` and short, so an exception page can't paint
      itself into the UI
- [x] Test catalog and orphaned files cleared

## 8. Cleanup — DONE

- [x] Orphan sweep with a grace period, run by a background service; 4 tests
- [x] Deletion stays in the sweep, not on the ✕ button (✕ only unlinks)
- [x] Shared storage keys decided (forbidden, see above)
- [x] Expired uploads caught at submit
- [x] `MSSQL_PID=Express`. `SERVERPROPERTY('Edition')` confirmed it applied to the existing volume
- [x] Portrait variants: libvips `thumbnail` fits a `width`×`width` square unless given a height,
      so `800.webp` of a portrait photo came out 800 *tall*. Fixed with `height: source.Height`.
      Worth a test: upload `Image.Black(1000, 2000, bands: 3)` and assert `800.webp` is 800 wide
- [x] `BlurDataUri` size guard dropped. The same square rule bounds the blur at 20×20, and its
      metadata is stripped, so it stays far below the `nvarchar(1000)` column
- [x] Abort in-flight XHRs when the island is disposed, and when a row is removed mid-upload
- [x] Leftover variant folders cleared. Their originals were deleted before the delete order was
      fixed, and the sweep lists only `originals/`, so it couldn't see them
- [x] `Xmp` dropped from variants: XMP can carry its own copy of the GPS fields. Variants keep
      `Icc` only. To verify on a real photo, `grep -c GPSLatitude` on a variant should print 0

## 9. Bulk import — in progress (CSV import started 2026-09-16)

**Bulk image matching — decided 2026-09-13, revised 2026-09-17.** The CSV creates the artworks
(and their series). The artist then uploads a folder of images for **one artwork type**, and each
image's file name (without its extension) is compared to `Artworks.Name` **within that type**. A
sculpture and a painting may share a title without being ambiguous.

**Matching rules:**
1. **One image per artwork.** More images go through the edit artwork page (step 10).
2. **Names compare case-insensitively**, not as slugs. A title with a character file names can't
   hold (`/`) never matches and is reported as unmatched.
3. **Artworks that already have any image are skipped and reported.** Re-uploading the same folder
   changes nothing.
4. **Always primary**, since only artworks with no image get one.
5. **Several artworks of the type share the name:** nothing is attached; reported as ambiguous.
6. **The same name appears more than once in the upload** (two series folders, or `Sunset.jpg` and
   `sunset.png`): none of them is attached; reported with a pointer to that artwork's edit page.
7. **Unmatched files are reported.**
8. **The report is built in the page** as files finish. Lost if the tab closes.
9. **Two uploads racing for one artwork:** `AttachPrimaryImageToImagelessArtworkByName` takes `UPDLOCK` on the
   matched artworks, so the second waits and then reports "already has images". No 2601 from it.
   That holds only while this procedure is the one path adding a primary image. When the edit page
   (step 10) can add one, it takes the same lock **and** the attach repository catches 2601 and
   reports the file as skipped (Mike, 2026-09-17: do both — the lock keeps it from happening, the
   catch keeps it from being a 500 if some later path forgets the lock).

**Folders (2026-09-17).** The artist drops (or picks) one top-level folder. Files directly inside it
and inside its immediate subfolders (their series folders) count; anything deeper (thumbnails) is
ignored. Series are **not** read from folder names, because the CSV already assigned them.

**Flow, per step:**
1. **Drop.** The drop handler calls `webkitGetAsEntry()` on every `dataTransfer.items` entry before it
   returns (the list is emptied afterwards). Pickers: one for files, one with `webkitdirectory` for a
   folder (a directory picker can't pick single files); those `File`s carry `webkitRelativePath`.
2. **Walk.** Call `readEntries()` until it returns an empty batch (Chrome stops at 100 per call);
   `entry.file()` gives the `File`, `entry.fullPath` the path. Asynchronous, so the page doesn't
   freeze. `File`s stay in the JS `Map`.
3. **Metadata to the server** (path, size) as an `IJSStreamReference`: .NET calls a JS function
   returning `DotNet.createJSStreamReference(bytes of the JSON)` and reads it with
   `OpenReadStreamAsync(maxAllowedSize: …)`. The default is 512 KB (about 3,000 files), so set it.
   Not a single interop call: SignalR caps browser-to-server messages at 32 KB (about 200 files).
4. **The island (server) filters and pre-checks.** Depth rule, duplicate names (by
   `DatabaseCollationComparer`, over names from `ArtworkName.FromFileName`), then one database
   call for the type and all names, returning each name's outcome. **Only files that will attach
   get uploaded**; everything else goes straight into the report. A huge folder of mostly old work
   uploads almost nothing.
5. **Driver** uploads 3–4 at a time and starts the next when a response arrives. Courtesy only;
   the server doesn't trust it.
6. **Kestrel** limits (30 MB body, minimum data rate) apply as they already do.
7. **Per-user rate limit:** `AddRateLimiter` with a token bucket per user
   (`RateLimitPartition.GetTokenBucketLimiter`, keyed by the `NameIdentifier` claim; e.g. burst 100,
   10 per second), `UseRateLimiter()` after `UseAuthorization()`, `RequireRateLimiting` on the
   endpoints. Rejects with 429.
8. **The form is read.** One file per request. ASP.NET Core keeps files over 64 KB in a temp file
   (`ASPNETCORE_TEMP` or the system temp folder) and deletes it when the request ends.
9. **Validation** (name length, empty, size, content type), shared with `/admin/uploads`.
10. **Header only.** NetVips reads width, height and bands without decoding. Over a megapixel cap:
    400. Otherwise estimate MB = width × height × bands × a safety factor. libvips picks the decoder
    from the bytes, so a lying header can't make the decode bigger than the estimate.
11. **Processing limiter** (`Images/ImageProcessingLimiter.cs`), called from `ImageUploadStore.SaveAsync`
    so both endpoints get it, around only the NetVips work (not the whole request, so slow
    connections don't hold anything). Two `System.Threading.RateLimiting.ConcurrencyLimiter`s: a
    processor slot per image (the only queue with a limit, counted in images, `OldestFirst`), then
    megabytes of estimated memory. An image holds its slot while waiting for memory, so at most
    `ProcessorSlots` images wait there. Queue full: `ImageProcessingBusyException` → 503 with
    `Retry-After`. A closed tab cancels its wait and hands back its slot. **Global, not per tenant**:
    the point is keeping the machine up.
12. **Process.** `NetVips.Concurrency = 1` (one thread per image; the slots set how many run).
    Check whether AVIF encoding obeys it.
13. **Lease released** by `using`, even when processing throws.
14. **The bulk endpoint attaches** (artwork type id posted as a form field) through the attach
    procedure. If nothing was attached, it **deletes the stored original and variants right away**
    instead of leaving them to the sweep. It returns 200 with an outcome value for skips (not 400).
15. **Browser:** 200 feeds the report through the island. 429/503/504: exponential backoff with
    jitter, honouring `Retry-After`, capped, then reported as failed. 400: show the message.
16. **Report** fills in as files finish: attached, already had an image, ambiguous, duplicate
    names, unmatched, failed.

**Progress.** One bar for the whole run, not a row per file. A file counts in full once its response
arrives; files in flight count by bytes sent. Once every byte is sent and responses are pending,
show a "Processing…" spinner. `BbProgress` is fine, but JS **throttles** updates to the island
(e.g. 4 per second, one overall number). Today `FileDropZone.razor.js` sends every progress event.

**Host sizing, computed once at startup** (`ImageProcessingCapacity.FromHost`, printed in the startup
log): slots = cores − 1 (at least 1), leaving a core for pages; memory budget =
`MemoryBudgetMegabytes`, set per deployment (Mike, 2026-09-17, replacing a fraction of
`GC.GetGCMemoryInfo().TotalAvailableMemoryBytes`); startup refuses a budget at or above that total;
queued images = slots × (proxy timeout ÷ time per
image). Estimate = width × height × bands × bytes per sample (from the header's band format) ×
`MemoryEstimateMultiplier`. The megapixel check runs first, and an image whose estimate exceeds the
whole budget is also rejected as too large. All numbers live in `appsettings.json` `ImageProcessing`
and are **placeholders until measured**: 100 megapixels (Mike, 2026-09-17), a 1,000 MB budget, 60 s proxy
timeout, retry after 10 s. **Measured 2026-09-17** (our `ImageProcessor`, production libvips settings,
peak memory above baseline ÷ decoded size, on a fast desktop):

| Image | Multiplier | Time |
|---|---|---|
| 48 MP JPEG / WebP / PNG / 8-bit TIFF | 0.77–0.89 | 5–7 s |
| 100 MP JPEG | 0.64–0.66 | 7 s |
| 48 MP JPEG with an EXIF rotation tag (every portrait phone photo) | **1.39–1.44** | 9 s |
| 48 MP 16-bit TIFF | 0.37–0.49 | 8 s |

So the multiplier is 2 (margin over 1.44) and the time per image 10 s (a VPS core is slower than
the desktop). Re-measure on the VPS with `tools/measure-image-memory/measure.cs` (its header shows how, with or
without the SDK installed); a later run of the rotated photo gave 1.58, still under 2.

**Open:**
- The megapixel cap, the safety factor, the proxy timeout and seconds per image: measure first.
- Whether `/admin/uploads` gets the per-user token bucket too (probably yes).
- Multi-tenancy: see "Multi-tenancy notes" at the end of this file.

- [ ] CSV of the artist's spreadsheet creates shop items with no images. Decided 2026-09-13:
      one CSV per item type; fixed header names the artist must use (no column mapping); a header
      must be a known field (`title`, `price`, …), `series`, or the name of an existing vocabulary
      that applies to the item type; an unknown term name rejects the file, while an unknown
      **series** name is created (revised 2026-09-18, see the top of this file); any bad cell
      rejects the whole file with row numbers. A title already in `ShopItems` is skipped and listed, not rejected, so a CSV can
      be re-imported to add only its new rows; same-named items are added by hand. Sample
      export is `paintings.csv` at the repo root.
      - One row per painting; several series or terms in one cell, split on a
        configurable list delimiter, `;` by default (series names already contain commas)
      - Dimensions stored in cm; the import is told the source unit and converts. Columns become
        `decimal(8, 4)` so a 2-decimal inch value converts exactly
      - `DatePainted` nullable, and stores how precise it is (a year alone is "exact date unknown").
        Only `yyyy`, `yyyy-MM` and `yyyy-MM-dd` accepted; a year of `0` means blank
      - Unknown header columns reject the file
      - `Price` nullable: no price means viewable but not purchasable
      - Blank rows skipped, every cell trimmed, file read as UTF-8
      - Photographs and sculptures are their own item types, so their rows leave `paintings.csv`
      - `catalogueNumber` is unused

      **Decided 2026-09-16, after profiling `paintings.csv`:**
      - **One row per artwork, never merged.** The export is an old shape: one row per series
        membership (39 titles appear twice, differing only in series). The artist puts all series in
        one `;`-separated cell. Two rows with the same title in one file are both skipped and listed,
        like a title already in the catalog.
      - **Sample files:** `paintings.csv` stays as it is, as the invalid sample (old shape,
        `catalogueNumber`, sculpture rows). Mike makes a valid copy, and sculptures go in their own
        spreadsheet.
      - **Products: the page asks for a product type and a "one of a kind" checkbox.** The switch is on
        edition size, not on the type, the same rule the products island uses:
        - ticked: edition size 1, and `sold` decides stock (0 sold, 1 not); `editionSize`/`stock`
          columns are errors
        - unticked: `editionSize` (blank = open) and `stock` required; `sold` is an error
        - no price and not sold: no product, so viewable but not buyable. A sold row may have no price
          (sold long ago, price unknown)
      - **No auto-created terms or series.** Typos would make near-duplicate terms, and guessing which
        columns are vocabularies would be guessing. Unknown names are errors with row numbers; name
        comparison already ignores capitals.
      - **Check, then confirm.** The upload shows a review (rows to add, skipped rows, errors and
        warnings) and changes nothing. The review page carries the CSV text in a hidden field;
        confirming posts it back and the server checks it again before importing, so an edited field
        is harmless and nothing is stored or swept. If the result differs from the review, show the
        new review instead. Needs a file size limit. The whole import is one transaction.
      - **Deferred: what happened to an artwork** (lost, not for sale, gifted, in a collection). That's
        an artwork status, not a product, and is additive later; artworks with no products are the ones
        to review then. Decide at that point whether "sold" moves there too, or the product's stock 0
        and the status would both say it.
      - **Parser: Sylvan.Data.Csv, chosen 2026-09-16 and hidden behind `Imports/CsvTable`** (the only
        file that references it; `CsvTableTests` pin the behaviour a replacement must keep: spreadsheet
        row numbers, quoted commas and line breaks, blank rows skipped, short rows padded, comma-only
        delimiter, `MalformedCsvException` with a row number). Headers are dynamic (vocabulary names), so
        class mapping doesn't help. Sylvan.Data.Csv: MIT, 1.4.4 April 2026, `DbDataReader` API. Rejected:
        CsvHelper, the most used, but no commit since June 2025; Sep (MIT, active), pre-1.0, span-based,
        and leaves quotes in values unless `Unescape = true`.

      **Build order (started 2026-09-16):**
      - [x] `Imports/CsvTable` over Sylvan
      - [x] Planner (2026-09-16, never touches the database): `ArtworkImportPlanner.Plan(csvText, settings,
            snapshot)` returns an `ArtworkImportPlan` (additions with row numbers, skipped rows, errors,
            `Fingerprint()`). `ArtworkImportColumns` matches the header row, `ArtworkImportRowReader`
            reads cells, `ArtworkImportCatalogSnapshot.LoadAsync` reads the catalog (new
            `GetArtworkNames`). Header names are in `ArtworkImportHeaders`: title, description,
            dateCreated, height, width, depth, duration (h:mm:ss or m:ss), series, price, sold,
            editionSize, stock; anything else must be a vocabulary for the type. Header problems stop
            the plan before rows are read. Inches allow 2 decimal places, centimetres 4, prices 2;
            `sold` is TRUE/FALSE/blank. Skipped rows aren't checked. `RejectsTheOldPaintingsExport`
            reads `paintings.csv`
      - [x] `ArtworkRepository.AddManyAsync` (2026-09-16): one connection and transaction, `AddArtwork`
            per addition (its own BEGIN/COMMIT nest); a stale choice in a later row rolls back the
            earlier ones (tested). Shares `ExecuteAddAsync` with `AddAsync`
      - [x] Split the drop zone's look from its behaviour (2026-09-16, image upload checked in the browser):
            `FileDropZoneFrame` is the look, and its `<file-drop-zone>` custom element (loaded in `App.razor`,
            like `<partial-date-field>`) opens the picker, highlights on drag, and turns a drop into the
            input's `change` event. `FileDropZone` keeps only the uploading and listens to `change`.
            `FileDropField` is the static version: a named file input inside the frame (the form needs
            `enctype="multipart/form-data"`), with the chosen file's name shown under the button
      - [x] Import page under `/admin/catalog/artworks/import?type={id}` (BUILT 2026-09-16, working in the browser; `Pages/Admin/Catalog/ArtworkImport/`, dashboard link per type). Notes: both forms are multipart,
            so the 4 MB form value limit applies to the unencoded text rather than URL-encoded text; `CsvTable.Parse`
            turns every line break into `\n`, because a browser posts hidden fields back with `\r\n` and the
            fingerprint would never match; `Utf8Text` decodes strictly and the page says "save as CSV UTF-8";
            both forms post to `?type=` alone so a stale `imported=` doesn't show; the unit is only asked for
            when the type has height and width. The review opens in `Components/Dialogs/StaticModalDialog`, a
            native `<dialog>` opened by a `<static-modal-dialog>` custom element, not a BbDialog island: island
            parameters reach the server in one SignalR message (32KB limit) and the review carries the whole CSV.
            So both import forms post without Enhance. Later the same day every dialog moved onto one
            `Components/Dialogs/ModalDialog` (native `<dialog>`, `<modal-dialog>` custom element, look in
            `.artist-shop-modal`); Blueprint's dialogs and the three `BbPortalHost` islands are gone, and the
            reconnect modal uses the same class. Checked in the browser: rename and confirm dialogs, Escape
            blocked while saving (tested with a temporary delay), and no greying out between catalog pages
            without the portal hosts. `@oncancel:preventDefault` compiled to a literal attribute name and broke
            the circuit, so `ModalDialog` renders `data-keep-open` and its script cancels Escape. Original spec:: file, unit, product type, one of a
            kind, list separator; the review (counts, skipped rows, errors by row and column) carries the
            CSV text and the plan's fingerprint in hidden fields; confirm re-plans with a fresh snapshot
            and shows the new review if the fingerprint differs. File size limit. Dashboard link per type.
            Decode the upload with a `StreamReader`, which drops the byte order mark (`Trim()` doesn't, so
            the first header wouldn't match). Reject `"` and whitespace as the list separator
- [x] Schema edits (2026-09-13): nullable `Price`, `DatePainted` + `DatePaintedPrecision` with
      `PartialDate` owning date validation, `decimal(8, 4)` dimensions, and a Year/Month/Day
      `PartialDateField` whose script disables impossible days
- [x] **Catalog vocabulary admin (built 2026-09-14/15).** Mediums and supports became
      artist-defined vocabularies of terms (the Drupal/WordPress taxonomy pattern, "controlled
      vocabulary" in museum cataloguing).

      **Render model: static pages with prerendered islands.** The first build used
      `InteractiveServer` pages with prerendering off, and every link click flickered. The page
      went blank, then showed "Loading…", and the shell (inside the page) rebuilt its tabs. The rules now:
      - Pages are static and load all the data. Islands (`@rendermode="InteractiveServer"`,
        prerendered) never load their own data. A prerendered island that fetched its own data
        would render "not loaded" when the circuit attaches, which is the flicker again.
      - Island parameters travel as JSON. `IReadOnlySet` and dictionaries keyed by a record don't
        deserialize (checked), so domain records use lists. Callbacks can't cross into an island.
      - After writing, an island calls `NavigationManager.Refresh()`, so the static page
        re-renders and passes new parameters in, or `NavigateTo` when the page changes.
        `@key="Id"` gives a different entity a fresh island.
      - `CatalogLayout` is a static layout holding the tabs (`SectionNavLink`, which stays active
        on a vocabulary's edit and terms pages) and `<BbPortalHost @rendermode="InteractiveServer" />`.
        That host island also keeps the circuit open between catalog pages.
      - Rejected: interactive routing for all of `/admin`. `AddSinglePainting` would have to opt
        out, and moving between static and interactive routing is a full page load.
      - Fixed 2026-09-15: island controls used to look ready and do nothing until the circuit attached.
        `Components/Interop/DisabledUntilInteractive` wraps each island's controls in a
        `<fieldset class="contents" disabled>` while `RendererInfo.IsInteractive` is false (verified in
        the prerendered HTML). `ImagesField` wraps only its controls, never its hidden inputs: a
        disabled input posts nothing.
      - Known and accepted: field ids change once when an island attaches.

      **What exists.**
      - `/admin/catalog/vocabularies/new` and `/{id}/edit`: `VocabularyEditor` (static) plus the
        `VocabularyEditorForm` island. It holds the name, the shop item type checkboxes, a confirm
        dialog when unselecting a type that has terms on items, and delete. `VocabularyForm` owns
        its `EditContext`, the last-saved values (for `HasChanges` and `WasUnselected`), and the
        store for database errors. Creating a vocabulary opens its terms page, and deleting one
        opens `/new`.
      - `/admin/catalog/vocabularies/{id}`: `VocabularyTermList` (static). `AddVocabularyTermForm` is
        a static form post that redirects back to the page after adding, and the
        `VocabularyTermTable` island holds the rename `BbDialog` and the delete confirm.
      - `Components/Dialogs/ConfirmDialog` wraps `BbAlertDialog`, with plain buttons so it stays open
        and disabled while saving.
      - Procedures live in `Database/Procedures/Vocabularies/` and `VocabularyTerms/`. Duplicate names
        are matched on the constraint name by `SqlErrors.IsUniqueConstraintViolation` and become
        `NameAlreadyInUseException`, then a field message. No `ShopItemType` C# enum was needed.

      **Stale rows, done 2026-09-15.** Deleted-elsewhere errors all become `CatalogChangedException`
      now: 50002 (vocabulary, `UpdateVocabulary`) refreshes the editor page, which then says the
      vocabulary doesn't exist; 50003 (term, `RenameVocabularyTerm`) closes the dialog and refreshes
      the table. See the add-artwork form for 50001/50004.

      **Open.** Nothing links to `/admin/catalog` from the dashboard.

- [x] **Series admin — BUILT 2026-09-15.** Series stays its own entity rather than a vocabulary: it will grow
      a description, has a public page, and orders its artworks. Same render model as the
      vocabulary pages; add a Series tab to `CatalogLayout` (`SectionNavLink` with
      `ActivePath="/admin/catalog/series"`).

      **Decided 2026-09-15:**
      - **Any shop item type in any series, mixed freely** (a painting and a photograph can share
        one). No type checklist like vocabularies. Schema, edited in place in `0001`:
        `PaintingSeries` → `Series`, `PaintingAndSeriesJunction` → `ShopItemAndSeriesJunction
        (ShopItemId, SeriesId, SortOrder, IsCover)` referencing `ShopItems (Id)`. In C#,
        `PaintingSeries` → `Series`, and the series list moves from `Painting` up to `ShopItem`.
        `AddPainting` and `GetPaintingBySlug` follow the rename. (`ShopItem` itself may be renamed.)
      - **Created** by an add box on the list page, a static form post like `AddVocabularyTermForm`.
      - **Slugs:** series slugs are never numbered; a name whose slug another series has is
        rejected. Shop items keep `dbo.ResolveShopItemSlug`, which every `AddPainting`/`AddSculpture`
        calls, since all shop items share `dbo.ShopItems`. (A caller-passes-slugs `dbo.ResolveSlug`
        was built and removed the same day, once series stopped needing it.) When shop item editing
        arrives, the function takes the item's own id to leave out, or renaming "Sunset" to
        "Sunset!" yields `sunset-2`. One slug max length (`CatalogLimits.SlugMaximumLength`) and one
        `ArtistShopSlug.FromName` for shop items and series.
      - **Cover:** the artist stars one artwork on the series page, and its primary image is the
        cover, like `IsPrimary` on images. `IsCover` on the junction with a filtered unique index
        `WHERE IsCover = 1`, so the cover is always a member and leaves with it. The star is
        disabled on artworks with no images. Cover = the starred artwork's primary image, else the
        first artwork in order with an image, else a "no images yet" placeholder (a series can
        have no images at all).
      - **Custom order** is the same drag list as `ImagesField`. Series upload nothing (their images
        are the artworks' own), so the island only saves order and star, immediately: one set-based
        `UPDATE` for order (`UNIQUE (SeriesId, SortOrder)` forces that), one for the star.
      - **Extract a generic sortable list with a star** from `ImagesField`/`ImageUploadRow`. It owns
        `BbSortable`, move/nudge and ↑/↓, the radio star with explicit/fallback colours, and the
        "first item that `CanStar`" fallback. Each user supplies the row's middle and its extra
        actions as `RenderFragment<TItem>`s (images: progress, Retry, ✕; series: checkbox for bulk
        remove). Not an island itself, so callbacks and fragments work. Suggested: it reports
        moves rather than reordering `Items`, so the series island can save before showing the new
        order, then `Refresh()`; `ImagesField` applies moves immediately.
      - **Queries:** separate procedures for series with and without covers, not a flag parameter.

      **Build order:** 1) schema — DONE 2026-09-15 (`Series`, `ShopItemAndSeriesJunction` with
      `IsCover` + filtered unique index, series slug `nvarchar(200)`; `AddPainting` and
      `GetPaintingBySlug` updated); 2) slugs — DONE 2026-09-15 (`ArtistShopSlug.FromName` shared,
      `CatalogLimits.SlugMaximumLength`, test `NumbersTheSlugWhenItIsTaken`). Parallel tests
      exposed deadlocks in `AddPainting` under `SERIALIZABLE`: fixed by `ResolveShopItemSlug`
      reading once, over one prefix range, `WITH (UPDLOCK)`; 20 clean runs; 3) series procedures, repository, tests —
      DONE 2026-09-15 (`Procedures/Series/`, `SeriesRepository`, 13 tests in
      `SeriesRepositoryTests`; `0003` adds `dbo.OrderedIdList` for reorder; `PaintingSeries` →
      `Series` record, series list moved up to `ShopItem`). Errors 50004 (renamed series gone),
      50005 (reorder list no longer matches the series), 50006/50007 (cover not in series / has no
      image) reach the caller as raw `SqlException`s; the island decides what to show in step 5.
      The plain `GetAllSeries` (no covers) waits for the add-artwork series picker. **Changed
      2026-09-15:** series slugs are not numbered. A name whose slug another series has is
      rejected (`Unique_Series_Slug`), on add and rename, as `NameAlreadyInUseException`; the page
      must explain it in words the artist knows (see the wording under step 5); 4) extract the sortable list with
      a star — BUILT 2026-09-15 and working in the browser (`Components/Lists/StarredSortableList`;
      a line above the list appears only while a star is chosen by hand and offers going back to the
      automatic star, one control for the whole list rather than click-again; series side is
      `ClearSeriesCover` + `SeriesRepository.ClearCoverAsync`;
      `ImagesField` uses it, `ImageUploadRow` is now only the thumbnail, name, status and progress;
      star identity is the storage key, and Retry now sits after ↑/↓ beside ✕); 5) list page with add form, then the series page island — BUILT 2026-09-15, builds, not yet
      checked in the browser. Folder `Pages/Admin/Catalog/SeriesAdmin` (a folder named `Series` would
      make a namespace hiding the `Series` record). Series have their own `SeriesLayout`, not a tab in
      the vocabulary `CatalogLayout` (Mike: keep them separate). `SeriesList` (static, add form),
      `SeriesEditor` (static) with two islands: `SeriesActions` (rename dialog, delete confirm) and
      `SeriesShopItemList` (order, star, clear star, select and remove). The repository turns the
      "page is stale" errors 50004-50007 into `CatalogChangedException`; the list island shows
      "changed somewhere else" and refreshes. Nothing in the UI adds artworks to a series yet. Name-taken wording, following
      the artwork form's "web address": "Another series already has this name, or one that only
      differs in punctuation, accents or capital letters." A name with no letters or digits needs
      the artwork form's "no letters or numbers to build a web address from" check.

      **Pages:**
      - `/admin/catalog/series` lists series (with cover and artwork counts), linking to
        `/admin/catalog/series/{id}`: rename (the slug follows, breaking old links, accepted),
        reorder and star its artworks, tick artworks and remove them after one confirm, and
        delete the series after a confirm. Each row shows the artwork's primary image. Adding
        existing artworks there: not now.
      - Reorder, star, tick and remove belong in one island over the artwork list.
      - Already done: `SortOrder` on the junction with `UNIQUE (SeriesId, SortOrder)`. `AddPainting`
        appends with `MAX + 1` under `UPDLOCK`.
      - Same rules as vocabularies: names trimmed, empty rejected, delete procedures remove junction
        rows then the row in one transaction, dialog counts labelled "(count as of page load)".
      - Public series ordering, later: the customer picks the sort, the artist sets the default and
        can drag a custom order.
      - **Later, noted 2026-09-15, not designed yet:**
        - **Artist's order of the series themselves — BUILT 2026-09-15**, not yet checked in the
          browser. `Series.SortOrder` (`UNIQUE`; new series go last), `dbo.ReorderSeries` (50008 when
          the set changed), `SeriesOrderList` island on `/admin/catalog/series`, and the list component
          split into `SortableList` with `StarredSortableList` built on it (Mike chose the split over
          an optional star, keeping every parameter required). Visitors' own sort orders: much later.
        - **"Chronological" is undefined**, deferred until visitors can sort. A series has no date of its own. It could come from its
          artworks (earliest, latest, median), but dates are `Paintings.DatePainted` only, so other
          types would need their own date or a shared one on `ShopItems`. Decide before sculptures
          and photographs get their tables.
        - **Browse series by artwork type**: Artworks → Paintings → the series containing paintings,
          exclusively, mostly, or at least one. No schema change: count the junction rows per
          `ShopItems.ShopItemTypeId`. The open questions are what "mostly" means (a share? the most
          common type?) and whether the artist can override it. Deferred.
- [ ] Term and series pickers on the add-artwork form — BUILT 2026-09-15. A `CheckboxGroupField` per
      vocabulary that applies to paintings, and one for series (in the artist's order); existing terms
      and series only. The groups are the `TermAndSeriesPickers` island, which reloads its options when
      the tab becomes visible again, so a term or series created in another tab appears without losing
      the form. The `CatalogSetupNotice` island at the top of the form lists what isn't set up yet
      (no vocabulary, a vocabulary applying to no artwork type, a vocabulary without terms, no series),
      with links opening in a new tab, and refreshes the same way (Mike: tell them before they fill
      the form in). Both islands use `Components/Interop/TabReturnWatcher`, which renders nothing and
      raises `OnTabReturn` on `visibilitychange`. Checked boxes post repeated
      `Input.VocabularyTermIds` / `Input.SeriesIds` values, and form binding reads those into
      `List<int>` (verified with a real POST); with nothing checked it sets the list to null, so the
      form's lists turn null into empty. `ShopItemTypeId.Painting` mirrors the
      SQL `1`. **Stale picks, done 2026-09-15:** `AddPainting` checks series ids too (50009, so a
      deleted series isn't a foreign key error), the repository turns 50001/50009 into
      `CatalogChangedException`, and the page keeps the form and its uploads while saying the choices
      were updated. The pickers island adopts the options it's passed on every render (the page reads
      them fresh each request), so a failed submit drops what no longer exists; ticks stay island state,
      seeded once.
- [ ] Someday: export the catalog to CSV plus images in folders by series, for moving the shop
      elsewhere. The CSV import only creates items, so the site becomes the source of truth once
      the artist edits there; a CSV can't update existing paintings or add them to a series
- [ ] **Edit painting — PAUSED 2026-09-16 for step 10, where it becomes "Edit artwork".** Decisions
      from 2026-09-15 below; step 10 revises the slug rule and the form class.
      - **The slug follows the name**, like a series. Mike: leaving the old name in the web address is
        weird, and editing usually happens a few times early in a painting's life and then never. Warn
        in the form that old links will stop working, in case any were shared. `ResolveShopItemSlug`
        needs the artwork's own id left out, or renaming "Sunset" to "Sunset!" numbers it `sunset-2`
        (the function reads `dbo.ShopItems` itself, so pass an id to exclude, `NULL` when adding).
      - **Split in two.** First: name, price, date, description, dimensions, terms and series, reusing
        the add form's fields and the `TermAndSeriesPickers` island, plus an `UpdatePainting` procedure
        (replace the term junction rows; for series, delete the rows that went and append new ones with
        `MAX + 1` so existing order survives). Second, separately: editing images, which means teaching
        `ImagesField` about images the artwork already has (show, reorder, restar, remove, add more) —
        today it only knows files being uploaded right now. "Remove" keeps unlinking only; the sweep
        deletes files later.
      - Form reuse: `PaintingCatalogAdditionForm` becomes the shared form class, seeded from an existing
        painting for edit. Watch the sibling `@key` rule and that a `[SupplyParameterFromForm]` model
        needs exactly one public constructor.
- [x] Split the drop zone's look from its behaviour so the static CSV form reuses it (see the CSV build order)
- [ ] Bulk image matching, build order (design above, 2026-09-17):
      1. DONE 2026-09-17: `NetVips.NetVips.BlockUntrusted = true;` in `Program.cs`. Not yet checked:
         upload a JPEG, PNG, WebP, AVIF and TIFF to confirm they still work
      2. DONE 2026-09-17: `dbo.AttachPrimaryImageToImagelessArtworkByName` (`Procedures/Artworks/AttachPrimaryImageToImagelessByName.sql`),
         `ArtworkImageRepository.AttachPrimaryToImagelessArtworkByNameAsync` returning the match (type +
         every matching artwork id), 50010 → `CatalogChangedException`. `0001` gained
         `Index_Artworks_TypeAndName` so the `UPDLOCK` lookup doesn't lock the whole table; the dev
         database must be dropped. 7 tests in `ArtworkImageRepositoryTests`, 148 pass.
         Collation pinned the same day: `DatabaseInitializer.Collation` (`Latin1_General_100_CI_AS_SC`) on
         `CREATE DATABASE`, and `VerifyCollationAsync` at startup (shop database only) and in the test fixture
      3. DONE 2026-09-17: `dbo.GetArtworkNameMatches` (`Procedures/Artworks/GetNameMatches.sql`) over a new
         `dbo.ArtworkNameList` (`0005`, primary key on Name, so case-only duplicates are an error: the
         page reports those before calling). `ArtworkImageRepository.GetArtworkNameMatchesAsync` returns a
         dictionary keyed by the names as sent. Both procedures now return the shared
         `ArtworkNameMatchType` in an `ArtworkNameMatch` (type + artwork ids); from the attach procedure,
         `OneImagelessArtwork` means attached. 151 tests pass
      4. DONE 2026-09-17: `Images/ImageUploadValidation` (validation moved out of the endpoint),
         `ImageProcessor.ReadHeader`, `ImageTooLargeException`, `ImageProcessingLimiter` + settings +
         capacity, and `ImageUploadStore` deleting the original on **any** failure (including busy
         and cancelled). `/admin/uploads` returns 503 + `Retry-After` when busy. 11 new tests
         (`ImageProcessingLimiterTests`, `ImageUploadStoreTests`), 162 pass. Not checked in a browser.
         Follow-ups the same day: `Utilities/Units` (`BytesPerMebibyte`, `PixelsPerMegapixel`; the CSV
         limit is now 1 MiB), `Utilities/ValidatedSettings.Read<T>` (bind a section by property name,
         `[Range]` attributes catch missing values, misspelled keys are errors) for both
         `ImageProcessingSettings` and `OrphanedImageSweepSettings` (now its own `OrphanedImageSweep`
         section), and a per-endpoint `RequestSizeLimitAttribute` on `/admin/uploads` (file limit + 1 MiB;
         not exercised by a test). 167 pass.
         **HEIC is not supported (Mike, 2026-09-17):** NetVips.Native's libheif has no HEVC decoder
         (patent-encumbered), and switching to the system libvips wasn't worth it. `ReadHeader` rejects
         `heif-compression = hevc` with `UnsupportedImageFormatException` (AVIF reports `av1`), and
         validation rejects `image/heic`/`image/heif` with the same "export as JPEG" message. Test fixture
         `tests/.../Images/Fixtures/sample.heic` (made with ImageMagick). 168 pass. The image picker's `accept` is now
         `ImageUploadValidation.FileInputAccept`, joined from the same set the server checks (not tried on an iPhone)
      5. DONE 2026-09-17: bulk endpoint (upload, process, attach, delete on skip, outcome in a 200)
         and the per-user token bucket, both described in "Done 2026-09-17" above. The JavaScript in
         step 6 posts `file` and a form field named exactly `artworkTypeId`, and the antiforgery
         token the way `FileDropZone.razor.js` already does
      6. DONE 2026-09-18: JS folder walk, both pickers, metadata stream, 4-at-a-time driver,
         backoff, throttled progress — see "What was built on 2026-09-18" at the top of this file
      7. DONE 2026-09-18: the page at `/admin/catalog/artworks/images`, island, depth rule,
         duplicate names through `DatabaseCollationComparer`, pre-check, bar + spinner, report.
         Still to do: the CSV import page's success state should link to it
- [ ] **Browse artworks — next.** Nothing lists them yet, so the only way to see what the CSV import
      and the image upload produced is the database
- [ ] Edit artwork page (step 10), the only way to add a second image

## 10. Artwork restructure — in progress (CSV import in step 9 was taken first, 2026-09-16)

**Why.** A painting is barely different from a sculpture or a photograph: the only real difference
is which vocabularies apply. The `Paintings` subtype table and the `ShopItem` → `Painting`
inheritance don't pay for themselves, and the artist needs types we can't foresee (fibre arts, a
dance performance). So `ShopItem` becomes `Artwork`, and its type becomes a row the artist manages.
"Work type" is the cataloguing standards' name for this (CCO, VRA Core); the table is `ArtworkTypes`.

**Decided:**
- Work types are artist-defined, seeded with Painting, Photograph and Sculpture. No code refers to a
  particular type: `ShopItemTypeId.Painting` and the computed `Paintings.ShopItemTypeId AS 1` go away.
- An artwork's type can't change after creation. The artist deletes it and adds it again.
- A type that artworks still use can't be deleted.
- Public address is `/artworks/{slug}`. Browse pages will filter and sort by type.
- **Fields per type work like vocabularies per type.** We predefine the fields, trying to cover
  every kind of artwork up front; a client who needs another asks us. The artist switches fields on
  per type. A field switched off in another tab while a form is open is handled the same way as
  un-ticking a vocabulary's type.
- Pre-release: edit `0001`/`0002` and drop the database, no migration.

**Schema.**
- Renames: `ShopItems` → `Artworks`, `ShopItemTypes` → `ArtworkTypes`, `ShopItemImages` →
  `ArtworkImages`, `ShopItemImageList` → `ArtworkImageList`, and the junctions and constraint names
  to match (`ArtworkAndVocabularyTermsJunction`, `VocabularyAndArtworkTypesJunction`,
  `ArtworkAndSeriesJunction`). `ArtworkTypes.Id` becomes `IDENTITY`.
- `Paintings` is dropped. Its columns move onto `Artworks`, renamed for any type: `DateCreated` +
  `DateCreatedPrecision`, `Description`, `WidthCm`, `HeightCm`, and a new `DepthCm`.
- The composite foreign keys that check a term's vocabulary applies to the artwork's type stay as
  they are, with the columns renamed.
- `ArtworkFields` (seeded by us, mirrored by a C# enum with explicit values) and
  `ArtworkTypeAndArtworkFieldsJunction`. A `CHECK` can't read another table, so `AddArtwork` (and
  later `UpdateArtwork`) `THROW`s when a value is given for a field its type doesn't have; that
  error becomes `CatalogChangedException` like 50001-50009.
- Fields (decided 2026-09-16): 1 Date created, 2 Height and width, 3 Depth, 4 Duration. Description
  is always on. Height and width are one switch because neither is any use alone. Depth requires
  height and width: `ArtworkFields.RequiresArtworkFieldId` (a field with no requirement names
  itself), copied into the junction under a foreign key, plus a foreign key from the junction to
  its own (type, required field) row. The database refuses depth without height and width, and
  refuses removing height and width while depth is on (547). The column is `NOT NULL` on purpose:
  a foreign key with a NULL column isn't checked, which let a NULL copy skip the rule. The type
  admin should show Depth nested under Height and width. Measurements are ordered height x width x
  depth everywhere, as galleries list them.

**Decided 2026-09-16, selling and navigation:**
- **Product types, not kinds** (renamed 2026-09-16): `ProductTypes` matches `ArtworkTypes`, and "product
  type" is the usual shop term; the `Product`/`Artwork` prefix already tells the two apart.
- **`Price`/`Stock` leave `Artworks`.** `ProductTypes` and `Products` (see the section below) are
  built in this step, not with the cart, so the add form doesn't lose its price.
- **The add and edit forms have a products section with any number of rows**: an island, like
  `ImagesField`, posting through hidden inputs. A plus button adds a row; each row has a product
  type, label, price and stock. Most of the time the artist adds the original and its price
  together with the artwork, so products aren't a separate form; the edit page is where the rest
  get set up.
- **Sold originals.** An edition-size-1 product with stock 0 must stay 0 when the edit form is
  saved. In the products island, a row with edition size 1 shows stock as for sale / sold instead
  of a number; otherwise stock is a number no higher than the edition size.
- **Edition size replaces one-of-a-kind** (decided 2026-09-16, replacing an `IsOneOfAKind` flag on
  the product type and an earlier "built-in Original"). `Products.EditionSize` is how many were ever made:
  `NULL` = open, restock freely (postcards, open prints); `N` = limited, never more than N (casts,
  limited prints); `1` = one of a kind. Product types are plain names, seeded Original, Print and
  Postcard, and an unused one can be deleted. The public page shows "Sold" for edition size 1 at stock 0,
  "Sold out" for others at 0, and "Edition of 10, 3 left". The CSV import page asks which product type
  "sold" rows become; they get edition size 1 and stock 0.
- **Dropped: at most one original per artwork.** Nothing marks a product as "the original" now,
  and `WHERE EditionSize = 1` would forbid a painting plus a one-off monoprint.
- **Refilling stock is allowed** (decided 2026-09-16). The only rule is `Stock <= EditionSize`.
  The artist edits stock by hand for private and gallery sales, and could always add a new
  product with full stock anyway, so nothing checks stock against past sales.
- **A sold product may have no price** (decided 2026-09-16):
  `CHECK (Price IS NOT NULL OR Stock = 0)`. Putting it back up for sale then requires a price.
- **Ways to the edit page:** an "Edit" link on the public page for logged-in admins, and "Edit it"
  next to "View it" after adding. An admin list of all artworks with filters comes later.

**Open:**
- **A product with no artwork** (a postcard of the artist in the studio). Deferred. Mike's idea: a
  product links to either an artwork or a non-artwork record (say `Merchandise`: name, slug,
  description, its own images), never both. Two nullable foreign keys plus a `CHECK` that exactly
  one is set keeps real foreign keys. Adding it later is additive. Watch that a unique index treats
  NULLs as equal in SQL Server, so the one-original index must also filter `ArtworkId IS NOT NULL`.
- **The admin artworks list** is deferred. Before building it, decide which filters (type, series,
  term, name, has images, for sale?), sorting, and paging.

**Build order.**
- [x] Schema (2026-09-16): `0001` rewritten, `0002` renamed to `ArtworkImageList`, new `0004`
      `ProductList`. Checked on a scratch database. **Databases not dropped yet**; drop them once the
      C# compiles. Fields seeded: 1 date created, 2 dimensions (depth optional inside it),
      3 duration; description is always on. Painting, Photograph and Sculpture start with 1 and 2
- [x] Procedures (2026-09-16): `ShopItems/` → `Artworks/`, everything renamed, checked on a
      scratch database. `AddArtwork` takes `@ArtworkTypeId`, the new columns and `@Products`, and
      throws 50010 (type gone), 50011 (a field switched off), 50012 (product type gone).
      `GetArtworkBySlug` `EXEC`s `GetArtworkById`, which returns artwork (with type id and name),
      images, series, terms, products. `UpdateArtwork` and the work type admin procedures come
      with their own steps
- [x] C# (2026-09-16): `ShopItem*` → `Artwork*` everywhere; `Painting` removed, `Artwork` is concrete
      with its type and products; `ArtworkRepository` (`AddAsync`, `GetByIdAsync`, `GetBySlugAsync`)
      maps 50001/50009-50012 to `CatalogChangedException`; `ProductTypeRepository` + `GetProductTypes`;
      `ArtworkField` enum; `Product`/`ProductAddition` in `Domain/Commerce`. 77 tests pass.
      Interim state until the later steps:
      - `/admin/catalog/artworks/add?type={id}` (dashboard links one per type) renders only the
        type's fields (`GetArtworkTypeFields`) and clears posted values for fields switched off
        meanwhile. It has no duration input yet and sends no products, so there is no price entry
        until the products island. 80 tests pass
      - public page is `/artworks/{slug}`; the public nav's "Paintings" page is still the placeholder
- [x] Work type admin under `/admin/catalog/types` (2026-09-16): add, rename, delete when unused, and
      tick the type's fields. Vocabularies keep ticking their types on the vocabulary page
- [ ] Products island: rows with product type, label, price, edition size and stock; with edition size 1,
      stock is for sale (1) or sold (0).
      The page passes the product types as a record-wrapped list, per the island parameter rule.
      **The add form starts with one row of the default product type** (decided 2026-09-19), since
      adding an artwork by hand usually means adding its original too; the artist can remove the row.
      Nothing creates a product today except the CSV import, so an artwork added by hand can be
      viewed and not bought.
      **(From Claude, 2026-09-19)** When it lands, move `AddArtwork`'s product-type check (50012)
      into `dbo.CheckArtworkChoicesAreCurrent` and give `UpdateArtwork` a `@Products` parameter, so
      the two forms can't drift on which stale choices they refuse. It sits in `AddArtwork` today
      only because the CSV import is the one caller that sends products
- [ ] Add artwork: the artist picks the type first, then `/admin/catalog/artworks/add?type={id}`.
      The static page renders only that type's fields and vocabularies, so no island has to react
      to a type dropdown. `TermAndSeriesPickers` takes the type id instead of assuming paintings.
      The products island's rows are inserted into `Products` in the same transaction. After adding,
      the message links to both the public page and the edit page
- [ ] Public page `/artworks/{slug}` replaces `/paintings/{slug}`, with an "Edit" link for admins;
      dashboard nav updated
- [x] Edit artwork (2026-09-19: fields, terms, series, images and delete; products wait on the
      products island). Built as described below, with one addition: a duration input, because the
      CSV import can set a duration and a form that didn't carry it would erase it on save:
      - Route by id, `/admin/catalog/artworks/{id:int}/edit`, since the slug changes on rename
      - Slug rule: keep the current slug if it equals the new name's slug or that slug plus
        `-<number>`; otherwise call `ResolveArtworkSlug`. This keeps an unchanged save from moving
        `sunset-2` to `sunset-4`, and the function needs no id to exclude
      - `UpdateArtwork`: stale-choice checks as in `AddArtwork`, a new error for an artwork deleted
        while the form was open; replace all term rows; for series, delete the unchecked rows and
        append new ones at `MAX + 1`. Removing an artwork from a series whose cover it was removes
        the cover too, since `IsCover` lives on that junction row
      - Form class: a base form with the fields, terms and series; the add form inherits it and
        adds `Images`/`PrimaryImageKey` (the edit form has no images in phase one). Seed it with a
        static `FromArtwork(Artwork)`, since a `[SupplyParameterFromForm]` model needs exactly one
        public constructor. Keep the property named `Input`: the pickers hardcode
        `Input.VocabularyTermIds` and `Input.SeriesIds`
      - Editing images: done in the same session, see below
- [ ] CSV import (step 9) is per work type: the artist picks the type on the import page, and the
      known headers are that type's fields and vocabularies, plus series, price and sold
- [ ] Next: admin artworks list under `/admin/catalog/artworks`, with filters, linking to each edit page.
      Once it exists, the artwork type page's "Used by N artworks, so it can't be deleted" should link
      to it filtered by that type (`ArtworkTypeEditorForm.razor`)

### Products and orders — tables built in this step; cart and orders come later

Discussed 2026-09-16. A postcard or print isn't an artwork but is made from one.
- **`Products`**: `Id`, `ArtworkId` → `Artworks`, `ProductTypeId`, `Label` ("A4", "A3"), `Price`,
  `EditionSize`, `Stock`. Cart lines reference `ProductId`. An artwork with no products can be
  viewed but not bought. Rejected: a cart line pointing at "an artwork, print or postcard" by product type
  + id, because that id can't have a foreign key. Edition size rules are in "Decided" above.
- Tracking which number of an edition sold (3/10) comes later.
- **Order lines are snapshots**: artwork name, product type name, label, unit price and quantity are
  copied, plus a nullable `ProductId` with `ON DELETE SET NULL`. Renames, price changes and
  deletions then can't rewrite history. The order copies the shipping address and totals too.

## Multi-tenancy notes (undecided, 2026-09-17)

The app is single-tenant today; the goal is many artists' sites (say 250) on one small VPS.
- **Shape:** one process with a `TenantId` column, one process with a database per tenant, or a
  process per tenant (simplest code, most memory: a Blazor Server process idles at roughly
  100–200 MB). Not chosen.
- **Image processing limits:** `ImageProcessingLimiter` is per process. With one process it is the
  machine's cap; with several, each needs its share, or the cap has to be shared between them.
- **Leaning (Mike, 2026-09-17, not final):** one process, a database per shop. A forgotten
  `WHERE TenantId = …` then can't leak one artist's catalog into another's, which is the risk a
  shared-table design never fully loses. What it would cost: `SqlConnectionFactory` resolved per
  request instead of a singleton (and a `Max Pool Size` cap, since ADO.NET pools per connection
  string), DbUp run per tenant at startup or on first use, one small shared database holding the
  host-name-to-shop registry, and identity staying single with a tenant claim. Express's 50 GB cap
  is per database, which helps; its ~1.4 GB buffer pool is per instance, which doesn't.
- **Image storage must move behind an abstraction first.** `ImageStorage` writes straight to local
  folders and `OrphanedImageSweeper` deletes every file in `originals/` the database doesn't
  reference — so pointing the sweep at one shop's database while the folder holds every shop's
  files would delete the others' originals, silently and for good. The seam wanted is a storage
  interface (save, open, delete, list) whose local implementation prefixes a tenant, so an offsite
  image service can take its place later. Decide it before a second database exists.
- **Memory budget:** `ImageProcessing:MemoryBudgetMegabytes`, set per deployment, with the .NET heap
  pinned by `DOTNET_GCHeapHardLimitPercent` so the two fit inside the container. Plan: the app in its
  own container with a memory limit (`mem_limit` in compose), SQL Server in another, so SQL's memory
  is outside the app's. In dev the app runs on the host and shares the whole machine.
- **Future:** move image processing into its own worker container (as imgproxy, Thumbor and
  Mastodon's Sidekiq do), so a memory spike or a crash in libvips can't take the website down.
  Needs `MALLOC_ARENA_MAX=2` in that container too.

## Deployment (work in progress, 2026-09-17)

`Dockerfile`, `.dockerignore` and `docker-compose.production.yml` are the plan for shipping; the open
items are listed at the top of the compose file. Checked locally the same day: the image builds
(Tailwind is downloaded in the build), the stack starts, migrations run, libvips loads, and the home
page returns 200. The app used about 56 MB idle and SQL Server about 680 MB, each under a 2 GB limit.
- **Inside a container .NET reports 75% of the memory limit** as `TotalAvailableMemoryBytes` (its GC
  heap limit): 2 GB gave 1,536 MB, so the image budget came out at 384 MB. At the current settings
  that rejected a 48 MP phone photo (about 412 MB estimated). Fixed the same day by replacing the
  fraction with an explicit `MemoryBudgetMegabytes`.
- **No CPU limit yet**, so the container saw all 20 host cores (19 slots). Add `cpus:` in compose;
  `Environment.ProcessorCount` follows it.
- `Failed to determine the https port for redirect` is expected until the reverse proxy exists.
- Trying it leaves the `artist-shop-production_*` volumes behind; `down -v` removes them.

## Image URLs (2026-09-17)

`Images/ImageVariants` owns the widths, formats, file names and "largest variant that exists up to
the wanted width"; `Images/ImageUrls.Variant(key, imageWidth, wantedWidth, format)` is the only place
that knows variants are served from `/media`, so a CDN or image service changes one file. The four
hand-built URLs (upload row, public artwork page, both series lists) use it, which also fixed the
public page asking for `800.webp` on images narrower than 800. Not yet used: `srcset`/`sizes` and a
`<picture>` with AVIF and WebP sources, for the gallery pages.

