DROP FUNCTION IF EXISTS is_collections_pages_only_grid;

-- whether deleting this component, or changing what it shows, would leave the collections page without a
-- card grid of all collections. plpgsql, so it can be made before get_collections_page_grid_ids
CREATE FUNCTION is_collections_pages_only_grid (p_id int) RETURNS boolean LANGUAGE plpgsql STABLE AS $$
BEGIN
    RETURN ARRAY(SELECT id FROM get_collections_page_grid_ids()) = ARRAY[p_id];
END;
$$;
