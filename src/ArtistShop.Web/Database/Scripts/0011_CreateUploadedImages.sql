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

-- the library images a post shows, as post_and_works_junction is its works. Derived from the body
-- whenever it's saved, never written on its own. Deleting an image takes its rows with it
CREATE TABLE post_and_uploaded_images_junction (
    post_id int NOT NULL,
    uploaded_image_id int NOT NULL,
    CONSTRAINT primary_key_post_and_uploaded_images_junction PRIMARY KEY (post_id, uploaded_image_id),
    CONSTRAINT foreign_key_post_uploaded_images_posts FOREIGN KEY (post_id) REFERENCES posts (id) ON DELETE CASCADE,
    CONSTRAINT foreign_key_post_uploaded_images_uploaded_images FOREIGN KEY (uploaded_image_id) REFERENCES uploaded_images (id) ON DELETE CASCADE
);

-- the Images page's "Used in", and the cascade from a deleted image
CREATE INDEX index_post_and_uploaded_images_junction_image ON post_and_uploaded_images_junction (uploaded_image_id);

-- the same for text components
CREATE TABLE custom_component_and_uploaded_images_junction (
    component_id int NOT NULL,
    uploaded_image_id int NOT NULL,
    CONSTRAINT primary_key_custom_component_and_uploaded_images_junction PRIMARY KEY (component_id, uploaded_image_id),
    CONSTRAINT foreign_key_custom_component_uploaded_images_custom_components FOREIGN KEY (component_id) REFERENCES custom_components (id) ON DELETE CASCADE,
    CONSTRAINT foreign_key_custom_component_uploaded_images_uploaded_images FOREIGN KEY (uploaded_image_id) REFERENCES uploaded_images (id) ON DELETE CASCADE
);

CREATE INDEX index_custom_component_and_uploaded_images_junction_image ON custom_component_and_uploaded_images_junction (uploaded_image_id);

-- a row background's library image; page_rows comes first, in 0009
ALTER TABLE page_rows
ADD CONSTRAINT foreign_key_page_rows_uploaded_images FOREIGN KEY (background_uploaded_image_key) REFERENCES uploaded_images (storage_key);

-- deleting an image clears the backgrounds showing it
CREATE INDEX index_page_rows_background_uploaded_image_key ON page_rows (background_uploaded_image_key);

-- the sweep kept post and text component images by reading their bodies; the rows keep them now
DROP FUNCTION IF EXISTS get_all_post_image_storage_keys;

DROP FUNCTION IF EXISTS get_all_custom_component_image_storage_keys;
