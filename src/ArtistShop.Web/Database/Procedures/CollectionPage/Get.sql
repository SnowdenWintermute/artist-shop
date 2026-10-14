DROP FUNCTION IF EXISTS get_collection_page;

CREATE FUNCTION get_collection_page () RETURNS TABLE (layout int, cards_per_page int) LANGUAGE sql STABLE AS $$
SELECT
    collection_page.layout,
    collection_page.cards_per_page
FROM
    collection_page;
$$;
