DROP FUNCTION IF EXISTS update_wording;

CREATE FUNCTION update_wording (
    p_collection_singular text,
    p_collection_plural text,
    p_collection_keeps_case boolean,
    p_work_singular text,
    p_work_plural text,
    p_work_keeps_case boolean
) RETURNS void LANGUAGE sql AS $$
UPDATE wording
SET
    collection_singular = p_collection_singular,
    collection_plural = p_collection_plural,
    collection_keeps_case = p_collection_keeps_case,
    work_singular = p_work_singular,
    work_plural = p_work_plural,
    work_keeps_case = p_work_keeps_case;
$$;
