DROP FUNCTION IF EXISTS find_artwork_ids_by_name;

CREATE FUNCTION find_artwork_ids_by_name (p_search text) RETURNS TABLE (id int) LANGUAGE sql STABLE AS $$
SELECT
    artwork.id
FROM
    artworks AS artwork
WHERE
    -- case_and_accent_insensitive ignores accents as well as case, so "cafe" finds "café". The
    -- column's own collation keeps accents, which is right for matching names but wrong for a
    -- search box. LIKE's wildcards in the search are escaped with a backslash, LIKE's default
    -- escape character, so a title holding a % is searched for literally. The backslash is
    -- escaped first, or it would double the ones the other two add
    artwork.name COLLATE case_and_accent_insensitive LIKE '%' || replace(
        replace(replace(p_search, '\', '\\'), '%', '\%'),
        '_',
        '\_'
    ) || '%';
$$;
