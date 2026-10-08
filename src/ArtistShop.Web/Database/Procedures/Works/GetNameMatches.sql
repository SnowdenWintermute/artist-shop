-- The match types must match the WorkNameMatchType enum in C#, and the same rules as
-- attach_primary_image_to_imageless_work_by_name: 1 one imageless work, 2 no work,
-- 3 several works, 4 a work with images.
-- Two functions, one per result set; the repository runs both in one command, this one first
DROP FUNCTION IF EXISTS get_work_name_match_types;

-- plpgsql for the RAISE; RETURN QUERY sends the rows of a query back as the function's result
CREATE FUNCTION get_work_name_match_types (p_work_type_id int, p_work_names text[]) RETURNS TABLE (name text, match_type smallint) LANGUAGE plpgsql STABLE AS $$
DECLARE
    one_imageless_work CONSTANT smallint := 1;
    no_work CONSTANT smallint := 2;
    several_works CONSTANT smallint := 3;
    work_with_images CONSTANT smallint := 4;
BEGIN
    IF NOT EXISTS (
        SELECT
        FROM
            work_types
        WHERE
            work_types.id = p_work_type_id
    ) THEN
        RAISE EXCEPTION 'The work type no longer exists.' USING ERRCODE = 'SH010';
    END IF;

    -- The caller folds names that differ only in case into one, so this is a bug in the caller:
    -- both spellings would match the same works and be reported twice. DISTINCT under the
    -- names' own collation counts "Sunset" and "sunset" once
    IF (
        SELECT
            COUNT(DISTINCT work_name COLLATE case_insensitive)
        FROM
            unnest(p_work_names) AS work_name
    ) <> cardinality(p_work_names) THEN
        RAISE EXCEPTION 'The work names must differ by more than case.';
    END IF;

    -- A CTE (common table expression) is a named query that the next statement can use like a
    -- table. LEFT JOIN keeps a name that matches nothing, as one row with a NULL work_id. The
    -- name column's case_insensitive collation makes = ignore case, so "sunset" matches "Sunset"
    RETURN QUERY
    WITH
        name_matches AS (
            SELECT
                work_name.name,
                work.id AS work_id,
                EXISTS (
                    SELECT
                    FROM
                        work_images AS image
                    WHERE
                        image.work_id = work.id
                ) AS has_images
            FROM
                unnest(p_work_names) AS work_name (name)
                LEFT JOIN works AS work ON work.work_type_id = p_work_type_id
                AND work.name = work_name.name
        )
    SELECT
        name_match.name,
        -- COUNT of a column skips NULLs, so an unmatched name counts 0. bool_or is true if any is
        CASE
            WHEN COUNT(name_match.work_id) = 0 THEN no_work
            WHEN COUNT(name_match.work_id) > 1 THEN several_works
            WHEN bool_or(name_match.has_images) THEN work_with_images
            ELSE one_imageless_work
        END
    FROM
        name_matches AS name_match
    GROUP BY
        name_match.name;
END;
$$;

DROP FUNCTION IF EXISTS get_work_name_matches;

-- every match, so the report can link to them
CREATE FUNCTION get_work_name_matches (p_work_type_id int, p_work_names text[]) RETURNS TABLE (name text, work_id int) LANGUAGE sql STABLE AS $$
SELECT
    work_name.name,
    work.id
FROM
    unnest(p_work_names) AS work_name (name)
    JOIN works AS work ON work.work_type_id = p_work_type_id
    AND work.name = work_name.name;
$$;
