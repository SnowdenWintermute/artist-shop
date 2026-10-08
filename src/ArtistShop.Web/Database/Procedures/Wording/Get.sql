DROP FUNCTION IF EXISTS get_wording;

CREATE FUNCTION get_wording () RETURNS TABLE (
    collection_singular text,
    collection_plural text,
    collection_keeps_case boolean,
    work_singular text,
    work_plural text,
    work_keeps_case boolean
) LANGUAGE sql STABLE AS $$
SELECT
    wording.collection_singular,
    wording.collection_plural,
    wording.collection_keeps_case,
    wording.work_singular,
    wording.work_plural,
    wording.work_keeps_case
FROM
    wording;
$$;
