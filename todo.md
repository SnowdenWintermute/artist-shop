# Todo: interactive image upload on the add-painting form

Goal: drop or pick multiple images on `/admin/paintings/add-single`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the painting.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

## Where this stands — 2026-09-12

Steps 0-7 are done and verified against the database. You can drop or pick multiple images on
`/admin/paintings/add-single`, watch each upload with a real progress bar, reorder by dragging,
star one as primary, and it all persists in order with the right primary. The catalog and the
image directories were cleared at the end of the session, so the next run starts empty.

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

## Open question: two paintings sharing a storage key

It happened once (paintings 6 and 7) and it was a **bug**, not a designed behaviour: the form
didn't reset after a successful save, so the island still held the previous painting's images and
resubmitting posted the same storage keys against a new painting. `@key="AddedSlug"` fixed the
cause.

The question worth deciding deliberately is whether sharing should be *possible* at all.

**Argument for forbidding it** — a `UNIQUE` constraint on `dbo.ShopItemImages.RelativePath` would
have turned that silent duplicate into a loud error on the second submit. Same reasoning as the
filtered unique index that makes zero-or-many primaries unrepresentable. It also means every
deletion path can assume one row per file instead of reference counting.

**Argument against** — `Postcard extends ShopItem` is in the domain notes, and a postcard of a
painting plausibly wants to reuse that painting's photograph. A global unique forecloses that.

**Leaning:** add the constraint now. The only sharing observed so far was an accident, and if
postcards later need reuse, dropping the constraint and adding reference counting should be a
deliberate schema change rather than something discovered after the fact.

**Either way, until it is decided the orphan sweep must not assume one row per file** — it has to
check for *any* referencing row.

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

## 8. Cleanup

- [ ] Orphan sweep: delete files with no `ShopItemImages.RelativePath`, **older than a grace
      period** — "unreferenced" is also true of a file uploaded a minute ago with the form
      still open. Done by hand once: 49 of 56 keys were orphans, 54M -> 4.4M.
- [ ] Deletion stays in the sweep, not on the ✕ button — ✕ must mean "unlink" so the same
      component works on an edit screen where the file is still referenced
- [ ] Decide the shared-storage-key question above, then make the sweep match it
- [ ] Abort in-flight XHRs when the island is disposed
- [ ] `MSSQL_PID=Express` in docker-compose (Developer edition is not production-licensed)
- [ ] Consider a `VARCHAR` widening or a size guard on `BlurDataUri` — the column is
      `nvarchar(1000)` and nothing currently checks the blur fits before the insert

## 9. Later — bulk import (not started)

- [ ] CSV of the artist's spreadsheet creates shop items with no images
- [ ] Bulk image upload matched to existing items by original filename
- [ ] Attach-images must be its own operation, not only reachable through `AddPainting`
- [ ] Directory upload: `webkitdirectory` on the picker, `dataTransfer.items` +
      `webkitGetAsEntry()` on the drop zone (currently `.files`, which flattens folders)
- [ ] Concurrency cap in the upload driver — hundreds of files means queueing 3-4 at a time
