-- The image library: images uploaded into posts and text components, and later Image components. The
-- row is what keeps the file; the orphan sweep removes files with none
CREATE TABLE uploaded_images (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_uploaded_images PRIMARY KEY (id),
    -- always the 32 hexadecimal characters of a version 7 GUID
    storage_key char(32) NOT NULL,
    CONSTRAINT unique_uploaded_images_storage_key UNIQUE (storage_key),
    original_file_name varchar(260) NOT NULL,
    -- the original's, in lower case hex
    sha256 char(64) NOT NULL,
    width int NOT NULL,
    height int NOT NULL,
    blur_data_uri varchar(1000) NOT NULL,
    alt_text varchar(500) NOT NULL,
    -- as works' created_at: now() would give every upload in one transaction the same time
    uploaded_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- the sweep kept post and text component images by reading their bodies; the rows keep them now
DROP FUNCTION IF EXISTS get_all_post_image_storage_keys;

DROP FUNCTION IF EXISTS get_all_custom_component_image_storage_keys;
