DROP FUNCTION IF EXISTS set_post_artworks;

-- Rebuilds the post's junction rows from its body, so they can't disagree with what the post shows
CREATE FUNCTION set_post_artworks (p_post_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM post_and_artworks_junction
WHERE
    post_id = p_post_id;

INSERT INTO
    post_and_artworks_junction (post_id, artwork_id)
SELECT DISTINCT
    p_post_id,
    artwork.id
FROM
    posts AS post
    -- A jsonpath: every op, then its insert, then an artwork embed's id. The default lax mode skips
    -- whatever lacks a step, like a text insert, which has no .artwork. Each match is a jsonb value,
    -- and a jsonb number casts to int
    CROSS JOIN LATERAL jsonb_path_query(post.body, '$.ops[*].insert.artwork.artworkId') AS embedded (artwork_id)
    -- an artwork deleted after the editor loaded leaves an embed that renders nothing, not an error
    JOIN artworks AS artwork ON artwork.id = embedded.artwork_id::int
WHERE
    post.id = p_post_id;
$$;
