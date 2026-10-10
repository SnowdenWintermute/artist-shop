DROP FUNCTION IF EXISTS reorder_fonts;

-- p_fonts is every font, in the new order. Fonts come only from code, so there's no add or remove to
-- miss: the order replaces whatever was there
CREATE FUNCTION reorder_fonts (p_fonts text[]) RETURNS void LANGUAGE sql AS $$
DELETE FROM font_order;

-- WITH ORDINALITY numbers each element by its place in the array, from 1
INSERT INTO
    font_order (font, sort_order)
SELECT
    ordered.font,
    ordered.position - 1
FROM
    unnest(p_fonts) WITH ORDINALITY AS ordered (font, position);
$$;
