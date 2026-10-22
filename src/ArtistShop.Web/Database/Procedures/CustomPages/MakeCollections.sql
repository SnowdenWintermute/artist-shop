DROP FUNCTION IF EXISTS make_custom_page_collections;

-- the admin only offers this on a page holding a card grid of all collections; another is refused
CREATE FUNCTION make_custom_page_collections (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR SHARE holds off a delete of the page until this commits
    PERFORM 1 FROM custom_pages WHERE id = p_id FOR SHARE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The page no longer exists.' USING ERRCODE = 'SH020';
    END IF;

    UPDATE collections_page
    SET
        page_id = p_id;

    PERFORM check_collections_page_has_grid();
END;
$$;
