-- The match types must match the ArtworkNameMatchType enum in C#, and the same rules as
-- attach_primary_image_to_imageless_artwork_by_name: 1 one imageless artwork, 2 no artwork,
-- 3 several artworks, 4 an artwork with images.
-- Two functions, one per result set; the repository runs both in one command, this one first
DROP FUNCTION IF EXISTS get_artwork_name_match_types;

-- plpgsql for the RAISE; RETURN QUERY sends the rows of a query back as the function's result
CREATE FUNCTION get_artwork_name_match_types (p_artwork_type_id int, p_artwork_names text[]) RETURNS TABLE (name text, match_type smallint) LANGUAGE plpgsql STABLE AS $$
DECLARE
    one_imageless_artwork CONSTANT smallint := 1;
    no_artwork CONSTANT smallint := 2;
    several_artworks CONSTANT smallint := 3;
    artwork_with_images CONSTANT smallint := 4;
BEGIN
    IF NOT EXISTS (
        SELECT
        FROM
            artwork_types
        WHERE
            artwork_types.id = p_artwork_type_id
    ) THEN
        RAISE EXCEPTION 'The artwork type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- The caller folds names that differ only in case into one, so this is a bug in the caller:
    -- both spellings would match the same artworks and be reported twice. DISTINCT under the
    -- names' own collation counts "Sunset" and "sunset" once
    IF (
        SELECT
            COUNT(DISTINCT artwork_name COLLATE case_insensitive)
        FROM
            unnest(p_artwork_names) AS artwork_name
    ) <> cardinality(p_artwork_names) THEN
        RAISE EXCEPTION 'The artwork names must differ by more than case.';
    END IF;

    -- A CTE (common table expression) is a named query that the next statement can use like a
    -- table. LEFT JOIN keeps a name that matches nothing, as one row with a NULL artwork_id. The
    -- name column's case_insensitive collation makes = ignore case, so "sunset" matches "Sunset"
    RETURN QUERY
    WITH
        name_matches AS (
            SELECT
                artwork_name.name,
                artwork.id AS artwork_id,
                EXISTS (
                    SELECT
                    FROM
                        artwork_images AS image
                    WHERE
                        image.artwork_id = artwork.id
                ) AS has_images
            FROM
                unnest(p_artwork_names) AS artwork_name (name)
                LEFT JOIN artworks AS artwork ON artwork.artwork_type_id = p_artwork_type_id
                AND artwork.name = artwork_name.name
        )
    SELECT
        name_match.name,
        -- COUNT of a column skips NULLs, so an unmatched name counts 0. bool_or is true if any is
        CASE
            WHEN COUNT(name_match.artwork_id) = 0 THEN no_artwork
            WHEN COUNT(name_match.artwork_id) > 1 THEN several_artworks
            WHEN bool_or(name_match.has_images) THEN artwork_with_images
            ELSE one_imageless_artwork
        END
    FROM
        name_matches AS name_match
    GROUP BY
        name_match.name;
END;
$$;

DROP FUNCTION IF EXISTS get_artwork_name_matches;

-- every match, so the report can link to them
CREATE FUNCTION get_artwork_name_matches (p_artwork_type_id int, p_artwork_names text[]) RETURNS TABLE (name text, artwork_id int) LANGUAGE sql STABLE AS $$
SELECT
    artwork_name.name,
    artwork.id
FROM
    unnest(p_artwork_names) AS artwork_name (name)
    JOIN artworks AS artwork ON artwork.artwork_type_id = p_artwork_type_id
    AND artwork.name = artwork_name.name;
$$;
