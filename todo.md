# Todo: interactive image upload on the add-painting form

Goal: drop or pick multiple images on `/admin/paintings/add-single`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the painting.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

## Where this stands — 2026-09-14

**Next session starts at "Catalog vocabulary admin" in step 9**, build order step 1: the
vocabulary procedures and `VocabularyRepository`. Design, routes and decisions are all settled
there. Image upload (steps 0-8) is finished.

Done since the image work, all tested (28 tests pass):
- **Schema:** nullable `Price`; `DatePainted` plus `DatePaintedPrecision`; `decimal(8, 4)`
  dimensions; `SortOrder` on the series junction.
- **Vocabularies replace `Mediums`/`Supports`:** `ShopItemTypes`, `Vocabularies`,
  `VocabularyAndShopItemTypesJunction`, `VocabularyTerms` and `ShopItemAndVocabularyTermsJunction`,
  enforced with composite foreign keys. `Paintings.ShopItemTypeId` is a persisted computed `1`.
  `AddPainting` takes `@VocabularyTermIds`; `GetPaintingBySlug` returns painting, images, series,
  terms, in that order.
- **`PartialDate`** and the Year/Month/Day `PartialDateField` with its day-limiting script.

Nothing creates vocabularies, terms or series yet, so those insert paths haven't run against
real rows.

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

## 9. Next — bulk import (not started)

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
- [x] Schema edits (2026-09-13): nullable `Price`, `DatePainted` + `DatePaintedPrecision` with
      `PartialDate` owning date validation, `decimal(8, 4)` dimensions, and a Year/Month/Day
      `PartialDateField` whose script disables impossible days
- [ ] **Catalog vocabulary admin — NEXT.** Mediums and supports became artist-defined
      vocabularies of terms (the Drupal/WordPress taxonomy pattern, "controlled vocabulary" in
      museum cataloguing); the schema is done. Series stays its own entity: it will grow a
      cover photo and description, has a public page, and orders its paintings.

      **Pages** (all `InteractiveServer`; static rendering isn't a goal for admin). A shared
      catalog shell renders a tab bar of links, `[Medium] [Support] … [+ New vocabulary]
      [Series]`, plus `<BbPortalHost />`. The host must render interactively, and keeping it out
      of `MainLayout` avoids opening a circuit on public pages. Admin URLs use ids, and
      `{id:int}` keeps `new` from matching.
      - `/admin/catalog/vocabularies/new` and `/admin/catalog/vocabularies/{id}/edit` share one
        form: the name, plus checkboxes for the shop item types the vocabulary applies to. Saving
        with a type unticked confirms first ("removes Medium terms from 40 paintings"), then
        unlinks. Edit also has Delete vocabulary, confirming with term and item counts.
      - `/admin/catalog/vocabularies/{id}` lists terms with usage counts and an add-term box;
        pencil opens a rename `BbDialog`, trash opens a delete `BbAlertDialog`, which unlinks
        and then deletes.
      - `/admin/catalog/series` lists series with painting counts, linking to
        `/admin/catalog/series/{id}`: rename (the slug follows, breaking old links, accepted),
        drag to reorder its paintings, tick paintings and remove them after one confirm, and
        delete the series after a confirm. Adding existing paintings there: not now.

      **Rules.** Names are trimmed before saving and empty names are rejected, for vocabularies,
      terms and series. Delete and untick dialogs show counts from the page load, labelled
      "(count as of page load)". Dialogs are controlled (`@bind-Open`) so they stay open while
      the save runs. Deletes use `BbAlertDialog` (no dismiss by clicking outside). Every delete
      procedure removes junction rows first, then the row, in one transaction; the foreign keys
      keep refusing any other delete.

      **Procedures.** Vocabularies: `GetVocabularies`, `GetShopItemTypes`, `GetVocabulary`
      (name and ticked types), `CountVocabularyUsageByShopItemType`, `AddVocabulary`,
      `UpdateVocabulary` (rename, add ticks, unlink then remove unticks, in one transaction),
      `DeleteVocabulary`. Terms: `GetVocabularyTermsWithUsage`, `AddVocabularyTerm`,
      `RenameVocabularyTerm`, `DeleteVocabularyTerm`. Duplicate names surface as error 2627 or
      2601; turn them into a field message.

      **Build order.** 1) vocabulary procedures and `VocabularyRepository`; 2) catalog shell and
      the create/edit vocabulary page, then delete `Pages/Admin/DialogExperiment.razor`; 3) term
      procedures, `VocabularyTermRepository` and the terms page; 4) series. A `ShopItemType`
      C# enum mirroring `ShopItemTypes` arrives with the checkboxes, not before.

      Already done for series: `SortOrder` on the junction with `UNIQUE (SeriesId, SortOrder)`, so
      reordering must be one set-based `UPDATE`. `AddPainting` appends with `MAX + 1` under
      `UPDLOCK`. Public series ordering, later: the customer picks the sort, the artist sets the
      default and can drag a custom order.
- [ ] Someday: export the catalog to CSV plus images in folders by series, for moving the shop
      elsewhere. The CSV import only creates items, so the site becomes the source of truth once
      the artist edits there; a CSV can't update existing paintings or add them to a series
- [ ] Edit painting flow. None exists, so a painting can't be renamed yet; decide whether its
      slug follows the name like a series does
- [ ] Split the drop zone's look from its behaviour so the static CSV form reuses it
- [ ] Bulk image upload matched to existing items by file name = item name, per the questions
      above
- [ ] Attach-images must be its own operation, not only reachable through `AddPainting`
- [ ] Edit page per shop item type, the only way to add a second image
- [ ] Directory upload: `webkitdirectory` on the picker, `dataTransfer.items` +
      `webkitGetAsEntry()` on the drop zone (currently `.files`, which flattens folders)
- [ ] Concurrency cap in the upload driver — hundreds of files means queueing 3-4 at a time
