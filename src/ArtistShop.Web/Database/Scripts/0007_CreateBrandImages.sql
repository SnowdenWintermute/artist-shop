-- The website's logos and browser tab icon. Each original stays on disk under its storage key, like
-- a work image's; the files made from it are small and served on every page, so they're kept here
CREATE TABLE brand_images (
    -- BrandImageType's id: a website has at most one of each
    type int NOT NULL,
    CONSTRAINT primary_key_brand_images PRIMARY KEY (type),
    -- always the 32 hexadecimal characters of a version 7 GUID
    storage_key char(32) NOT NULL,
    CONSTRAINT unique_brand_images_storage_key UNIQUE (storage_key),
    original_file_name varchar(260) NOT NULL,
    -- the original's, in lower case hex
    sha256 char(64) NOT NULL
);

CREATE TABLE brand_image_files (
    image_type int NOT NULL,
    CONSTRAINT foreign_key_brand_image_files_brand_images FOREIGN KEY (image_type) REFERENCES brand_images (type) ON DELETE CASCADE,
    -- BrandImageFileType's id
    type int NOT NULL,
    CONSTRAINT primary_key_brand_image_files PRIMARY KEY (image_type, type),
    -- BrandImageFormat's id
    format int NOT NULL,
    width int NOT NULL,
    height int NOT NULL,
    -- the file's own, in its address, so new bytes get a new address and browsers may keep a file for good
    sha256 char(64) NOT NULL,
    bytes bytea NOT NULL
);

-- how a file is found when it's served
CREATE INDEX index_brand_image_files_sha256 ON brand_image_files (sha256);
