DROP FUNCTION IF EXISTS make_custom_page_blog;

-- the admin only offers this on a page holding a post list
CREATE FUNCTION make_custom_page_blog (p_id int) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    -- FOR SHARE holds off a delete of the page until this commits
    PERFORM 1 FROM custom_pages WHERE id = p_id FOR SHARE;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The page no longer exists.' USING ERRCODE = 'SH020';
    END IF;

    UPDATE blog_page
    SET
        page_id = p_id;
END;
$$;
