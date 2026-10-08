DROP FUNCTION IF EXISTS vocabulary_artwork_ids_with_several_terms;

-- the artworks that would lose this vocabulary's terms if it became mutually exclusive. Shared by the
-- confirm dialog's list and update_vocabulary's delete, so the dialog lists what the save removes
CREATE FUNCTION vocabulary_artwork_ids_with_several_terms (p_id int) RETURNS TABLE (artwork_id int) LANGUAGE sql STABLE AS $$
SELECT
    artwork_term.artwork_id
FROM
    artwork_and_vocabulary_terms_junction AS artwork_term
WHERE
    artwork_term.vocabulary_id = p_id
GROUP BY
    artwork_term.artwork_id
HAVING
    COUNT(*) > 1;
$$;
