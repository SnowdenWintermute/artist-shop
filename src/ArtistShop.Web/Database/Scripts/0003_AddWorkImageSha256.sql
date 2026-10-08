-- The SHA-256 of the image's original, in lower case hex, so an import can tell an image is here
-- already without reading the file. Only the server sets it, when an upload is saved; an image
-- saved before this, or through the work form, has none and is hashed from its file when needed
ALTER TABLE work_images
ADD COLUMN sha256 char(64);
