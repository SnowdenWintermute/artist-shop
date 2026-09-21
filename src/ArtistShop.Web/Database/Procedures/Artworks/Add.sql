DROP FUNCTION IF EXISTS add_artwork;

-- no transaction of its own: a function runs inside its caller's, and the repository adds a whole
-- CSV import in one
CREATE FUNCTION add_artwork (
    p_artwork_type_id int,
    p_name text,
    p_candidate_slug text,
    p_description text,
    p_date_created date,
    p_date_created_precision smallint,
    p_height_cm numeric,
    p_width_cm numeric,
    p_depth_cm numeric,
    p_duration_seconds int,
    p_images artwork_image_input[],
    p_vocabulary_term_ids int[],
    p_series_ids int[],
    p_products product_input[]
) RETURNS TABLE (id int, slug text) LANGUAGE plpgsql AS $$
DECLARE
    new_id int;
    new_slug text;
BEGIN
    PERFORM check_artwork_choices_are_current(
        p_artwork_type_id,
        p_date_created,
        p_height_cm,
        p_width_cm,
        p_depth_cm,
        p_duration_seconds,
        p_vocabulary_term_ids,
        p_series_ids
    );

    -- Not in the shared check, because only this function takes products: the CSV import is the
    -- one caller that sends any today. It moves there once the add and edit forms post them as well
    PERFORM
    FROM
        product_types
    WHERE
        product_types.id IN (
            SELECT
                product.product_type_id
            FROM
                unnest(p_products) AS product
        )
    FOR KEY SHARE;

    IF EXISTS (
        SELECT
        FROM
            unnest(p_products) AS product
        WHERE
            NOT EXISTS (
                SELECT
                FROM
                    product_types AS product_type
                WHERE
                    product_type.id = product.product_type_id
            )
    ) THEN
        RAISE EXCEPTION 'A chosen product type no longer exists.' USING ERRCODE = 'SH012';
    END IF;

    new_slug := resolve_artwork_slug(p_candidate_slug);

    -- the RETURNS TABLE columns id and slug are variables inside the function, so a bare id here
    -- would be ambiguous; naming the table settles it
    INSERT INTO
        artworks (
            artwork_type_id,
            name,
            slug,
            description,
            date_created,
            date_created_precision,
            height_cm,
            width_cm,
            depth_cm,
            duration_seconds
        )
    VALUES
        (
            p_artwork_type_id,
            p_name,
            new_slug,
            p_description,
            p_date_created,
            p_date_created_precision,
            p_height_cm,
            p_width_cm,
            p_depth_cm,
            p_duration_seconds
        )
    RETURNING
        artworks.id INTO new_id;

    PERFORM set_artwork_images(new_id, p_images);

    PERFORM set_artwork_vocabulary_terms(new_id, p_artwork_type_id, p_vocabulary_term_ids);

    PERFORM set_artwork_series(new_id, p_series_ids);

    INSERT INTO
        products (artwork_id, product_type_id, label, price, edition_size, stock)
    SELECT
        new_id,
        product.product_type_id,
        product.label,
        product.price,
        product.edition_size,
        product.stock
    FROM
        unnest(p_products) AS product;

    RETURN QUERY
    SELECT
        new_id,
        new_slug;
END;
$$;
