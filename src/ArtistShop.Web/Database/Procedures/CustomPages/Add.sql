DROP FUNCTION IF EXISTS add_custom_page;

-- an empty page, in no nav. A name or address another page has fails its unique constraint
CREATE FUNCTION add_custom_page (p_name text, p_address text) RETURNS int LANGUAGE sql AS $$
INSERT INTO
    custom_pages (name, address)
VALUES
    (p_name, p_address)
RETURNING
    id;
$$;
