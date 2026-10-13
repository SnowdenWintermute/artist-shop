DROP FUNCTION IF EXISTS check_blog_page_has_post_list;

-- Called last by everything that can take a post list off a page, or move the blog page, so a post's
-- Up link always leads to its posts. FOR UPDATE queues the callers, so two tabs each taking out one
-- of two post lists can't both see the other's still there
CREATE FUNCTION check_blog_page_has_post_list () RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    PERFORM 1 FROM blog_page FOR UPDATE;

    IF NOT EXISTS (SELECT 1 FROM get_blog_page_post_list_ids()) THEN
        RAISE EXCEPTION 'The blog page needs a post list.' USING ERRCODE = 'SH027';
    END IF;
END;
$$;
