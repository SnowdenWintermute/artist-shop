DROP FUNCTION IF EXISTS vocabulary_work_ids_with_several_terms;

-- the works that would lose this vocabulary's terms if it became mutually exclusive. Shared by the
-- confirm dialog's list and update_vocabulary's delete, so the dialog lists what the save removes
CREATE FUNCTION vocabulary_work_ids_with_several_terms (p_id int) RETURNS TABLE (work_id int) LANGUAGE sql STABLE AS $$
SELECT
    work_term.work_id
FROM
    work_and_vocabulary_terms_junction AS work_term
WHERE
    work_term.vocabulary_id = p_id
GROUP BY
    work_term.work_id
HAVING
    COUNT(*) > 1;
$$;
