DROP FUNCTION IF EXISTS rich_text_without_images;

-- The text with every embed of these uploaded images taken out, as the editor would if each were
-- deleted by hand. Anything else in the text is left as it is
CREATE FUNCTION rich_text_without_images (p_body jsonb, p_storage_keys text[]) RETURNS jsonb LANGUAGE sql IMMUTABLE AS $$
SELECT
    jsonb_set(
        p_body,
        '{ops}',
        COALESCE(
            (
                SELECT
                    jsonb_agg(kept.op ORDER BY kept.position)
                FROM
                    jsonb_array_elements(p_body -> 'ops') WITH ORDINALITY AS kept (op, position)
                WHERE
                    -- #>> follows the path to the storage key, NULL for anything that isn't an image
                    -- embed, which the empty text never matches
                    NOT (COALESCE(kept.op #>> '{insert,artshop-image,storageKey}', '') = ANY (p_storage_keys))
            ),
            '[]'::jsonb
        )
    );
$$;
