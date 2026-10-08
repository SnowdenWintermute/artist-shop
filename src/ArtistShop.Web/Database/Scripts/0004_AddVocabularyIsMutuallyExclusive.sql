-- A mutually exclusive vocabulary allows an artwork at most one of its terms, like "Time of day". Every
-- vocabulary so far allows several, so the existing rows are filled with false
ALTER TABLE vocabularies
ADD COLUMN is_mutually_exclusive boolean NOT NULL DEFAULT false;

-- the target of the junction's foreign key below, which has to match a unique pair of columns
ALTER TABLE vocabularies
ADD CONSTRAINT unique_vocabularies_id_is_mutually_exclusive UNIQUE (id, is_mutually_exclusive);

-- Copied from the vocabulary, as artwork_type_id is from the artwork, so an index on this table can
-- see it. The default only fills the existing rows: without one, an insert that forgets to copy the
-- flag fails instead of getting past the index
ALTER TABLE artwork_and_vocabulary_terms_junction
ADD COLUMN is_mutually_exclusive boolean NOT NULL DEFAULT false;

ALTER TABLE artwork_and_vocabulary_terms_junction
ALTER COLUMN is_mutually_exclusive
DROP DEFAULT;

-- ON UPDATE CASCADE copies a change of the vocabulary's flag into its rows here
ALTER TABLE artwork_and_vocabulary_terms_junction
ADD CONSTRAINT foreign_key_artwork_terms_vocabularies_is_mutually_exclusive FOREIGN KEY (vocabulary_id, is_mutually_exclusive) REFERENCES vocabularies (id, is_mutually_exclusive) ON UPDATE CASCADE;

-- one term per artwork, only in mutually exclusive vocabularies
CREATE UNIQUE INDEX unique_artwork_and_vocabulary_terms_junction_mutually_exclusive ON artwork_and_vocabulary_terms_junction (artwork_id, vocabulary_id)
WHERE
    is_mutually_exclusive;
