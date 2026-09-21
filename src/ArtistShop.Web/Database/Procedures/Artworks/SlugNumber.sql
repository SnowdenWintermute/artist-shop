DROP FUNCTION IF EXISTS artwork_slug_number;

-- The number on the end of a slug in the candidate's family: 4 for sunset-4 under sunset, and NULL
-- for sunset itself, sunset-2b or anything else. In place of TRY_CAST, the regular expression
-- decides whether the rest is a whole number; nine digits at most, so it always fits an int.
-- IMMUTABLE: the same arguments always give the same answer
CREATE FUNCTION artwork_slug_number (p_slug text, p_candidate_slug text) RETURNS int LANGUAGE sql IMMUTABLE AS $$
SELECT
    CASE
        WHEN starts_with(p_slug, p_candidate_slug || '-')
        AND substring(p_slug FROM length(p_candidate_slug) + 2) ~ '^[0-9]{1,9}$' THEN substring(p_slug FROM length(p_candidate_slug) + 2)::int
    END;
$$;
