DROP FUNCTION IF EXISTS check_collections_page_has_grid;

-- Called last by everything that can take a card grid of all collections off a page, change what one
-- shows, or move the collections page, so a collection's Up link always leads to the collections. FOR
-- UPDATE queues the callers, as in check_blog_page_has_post_list
CREATE FUNCTION check_collections_page_has_grid () RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM collections_page FOR UPDATE;

    IF NOT EXISTS (SELECT 1 FROM get_collections_page_grid_ids()) THEN
        RAISE EXCEPTION 'The collections page needs a card grid of all collections.' USING ERRCODE = 'SH032';
    END IF;
END;
$$;
