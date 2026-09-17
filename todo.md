# Todo: interactive image upload on the add-painting form

Goal: drop or pick multiple images on `/admin/paintings/add-single`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the painting.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

## Where this stands — 2026-09-16, end of session

**Next session starts at step 9's CSV import build order**: the drop zone split, then the import page.
Everything below is uncommitted on top of `b89c3c8`; 132 tests pass; nothing from this session has
been checked in a browser.

**Before running `dev.sh`:** drop the dev database. `0001` changed (`ProductKinds` became
`ProductTypes`), and DbUp won't rerun a script it already recorded:
`source .env && docker exec artist-shop-mssql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "DROP DATABASE IF EXISTS ArtistShop;"`
Running a schema script through `sqlcmd` by hand needs `-I`, or the filtered indexes fail.

Done 2026-09-16:
- **Work type admin** (step 10): `/admin/catalog/types/new` and `/{id}/edit` in
  `Catalog/ArtworkTypes/`, like the vocabulary editor. Field checkboxes come from `ArtworkFields`
  (`ArtworkFieldRepository`), Depth nested under Height and width. Switching a field off clears its
  values on the type's artworks after a confirm dialog. Delete only when unused (50014); a type
  deleted before a save is 50013. `DeleteArtworkType` locks the type row first, the same order as
  `AddArtwork`. Dashboard link "Artwork types".
- **Vocabularies skip a deleted type** instead of failing the foreign key (a join in
  `AddVocabulary`/`UpdateVocabulary`).
- **Review cleanups:** `Components/Forms/ServerValidatedForm` is the base of the four form classes
  that show database errors; `CatalogLayout` renamed `VocabularyLayout`; `AddArtwork.razor` loads its
  type with `ArtworkTypeRepository.GetAsync` (the `GetArtworkTypeFields` procedure is gone).
