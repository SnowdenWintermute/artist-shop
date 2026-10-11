-- replace_brand_image's files, one row per BrandImageFileType
CREATE TYPE site_types.brand_image_file_input AS (
    type int,
    format int,
    width int,
    height int,
    sha256 char(64),
    bytes bytea
);
