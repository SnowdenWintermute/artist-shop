DROP FUNCTION IF EXISTS delete_post;

-- the junction rows go with it, by the cascade
CREATE FUNCTION delete_post (p_id int) RETURNS void LANGUAGE sql AS $$
DELETE FROM posts
WHERE
    id = p_id;
$$;
