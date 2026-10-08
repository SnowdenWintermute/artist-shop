DROP FUNCTION IF EXISTS get_work_neighbours;

CREATE FUNCTION get_work_neighbours (p_collection_id int, p_work_id int, p_only_works_with_images boolean) RETURNS TABLE (
    previous_collection_slug text,
    previous_work_slug text,
    previous_image_count int,
    next_collection_slug text,
    next_work_slug text,
    next_image_count int
) LANGUAGE sql STABLE AS $$
-- the places in a collection a visitor may be sent to, written once so both sides share the rule. A
-- work with no photograph is not somewhere a visitor can be sent. NOT MATERIALIZED puts this into
-- each lookup below as it stands, so each one reads its own collection by index instead of the whole
-- catalogue being listed first
WITH
    destinations AS NOT MATERIALIZED (
        SELECT
            junction.collection_id,
            junction.sort_order,
            work.id AS work_id,
            work.slug
        FROM
            work_and_collection_junction AS junction
            JOIN works AS work ON work.id = junction.work_id
        WHERE
            NOT p_only_works_with_images
            OR EXISTS (
                SELECT
                FROM
                    work_images AS image
                WHERE
                    image.work_id = work.id
            )
    )
    -- The work's own place in the collection is the anchor, so both sides come off one row and an
    -- work that isn't in the collection returns nothing at all. Each side looks within the collection
    -- first, and only past its end walks the collections the way the artist ordered them, stepping over
    -- any with nothing to see. LIMIT over UNION ALL stops at the first branch that finds a place, so
    -- the walk only runs at an end
SELECT
    previous_collection.slug,
    previous_work.slug,
    (
        SELECT
            count(*)::int
        FROM
            work_images AS image
        WHERE
            image.work_id = previous_work.work_id
    ),
    next_collection.slug,
    next_work.slug,
    (
        SELECT
            count(*)::int
        FROM
            work_images AS image
        WHERE
            image.work_id = next_work.work_id
    )
FROM
    work_and_collection_junction AS here
    JOIN collections AS here_collection ON here_collection.id = here.collection_id
    LEFT JOIN LATERAL (
        (
            SELECT
                destination.*
            FROM
                destinations AS destination
            WHERE
                destination.collection_id = here.collection_id
                AND destination.sort_order < here.sort_order
            ORDER BY
                destination.sort_order DESC
            LIMIT
                1
        )
        UNION ALL
        (
            SELECT
                last_place.*
            FROM
                collections AS earlier_collection
                CROSS JOIN LATERAL (
                    SELECT
                        destination.*
                    FROM
                        destinations AS destination
                    WHERE
                        destination.collection_id = earlier_collection.id
                    ORDER BY
                        destination.sort_order DESC
                    LIMIT
                        1
                ) AS last_place
            WHERE
                earlier_collection.sort_order < here_collection.sort_order
            ORDER BY
                earlier_collection.sort_order DESC
            LIMIT
                1
        )
        LIMIT
            1
    ) AS previous_work ON true
    LEFT JOIN collections AS previous_collection ON previous_collection.id = previous_work.collection_id
    LEFT JOIN LATERAL (
        (
            SELECT
                destination.*
            FROM
                destinations AS destination
            WHERE
                destination.collection_id = here.collection_id
                AND destination.sort_order > here.sort_order
            ORDER BY
                destination.sort_order
            LIMIT
                1
        )
        UNION ALL
        (
            SELECT
                first_place.*
            FROM
                collections AS later_collection
                CROSS JOIN LATERAL (
                    SELECT
                        destination.*
                    FROM
                        destinations AS destination
                    WHERE
                        destination.collection_id = later_collection.id
                    ORDER BY
                        destination.sort_order
                    LIMIT
                        1
                ) AS first_place
            WHERE
                later_collection.sort_order > here_collection.sort_order
            ORDER BY
                later_collection.sort_order
            LIMIT
                1
        )
        LIMIT
            1
    ) AS next_work ON true
    LEFT JOIN collections AS next_collection ON next_collection.id = next_work.collection_id
WHERE
    here.collection_id = p_collection_id
    AND here.work_id = p_work_id;
$$;
