# Todo: interactive image upload on the add-painting form

Goal: drop or pick multiple images on `/admin/paintings/add-single`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the painting.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

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

### Still open from this step

- [ ] `forceLoad: true` on the success redirect — enhanced navigation preserves the island,
      so the form keeps its images after a save and a second submit duplicates the painting
      (painting 7 duplicated painting 6 this way). Validation failures must stay enhanced.
- [ ] Delete duplicate painting 7
- [ ] Intermittent: upload stuck at 100% after a submit. Not reproducible; `.catch` handlers
      are now in place, so check the browser console next time it appears.

## 8. Cleanup

- [ ] Orphan sweep: delete files with no `ShopItemImages.RelativePath`, **older than a grace
      period** — "unreferenced" is also true of a file uploaded a minute ago with the form
      still open. Done by hand once: 49 of 56 keys were orphans, 54M -> 4.4M.
- [ ] Deletion stays in the sweep, not on the ✕ button — ✕ must mean "unlink" so the same
      component works on an edit screen where the file is still referenced
- [ ] Note: two paintings can share a storage key (see painting 6/7), so the sweep must check
      for *any* referencing row, not assume one-to-one
- [ ] Abort in-flight XHRs when the island is disposed
- [ ] `MSSQL_PID=Express` in docker-compose (Developer edition is not production-licensed)

## 9. Later — bulk import (not started)

- [ ] CSV of the artist's spreadsheet creates shop items with no images
- [ ] Bulk image upload matched to existing items by original filename
- [ ] Attach-images must be its own operation, not only reachable through `AddPainting`
- [ ] Directory upload: `webkitdirectory` on the picker, `dataTransfer.items` +
      `webkitGetAsEntry()` on the drop zone (currently `.files`, which flattens folders)
- [ ] Concurrency cap in the upload driver — hundreds of files means queueing 3-4 at a time
