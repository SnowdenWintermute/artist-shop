DROP FUNCTION IF EXISTS update_colors;

-- replaces every choice at once, so a role left out goes back to being derived
CREATE FUNCTION update_colors (p_color_role_ids int[], p_colors text[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM colors;

INSERT INTO
    colors (color_role_id, color)
SELECT
    chosen.color_role_id,
    chosen.color
FROM
    unnest(p_color_role_ids, p_colors) AS chosen (color_role_id, color);
$$;
