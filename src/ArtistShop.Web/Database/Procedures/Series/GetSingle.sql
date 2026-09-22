DROP FUNCTION IF EXISTS get_series;

CREATE FUNCTION get_series (p_id int) RETURNS TABLE (id int, name text, slug text) LANGUAGE sql STABLE AS $$
SELECT
    series.id,
    series.name,
    series.slug
FROM
    series
WHERE
    series.id = p_id;
$$;

DROP FUNCTION IF EXISTS get_series_artworks;

CREATE FUNCTION get_series_artworks (p_id int) RETURNS TABLE (
    id int,
    name text,
    artwork_type_name text,
    is_cover boolean,
    storage_key text,
    original_file_name text,
    width int,
    height int,
    blur_data_uri text
) LANGUAGE sql STABLE AS $$
SELECT
    artwork.id,
    artwork.name,
    artwork_type.name,
    junction.is_cover,
    primary_image.storage_key,
    primary_image.original_file_name,
    primary_image.width,
    primary_image.height,
    primary_image.blur_data_uri
FROM
    artwork_and_series_junction AS junction
    JOIN artworks AS artwork ON artwork.id = junction.artwork_id
    JOIN artwork_types AS artwork_type ON artwork_type.id = artwork.artwork_type_id
    LEFT JOIN artwork_images AS primary_image ON primary_image.artwork_id = artwork.id
    AND primary_image.is_primary
WHERE
    junction.series_id = p_id
ORDER BY
    junction.sort_order;
$$;
