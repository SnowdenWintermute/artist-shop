CREATE TABLE posts (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_posts PRIMARY KEY (id),
    title varchar(200) NOT NULL,
    -- follows the title, like a series slug, and a title whose slug another post has is refused
    slug varchar(200) NOT NULL,
    CONSTRAINT unique_posts_slug UNIQUE (slug),
    -- the editor's document as it saved it: a Quill Delta, {"ops": [...]}. jsonb stores it parsed,
    -- so a malformed document is refused on the way in and the functions below can read inside it
    body jsonb NOT NULL,
    -- -> reads one key of a json object; jsonb_typeof names the kind of value it found. With no ops
    -- key both are NULL, and = would make the check UNKNOWN, which passes. IS NOT DISTINCT FROM
    -- compares NULL as a value, so a missing key fails
    CONSTRAINT check_posts_body_ops CHECK (jsonb_typeof(body -> 'ops') IS NOT DISTINCT FROM 'array'),
    -- NULL is a draft. Visitors see a post once this has passed, so a date in the future would
    -- schedule it without another column
    published_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- the artworks a post embeds. Derived from the body whenever it's saved, never written on its own
CREATE TABLE post_and_artworks_junction (
    post_id int NOT NULL,
    artwork_id int NOT NULL,
    CONSTRAINT primary_key_post_and_artworks_junction PRIMARY KEY (post_id, artwork_id),
    CONSTRAINT foreign_key_post_artworks_posts FOREIGN KEY (post_id) REFERENCES posts (id) ON DELETE CASCADE,
    CONSTRAINT foreign_key_post_artworks_artworks FOREIGN KEY (artwork_id) REFERENCES artworks (id) ON DELETE CASCADE
);

-- the primary key leads with post_id, so it can't find an artwork's posts: this serves the artwork
-- page's "mentioned in" list and the cascade from a deleted artwork
CREATE INDEX index_post_and_artworks_junction_artwork ON post_and_artworks_junction (artwork_id);
