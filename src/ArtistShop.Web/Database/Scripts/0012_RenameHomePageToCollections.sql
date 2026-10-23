-- The page every website starts with, which 0009 named Home, is Collections. Only where the artist
-- hasn't renamed it, and the name and address only where no other page has taken them
UPDATE custom_pages SET name = 'Collections'
WHERE address = 'home' AND name = 'Home'
    AND NOT EXISTS (SELECT FROM custom_pages WHERE name = 'Collections');

UPDATE custom_pages SET address = 'collections'
WHERE address = 'home' AND name = 'Collections'
    AND NOT EXISTS (SELECT FROM custom_pages WHERE address = 'collections');
