DROP FUNCTION IF EXISTS add_slide;

-- One image joins the end of the owner's slides: a library image, or a work and its picked image (null for
-- its main one). An image or work deleted since the picker showed it is named rather than left to the
-- foreign key's error. plpgsql, so lock_slides is looked up when it runs and the files can run in any order
CREATE FUNCTION add_slide (
    p_component_id int,
    p_row_id int,
    p_uploaded_image_key text,
    p_work_id int,
    p_work_image_key text
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    last_place int;
BEGIN
    last_place := lock_slides(p_component_id, p_row_id);

    IF p_uploaded_image_key IS NOT NULL THEN
        PERFORM 1 FROM uploaded_images WHERE storage_key = p_uploaded_image_key;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The image no longer exists.' USING ERRCODE = 'SH029';
        END IF;
    END IF;

    IF p_work_id IS NOT NULL THEN
        PERFORM 1 FROM works WHERE id = p_work_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'The work no longer exists.' USING ERRCODE = 'SH003';
        END IF;
    END IF;

    INSERT INTO
        slides (component_id, row_id, uploaded_image_key, work_id, work_image_key, sort_order)
    VALUES
        (p_component_id, p_row_id, p_uploaded_image_key, p_work_id, p_work_image_key, last_place + 1);
END;
$$;
