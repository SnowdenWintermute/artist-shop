DROP FUNCTION IF EXISTS resolve_artwork_slug;

-- the candidate if no artwork has it, otherwise the candidate with the next free number
CREATE FUNCTION resolve_artwork_slug (p_candidate_slug text) RETURNS text LANGUAGE plpgsql AS $$
DECLARE
    candidate_is_taken boolean;
    highest_number int;
BEGIN
    -- An advisory lock is a lock on a name we choose rather than on a row, held until the
    -- transaction ends. The slug about to be chosen is no row yet, so there is nothing else to
    -- lock: a second caller waits here instead of choosing the same slug and failing
    -- unique_artworks_slug. One name for every candidate, because families overlap: sunset can
    -- resolve to sunset-2, the very slug a work titled "Sunset 2" asks for
    PERFORM pg_advisory_xact_lock(hashtext('resolve_artwork_slug'));

    -- bool_or is true if any row's value is true
    SELECT
        COALESCE(bool_or(artwork.slug = p_candidate_slug), false),
        MAX(artwork_slug_number(artwork.slug, p_candidate_slug))
    INTO
        candidate_is_taken,
        highest_number
    FROM
        artworks AS artwork
    WHERE
        starts_with(artwork.slug, p_candidate_slug);

    IF NOT candidate_is_taken THEN
        RETURN p_candidate_slug;
    END IF;

    RETURN p_candidate_slug || '-' || (COALESCE(highest_number, 1) + 1);
END;
$$;
