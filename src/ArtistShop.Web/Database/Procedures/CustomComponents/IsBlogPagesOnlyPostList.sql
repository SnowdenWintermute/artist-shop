DROP FUNCTION IF EXISTS is_blog_pages_only_post_list;

-- whether deleting this component would leave the blog page without a post list. plpgsql, so it
-- can be made before get_blog_page_post_list_ids
CREATE FUNCTION is_blog_pages_only_post_list (p_id int) RETURNS boolean LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN ARRAY(SELECT id FROM get_blog_page_post_list_ids()) = ARRAY[p_id];
END;
$$;
