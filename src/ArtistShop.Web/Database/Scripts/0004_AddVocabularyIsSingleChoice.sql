-- A single-choice vocabulary allows an artwork at most one of its terms, like "Time of day". Every
-- vocabulary so far allows several, so the existing rows are filled with false
ALTER TABLE vocabularies
ADD COLUMN is_single_choice boolean NOT NULL DEFAULT false;

-- the target of the junction's foreign key below, which has to match a unique pair of columns
ALTER TABLE vocabularies
ADD CONSTRAINT unique_vocabularies_id_is_single_choice UNIQUE (id, is_single_choice);

-- Copied from the vocabulary, as artwork_type_id is from the artwork, so an index on this table can
-- see it. The default only fills the existing rows: without one, an insert that forgets to copy the
-- flag fails instead of getting past the index
ALTER TABLE artwork_and_vocabulary_terms_junction
ADD COLUMN is_single_choice boolean NOT NULL DEFAULT false;

ALTER TABLE artwork_and_vocabulary_terms_junction
ALTER COLUMN is_single_choice
DROP DEFAULT;

-- ON UPDATE CASCADE copies a change of the vocabulary's flag into its rows here
ALTER TABLE artwork_and_vocabulary_terms_junction
ADD CONSTRAINT foreign_key_artwork_terms_vocabularies_is_single_choice FOREIGN KEY (vocabulary_id, is_single_choice) REFERENCES vocabularies (id, is_single_choice) ON UPDATE CASCADE;

-- one term per artwork, only in single-choice vocabularies
CREATE UNIQUE INDEX unique_artwork_and_vocabulary_terms_junction_single_choice ON artwork_and_vocabulary_terms_junction (artwork_id, vocabulary_id)
WHERE
    is_single_choice;
