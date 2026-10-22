DROP FUNCTION IF EXISTS delete_custom_page;

-- Its rows, slots and nav entry go with it; its components stay in the library. Deleting a page
-- that's gone already does nothing
CREATE FUNCTION delete_custom_page (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR SHARE holds off make_custom_page_home until this commits, so the check stays true
    PERFORM 1 FROM home_page WHERE page_id = p_id FOR SHARE;

    IF FOUND THEN
        RAISE EXCEPTION 'The page is the home page.' USING ERRCODE = 'SH021';
    END IF;

    PERFORM 1 FROM blog_page WHERE page_id = p_id FOR SHARE;

    IF FOUND THEN
        RAISE EXCEPTION 'The page is the blog page.' USING ERRCODE = 'SH022';
    END IF;

    PERFORM 1 FROM collections_page WHERE page_id = p_id FOR SHARE;

    IF FOUND THEN
        RAISE EXCEPTION 'The page is the collections page.' USING ERRCODE = 'SH031';
    END IF;

    DELETE FROM custom_pages
    WHERE
        id = p_id;
END;
$$;
