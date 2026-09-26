-- When a site its owner deleted is erased: its schema, images and this row, with its hosts and
-- members. Until then its hosts serve nothing but stay taken, and its owner can keep it. NULL for a
-- site that isn't being deleted
ALTER TABLE sites
ADD COLUMN erase_at timestamptz NULL;
