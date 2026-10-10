-- The order the operator puts the fonts in, which the Theme page lists them in. The fonts themselves are
-- in code (Domain/Website/Font.cs); a font with no row comes after the rest, and a name since removed is
-- only ignored
CREATE TABLE font_order (
    -- a Font's name
    font varchar(100) NOT NULL,
    CONSTRAINT primary_key_font_order PRIMARY KEY (font),
    sort_order int NOT NULL
);