- **`ProductKind` renamed `ProductType`** everywhere (tables, procedures, C#).
- **CSV import started** (step 9): the decisions of 2026-09-16, `Imports/CsvTable` over Sylvan,
  the planner, and `ArtworkRepository.AddManyAsync` (one transaction; tested rollback).
- Not done, noted: "Used by N artworks" on the type page should link to the future admin artworks
  list (step 10's last item).

Done 2026-09-15, all in step 9:
- **Series admin**, steps 1-5 of its build order: series hold any shop item type, the artist orders both
  the series and the artworks inside one, the cover is a starred artwork's primary image, and the
  add-painting form has term and series pickers with a setup notice that refreshes on tab return.
- **`SortableList` + `StarredSortableList`** in `Components/Lists`, shared with the image list, dragged
  by a grip handle.
- **Deadlock fix:** concurrent `AddPainting` calls deadlocked under `SERIALIZABLE`; `ResolveShopItemSlug`
  now reads once over one prefix range `WITH (UPDLOCK)`.
- **Every "deleted somewhere else" error** (50001-50009) becomes `CatalogChangedException` and shows a
  message instead of Blazor's error bar.
- **Islands prerender disabled** via `DisabledUntilInteractive`.
- Not yet checked in a browser: a series page's artwork list (reorder, star, remove) and the stale-pick
  message on the add-painting form.

Done 2026-09-14/15:
- Vocabulary and term procedures, `VocabularyRepository`, `VocabularyTermRepository`,
  `ShopItemTypeRepository`, `SqlErrors`, and repository tests using a shared `CatalogTestData`.
- `ShopItemTypeId` is `int` everywhere, not `tinyint`.
- The test database is dropped and recreated on every run (an xUnit assembly fixture).
- Admin pages at `/admin/catalog/vocabularies/...`: static pages, a static `CatalogLayout`,
  and prerendered interactive islands for the dialogs.
- Field ids get a per-instance suffix, so two fields bound to `Name` can share a page.

Done before that, 2026-09-13/14:
- **Schema:** nullable `Price`; `DatePainted` plus `DatePaintedPrecision`; `decimal(8, 4)`
  dimensions; `SortOrder` on the series junction.
- **Vocabularies replace `Mediums`/`Supports`:** `ShopItemTypes`, `Vocabularies`,
  `VocabularyAndShopItemTypesJunction`, `VocabularyTerms` and `ShopItemAndVocabularyTermsJunction`,
  enforced with composite foreign keys. `Paintings.ShopItemTypeId` is a persisted computed `1`.
  `AddPainting` takes `@VocabularyTermIds`; `GetPaintingBySlug` returns painting, images, series,
  terms, in that order.
- **`PartialDate`** and the Year/Month/Day `PartialDateField` with its day-limiting script.

Nothing can edit a painting yet, and no public page shows a series.

### Image upload, as of 2026-09-13

Steps 0-7 are done and verified against the database. You can drop or pick multiple images on
`/admin/paintings/add-single`, watch each upload with a real progress bar, reorder by dragging,
star one as primary, and it all persists in order with the right primary.

Step 8 is done:
- **Orphan sweep:** `OrphanedImageSweepService` (a `BackgroundService` on a `PeriodicTimer`) runs
  `OrphanedImageSweeper` at startup and then every interval. It deletes unreferenced originals
  older than the grace period. The settings are `ImageStorage:OrphanGracePeriod` and
  `OrphanSweepInterval`: 7 days / 1 day, and 5 minutes / 1 minute in Development.
- **Submit check:** the add-painting submit rejects images whose original the sweep already deleted.
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

`Unique_ShopItemImages_RelativePath` makes a second row for the same file a loud error. It came up
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

**How matching works (decided 2026-09-13).** The CSV creates the shop items. The artist then
uploads a folder of images, and each image's file name (without its extension) is compared to
`ShopItems.Name`. This works for any shop item type, not only paintings:
- **Exactly one item has that name:** create a `ShopItemImage` linked to it.
- **Several items share the name** (two "Sunset" paintings): attach nothing, and list it for the
  artist to assign by hand.
- **At the end, a report:** how many matched, which items already had images (so the artist can
  go and look), and which were ambiguous.

**Decided (2026-09-13):**
1. **One image per item.** More images are added on that item type's edit page, which doesn't
   exist yet.
2. **Names compare case-insensitively**, not as slugs. A title with a character file names
   can't hold (`/`) can never match, so it ends up in the unmatched list.
3. **Items that already have any image are skipped and flagged.** Re-uploading the same folder
   therefore changes nothing.
4. **Primary.** Always primary, since bulk upload only attaches to items with no images.
5. **Images matching nothing are flagged** in the report. The sweep removes their files.
6. **Attach as each file arrives**, no preview. There will be hundreds of matches.
7. **The report is built in the page** from each file's response. Lost if the tab closes.

**Consequences to handle:**
- Two files can match the same item (`Sunset.jpg` and `sunset.png`) and upload concurrently,
  so both could pass an "item has no images" check. Since a bulk attach is always primary, the
  filtered unique index on `IsPrimary` rejects the second insert (error 2601). Report that as
  skipped rather than letting it fail.
- Order is CSV import first, then image matching.

- [ ] CSV of the artist's spreadsheet creates shop items with no images. Decided 2026-09-13:
      one CSV per item type; fixed header names the artist must use (no column mapping); a header
      must be a known field (`title`, `price`, …), `series`, or the name of an existing vocabulary
      that applies to the item type; unknown term or series names reject the file; any bad cell
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
      the table. See the add-painting form for 50001/50009.

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
      The plain `GetAllSeries` (no covers) waits for the add-painting series picker. **Changed
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
      the painting form's "web address": "Another series already has this name, or one that only
      differs in punctuation, accents or capital letters." A name with no letters or digits needs
      the painting form's "no letters or numbers to build a web address from" check.

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
- [ ] Term and series pickers on the add-painting form — BUILT 2026-09-15. A `CheckboxGroupField` per
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
        needs the painting's own id left out, or renaming "Sunset" to "Sunset!" numbers it `sunset-2`
        (the function reads `dbo.ShopItems` itself, so pass an id to exclude, `NULL` when adding).
      - **Split in two.** First: name, price, date, description, dimensions, terms and series, reusing
        the add form's fields and the `TermAndSeriesPickers` island, plus an `UpdatePainting` procedure
        (replace the term junction rows; for series, delete the rows that went and append new ones with
        `MAX + 1` so existing order survives). Second, separately: editing images, which means teaching
        `ImagesField` about images the painting already has (show, reorder, restar, remove, add more) —
        today it only knows files being uploaded right now. "Remove" keeps unlinking only; the sweep
        deletes files later.
      - Form reuse: `PaintingCatalogAdditionForm` becomes the shared form class, seeded from an existing
        painting for edit. Watch the sibling `@key` rule and that a `[SupplyParameterFromForm]` model
        needs exactly one public constructor.
- [x] Split the drop zone's look from its behaviour so the static CSV form reuses it (see the CSV build order)
- [ ] Bulk image upload matched to existing items by file name = item name, per the questions
      above
- [ ] Attach-images must be its own operation, not only reachable through `AddPainting`
- [ ] Edit page per shop item type, the only way to add a second image
- [ ] Directory upload: `webkitdirectory` on the picker, `dataTransfer.items` +
      `webkitGetAsEntry()` on the drop zone (currently `.files`, which flattens folders)
- [ ] Concurrency cap in the upload driver — hundreds of files means queueing 3-4 at a time

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
      The page passes the product types as a record-wrapped list, per the island parameter rule
- [ ] Add artwork: the artist picks the type first, then `/admin/catalog/artworks/add?type={id}`.
      The static page renders only that type's fields and vocabularies, so no island has to react
      to a type dropdown. `TermAndSeriesPickers` takes the type id instead of assuming paintings.
      The products island's rows are inserted into `Products` in the same transaction. After adding,
      the message links to both the public page and the edit page
- [ ] Public page `/artworks/{slug}` replaces `/paintings/{slug}`, with an "Edit" link for admins;
      dashboard nav updated
- [ ] Edit artwork (step 9's plan, revised):
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
      - Phase two, separately: editing images
- [ ] CSV import (step 9) is per work type: the artist picks the type on the import page, and the
      known headers are that type's fields and vocabularies, plus series, price and sold
- [ ] Later: admin artworks list under `/admin/catalog/artworks`, with filters, linking to each edit page.
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
