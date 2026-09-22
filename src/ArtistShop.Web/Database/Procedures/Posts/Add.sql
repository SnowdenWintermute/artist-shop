DROP FUNCTION IF EXISTS add_post;

-- plpgsql, because a plain sql function has its body checked when it's created, and
-- set_post_artworks is created after this file. plpgsql looks names up when it runs
CREATE FUNCTION add_post (p_title text, p_slug text, p_body jsonb, p_is_published boolean) RETURNS int LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
BEGIN
    INSERT INTO
        posts (title, slug, body, published_at)
    VALUES
        (
            p_title,
            p_slug,
            p_body,
            CASE
                WHEN p_is_published THEN clock_timestamp()
            END
        )
    RETURNING
        id INTO new_id;

    PERFORM set_post_artworks(new_id);

    RETURN new_id;
END;
$$;
