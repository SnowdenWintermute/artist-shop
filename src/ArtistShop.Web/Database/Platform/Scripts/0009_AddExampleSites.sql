-- Where a site sits among the examples the platform's home page links to, from 0; NULL for a site
-- that isn't one. The operator chooses them and drags their order. Postgres checks a plain UNIQUE
-- after every row, so an UPDATE that swaps two positions would collide halfway: DEFERRABLE moves the
-- check to the end of the statement. NULLs never collide
ALTER TABLE sites
ADD COLUMN example_sort_order int NULL,
ADD CONSTRAINT unique_sites_example_sort_order UNIQUE (example_sort_order) DEFERRABLE;
