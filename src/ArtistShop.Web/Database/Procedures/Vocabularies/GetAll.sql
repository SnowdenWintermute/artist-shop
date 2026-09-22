DROP FUNCTION IF EXISTS get_vocabularies;

CREATE FUNCTION get_vocabularies () RETURNS TABLE (id int, name text) LANGUAGE sql STABLE AS $$
SELECT
    vocabulary.id,
    vocabulary.name
FROM
    vocabularies AS vocabulary;
$$;
