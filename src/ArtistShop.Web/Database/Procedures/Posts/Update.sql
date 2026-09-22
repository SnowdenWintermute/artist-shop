DROP FUNCTION IF EXISTS update_post;

CREATE FUNCTION update_post (p_id int, p_title text, p_slug text, p_body jsonb, p_is_published boolean) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    UPDATE posts
    SET
        title = p_title,
        slug = p_slug,
        body = p_body,
        -- saving a published post keeps its date. Unpublishing forgets it, so publishing again
        -- dates it afresh
        published_at = CASE
            WHEN p_is_published THEN COALESCE(published_at, clock_timestamp())
        END,
        updated_at = clock_timestamp()
    WHERE
        id = p_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The post no longer exists.' USING ERRCODE = 'SH015';
    END IF;

    PERFORM set_post_artworks(p_id);
END;
$$;
