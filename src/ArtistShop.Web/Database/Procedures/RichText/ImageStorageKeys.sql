DROP FUNCTION IF EXISTS rich_text_image_storage_keys;

-- The uploaded images a text from the editor names, read as rich_text_work_ids reads its works
CREATE FUNCTION rich_text_image_storage_keys (p_body jsonb) RETURNS SETOF text LANGUAGE sql IMMUTABLE AS $$
SELECT
    -- #>> '{}' is the JSON string's text, without its quotes
    embedded.storage_key #>> '{}'
FROM
    jsonb_path_query(p_body, '$.ops[*].insert."artshop-image".storageKey ? (@.type() == "string")') AS embedded (storage_key);
$$;
