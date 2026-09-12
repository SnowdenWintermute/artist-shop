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

## 5. The island component — MOSTLY DONE

- [x] Row per file: name, progress bar, status, real thumbnail once processed
- [x] Distinct "Processing…" state between 100% and the server finishing
- [x] Per-file error message from the server's own text
- [ ] Retry a failed upload (the `File` is still held in the JS Map for this)
- [ ] Remove a file
- [ ] Delete the temporary `<form action="/admin/uploads">` scaffold

Dropped: `IBrowserFile.RequestImageFileAsync` preview. Unnecessary — the served
variant arrives fast enough that a client-side preview earns nothing.

## 6. Reorder and primary

- [ ] Drag to reorder the thumbnails
- [ ] Star icon sets primary; exactly one, default to the first
- [ ] Keyboard alternative to dragging

## 7. Wire into the form

- [ ] Hidden inputs in DOM order: storage key, width, height, blur, plus the primary index
- [ ] Bind them onto `PaintingCatalogAdditionForm`
- [ ] `ToCatalogAddition()` builds real `ShopItemImage` records instead of `Images: []`
- [ ] Server-side validation: at least one image, primary index in range
- [ ] Confirm files are written before rows are inserted

## 8. Cleanup

- [ ] Orphan sweep for uploads whose form was never submitted
- [ ] Delete files when an image is removed from a saved painting
- [ ] Abort in-flight XHRs when the island is disposed
- [ ] `MSSQL_PID=Express` in docker-compose (Developer edition is not production-licensed)
