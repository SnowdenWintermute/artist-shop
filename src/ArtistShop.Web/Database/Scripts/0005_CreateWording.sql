-- What the website calls collections and works, on its public pages and in its admin. A NULL word
-- means the default, so a website that never chose any follows a change to the defaults
CREATE TABLE wording (
    -- one row per website: the key can only be true, so a second row has nowhere to go
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_wording PRIMARY KEY (id),
    CONSTRAINT check_wording_one_row CHECK (id),
    collection_singular varchar(40),
    collection_plural varchar(40),
    -- false capitalises the word in headings and lowercases it inside sentences
    collection_keeps_case boolean NOT NULL DEFAULT false,
    work_singular varchar(40),
    work_plural varchar(40),
    work_keeps_case boolean NOT NULL DEFAULT false,
    -- both forms or neither, so a plural is never guessed
    CONSTRAINT check_wording_collection_both_forms CHECK ((collection_singular IS NULL) = (collection_plural IS NULL)),
    CONSTRAINT check_wording_work_both_forms CHECK ((work_singular IS NULL) = (work_plural IS NULL)),
    -- a word is printed with its first letter capitalised, so it can't be blank
    CONSTRAINT check_wording_words_not_blank CHECK (
        btrim(collection_singular) <> ''
        AND btrim(collection_plural) <> ''
        AND btrim(work_singular) <> ''
        AND btrim(work_plural) <> ''
    )
);

INSERT INTO wording DEFAULT VALUES;
