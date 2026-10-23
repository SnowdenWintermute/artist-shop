DROP FUNCTION IF EXISTS combine_works;

-- Makes several works one, for images uploaded as separate works that were one all along. The kept
-- work takes the others' images, collections and terms, the slides and row backgrounds showing them
-- and the links to them, then they're deleted as delete_works deletes, along with whatever else shows them. The others
-- come in the order given, which decides their images' order and whose term a mutually exclusive
-- vocabulary keeps. An id already deleted is passed over. plpgsql, so the functions it calls are
-- looked up when it runs and the files can run in any order
CREATE FUNCTION combine_works (p_kept_id int, p_other_ids int[]) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_work_type_id int;
    -- the collections whose cover is one of the others
    v_covered_collection_ids int[];
BEGIN
    p_other_ids := array_remove(p_other_ids, p_kept_id);

    -- id order, so two combines can't each hold a work the other wants; an upload appending to one of
    -- them waits, as it does for update_work_images
    PERFORM
    FROM
        works AS work
    WHERE
        work.id = p_kept_id
        OR work.id = ANY (p_other_ids)
    ORDER BY
        work.id
    FOR NO KEY UPDATE;

    SELECT
        work.work_type_id INTO v_work_type_id
    FROM
        works AS work
    WHERE
        work.id = p_kept_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
    END IF;

    -- terms belong to a work type, so another type's wouldn't fit the kept work
    IF EXISTS (
        SELECT
        FROM
            works AS work
        WHERE
            work.id = ANY (p_other_ids)
            AND work.work_type_id <> v_work_type_id
    ) THEN
        RAISE EXCEPTION 'Works of different types can''t be combined.';
    END IF;

    -- Before the images move, while each other work's main image can still be told from the kept
    -- work's. A slide without a key shows its work's main image, so it gets that image's key and goes
    -- on showing the same picture
    UPDATE slides AS slide
    SET
        work_id = p_kept_id,
        work_image_key = COALESCE(slide.work_image_key, work_main_image_key(slide.work_id))
    WHERE
        slide.work_id = ANY (p_other_ids);

    UPDATE page_rows AS page_row
    SET
        background_work_id = p_kept_id,
        background_work_image_key = COALESCE(page_row.background_work_image_key, work_main_image_key(page_row.background_work_id))
    WHERE
        page_row.background_work_id = ANY (p_other_ids);

    UPDATE custom_components AS component
    SET
        settings = jsonb_set(component.settings, '{linkTarget,workId}', to_jsonb(p_kept_id)),
        updated_at = now()
    FROM
        custom_component_links AS link
    WHERE
        link.component_id = component.id
        AND link.work_id = ANY (p_other_ids);

    UPDATE custom_component_links
    SET
        work_id = p_kept_id
    WHERE
        work_id = ANY (p_other_ids);

    -- after the kept work's own, each work's in its own order. Only a storage key names an image file,
    -- so moving the rows moves the images
    UPDATE work_images AS image
    SET
        work_id = p_kept_id,
        is_primary = false,
        sort_order = moved.sort_order
    FROM
        (
            SELECT
                other_image.id,
                COALESCE(
                    (
                        SELECT
                            MAX(kept_image.sort_order)
                        FROM
                            work_images AS kept_image
                        WHERE
                            kept_image.work_id = p_kept_id
                    ),
                    -1
                ) + row_number() OVER (
                    ORDER BY
                        array_position(p_other_ids, other_image.work_id),
                        other_image.sort_order
                ) AS sort_order
            FROM
                work_images AS other_image
            WHERE
                other_image.work_id = ANY (p_other_ids)
        ) AS moved
    WHERE
        image.id = moved.id;

    -- the kept work's main image stays its main one; one that had none takes the first
    UPDATE work_images AS image
    SET
        is_primary = true
    WHERE
        image.id = (
            SELECT
                first_image.id
            FROM
                work_images AS first_image
            WHERE
                first_image.work_id = p_kept_id
            ORDER BY
                first_image.sort_order
            LIMIT
                1
        )
        AND NOT EXISTS (
            SELECT
            FROM
                work_images AS primary_image
            WHERE
                primary_image.work_id = p_kept_id
                AND primary_image.is_primary
        );

    -- With no conflict target, DO NOTHING skips a row breaking any unique index: a term the kept work
    -- already has, and a second term in a mutually exclusive vocabulary, where the kept work's term
    -- stays, or else the first one inserted
    INSERT INTO
        work_and_vocabulary_terms_junction (work_id, work_type_id, term_id, vocabulary_id, is_mutually_exclusive)
    SELECT
        p_kept_id,
        junction.work_type_id,
        junction.term_id,
        junction.vocabulary_id,
        junction.is_mutually_exclusive
    FROM
        work_and_vocabulary_terms_junction AS junction
    WHERE
        junction.work_id = ANY (p_other_ids)
    ORDER BY
        array_position(p_other_ids, junction.work_id)
    ON CONFLICT DO NOTHING;

    -- the lock set_work_collections takes on each collection, as this changes their works too
    PERFORM
    FROM
        collections AS collection
    WHERE
        collection.id IN (
            SELECT
                junction.collection_id
            FROM
                work_and_collection_junction AS junction
            WHERE
                junction.work_id = ANY (p_other_ids)
        )
    ORDER BY
        collection.id
    FOR NO KEY UPDATE;

    SELECT
        array_agg(junction.collection_id) INTO v_covered_collection_ids
    FROM
        work_and_collection_junction AS junction
    WHERE
        junction.work_id = ANY (p_other_ids)
        AND junction.is_cover;

    -- In a collection the kept work isn't in, it takes the place of the first of the others there.
    -- DISTINCT ON keeps the first row of each collection in the ORDER BY
    UPDATE work_and_collection_junction AS junction
    SET
        work_id = p_kept_id
    FROM
        (
            SELECT DISTINCT
                ON (other.collection_id) other.work_id,
                other.collection_id
            FROM
                work_and_collection_junction AS other
            WHERE
                other.work_id = ANY (p_other_ids)
                AND other.collection_id NOT IN (
                    SELECT
                        kept.collection_id
                    FROM
                        work_and_collection_junction AS kept
                    WHERE
                        kept.work_id = p_kept_id
                )
            ORDER BY
                other.collection_id,
                other.sort_order
        ) AS first_other
    WHERE
        junction.work_id = first_other.work_id
        AND junction.collection_id = first_other.collection_id;

    -- now rather than in the cascade, so the kept work can take a cover one of these held
    DELETE FROM work_and_collection_junction
    WHERE
        work_id = ANY (p_other_ids);

    UPDATE work_and_collection_junction
    SET
        is_cover = true
    WHERE
        work_id = p_kept_id
        AND collection_id = ANY (v_covered_collection_ids);

    PERFORM delete_works(p_other_ids);
END;
$$;
