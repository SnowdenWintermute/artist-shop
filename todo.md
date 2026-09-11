# Todo: interactive image upload on the add-painting form

Goal: drop or pick multiple images on `/admin/paintings/add-single`, watch each one upload,
reorder them by dragging, star one as primary, and have them saved with the painting.

Approach: the page stays static SSR. One `InteractiveServer` island owns the image list.
Bytes go to a separate HTTP endpoint via XHR (not over the circuit). The island and the
form talk through hidden inputs inside the existing `<EditForm>`.

## 0. Spike the boundary

- [ ] Minimal `InteractiveServer` island inside the EditForm, rendering one hardcoded hidden input
- [ ] Submit; confirm it model-binds into `PaintingCatalogAdditionForm`
- [ ] Confirm the island survives a failed-validation re-render

Everything below assumes this works. Check it before building anything else.

## 1. Storage and serving

- [ ] Pick a content directory outside `wwwroot` (uploads must not land in the publish output or git)
- [ ] `UseStaticFiles` with a `PhysicalFileProvider` on that directory, under its own request path
- [ ] Decide staging vs. final layout — the painting row does not exist yet at upload time
- [ ] Gitignore the content directory

## 2. Upload endpoint

- [ ] Minimal API `POST /admin/uploads`, admin-only, one file per request
- [ ] Send the antiforgery token as a `RequestVerificationToken` header or it 400s
- [ ] Validate content type and size; reject non-images
- [ ] Return `{ storageKey, width, height, blurDataUri }`

## 3. Image processing

- [ ] Choose the library (see licensing notes — Magick.NET or libvips, probably not ImageSharp)
- [ ] Save the original master
- [ ] Generate the `-400` / `-800` size variants
- [ ] Generate the blur data URI (tiny WebP, base64, inlined)
- [ ] Read real width and height off the decoded image, not the client

## 4. JS interop module

- [ ] Drop zone: `dragover` / `drop`, read `dataTransfer.files` (Blazor cannot reach the bytes)
- [ ] Click-to-pick: hidden `<input type="file" multiple>`
- [ ] XHR per file, `upload.onprogress` for the real percentage
- [ ] Report progress and completion back via `DotNetObjectReference`
- [ ] Clean up listeners and the object reference on dispose

## 5. The island component

- [ ] Row per file: name, size, progress bar, thumbnail
- [ ] Preview via `IBrowserFile.RequestImageFileAsync` so 4MB never crosses the circuit
- [ ] Distinct "processing" state after the bar reaches 100% — encoding takes seconds
- [ ] Per-file error state and retry
- [ ] Remove a file

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
