-- The website's component library: each row is one of our templates (CustomComponentType's id)
-- with the artist's settings. Pages place components rather than own them, so one can be on several
-- pages, and taking one off its last page leaves it here
CREATE TABLE custom_components (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_custom_components PRIMARY KEY (id),
    type int NOT NULL,
    -- for finding it again in the library; a page never shows it. A new one gets its type and a number
    name varchar(100) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_custom_components_name UNIQUE (name),
    CONSTRAINT check_custom_components_name_not_blank CHECK (btrim(name) <> ''),
    -- keyed by the type's settings record, in one document so a new setting needs no migration
    settings jsonb NOT NULL,
    CONSTRAINT check_custom_components_settings_object CHECK (jsonb_typeof(settings) = 'object'),
    -- which shape of the type's settings the document has, so an old one can be read after a change
    settings_version int NOT NULL,
    -- tells the settings page's backup of unsaved edits whether the component was saved since
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE custom_pages (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_custom_pages PRIMARY KEY (id),
    name varchar(100) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_custom_pages_name UNIQUE (name),
    CONSTRAINT check_custom_pages_name_not_blank CHECK (btrim(name) <> ''),
    -- the page's address is /address
    address varchar(200) NOT NULL,
    CONSTRAINT unique_custom_pages_address UNIQUE (address)
);

-- the page / shows: one row per website, as theme_in_use. Its page can't be deleted while it's home
CREATE TABLE home_page (
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_home_page PRIMARY KEY (id),
    CONSTRAINT check_home_page_one_row CHECK (id),
    page_id int NOT NULL,
    CONSTRAINT foreign_key_home_page_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id)
);

CREATE TABLE page_rows (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_page_rows PRIMARY KEY (id),
    page_id int NOT NULL,
    CONSTRAINT foreign_key_page_rows_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id) ON DELETE CASCADE,
    sort_order int NOT NULL,
    -- DEFERRABLE for the same reason as unique_collections_sort_order
    CONSTRAINT unique_page_rows_page_sort_order UNIQUE (page_id, sort_order)
    DEFERRABLE,
    -- RowWidths's id: how the row shares its width between its slots
    widths int NOT NULL,
    -- RowAlignment's id: where slots of different heights line up
    alignment int NOT NULL
);

CREATE TABLE row_slots (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_row_slots PRIMARY KEY (id),
    row_id int NOT NULL,
    CONSTRAINT foreign_key_row_slots_page_rows FOREIGN KEY (row_id) REFERENCES page_rows (id) ON DELETE CASCADE,
    -- left to right
    sort_order int NOT NULL,
    CONSTRAINT unique_row_slots_row_sort_order UNIQUE (row_id, sort_order)
    DEFERRABLE
);

-- a component in a slot's stack. Deleting a slot takes its placements but never their components
CREATE TABLE slot_placements (
    slot_id int NOT NULL,
    CONSTRAINT foreign_key_slot_placements_row_slots FOREIGN KEY (slot_id) REFERENCES row_slots (id) ON DELETE CASCADE,
    -- top to bottom
    sort_order int NOT NULL,
    CONSTRAINT primary_key_slot_placements PRIMARY KEY (slot_id, sort_order)
    DEFERRABLE,
    component_id int NOT NULL,
    CONSTRAINT foreign_key_slot_placements_custom_components FOREIGN KEY (component_id) REFERENCES custom_components (id),
    -- a component shows once per slot, so a stack's order can be given as component ids
    CONSTRAINT unique_slot_placements_slot_component UNIQUE (slot_id, component_id)
);

-- what deleting a component checks for
CREATE INDEX index_slot_placements_component_id ON slot_placements (component_id);

-- the works a text component embeds, as post_and_works_junction is a post's. Derived from its text whenever
-- it's saved, never written on its own
CREATE TABLE custom_component_and_works_junction (
    component_id int NOT NULL,
    work_id int NOT NULL,
    CONSTRAINT primary_key_custom_component_and_works_junction PRIMARY KEY (component_id, work_id),
    CONSTRAINT foreign_key_custom_component_works_custom_components FOREIGN KEY (component_id) REFERENCES custom_components (id) ON DELETE CASCADE,
    CONSTRAINT foreign_key_custom_component_works_works FOREIGN KEY (work_id) REFERENCES works (id) ON DELETE CASCADE
);

-- the work page's "mentioned in" list, and the cascade from a deleted work
CREATE INDEX index_custom_component_and_works_junction_work ON custom_component_and_works_junction (work_id);

-- the page posts link up to: one row per website, as home_page. It's chosen from the pages holding a
-- post list, and can't be deleted while it's the blog page
CREATE TABLE blog_page (
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_blog_page PRIMARY KEY (id),
    CONSTRAINT check_blog_page_one_row CHECK (id),
    page_id int NOT NULL,
    CONSTRAINT foreign_key_blog_page_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id)
);

-- the nav bar's links, in the artist's order. A page with no row here is reached by its address only
CREATE TABLE nav_entries (
    page_id int NOT NULL,
    CONSTRAINT primary_key_nav_entries PRIMARY KEY (page_id),
    CONSTRAINT foreign_key_nav_entries_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id) ON DELETE CASCADE,
    sort_order int NOT NULL,
    CONSTRAINT unique_nav_entries_sort_order UNIQUE (sort_order)
    DEFERRABLE
);

-- Every website, new or not, starts with what it had before custom pages: a Home page holding one
-- collection viewer (CustomComponentType.CollectionViewer) and a Blog page at /posts holding one post
-- list (CustomComponentType.PostList), both in the nav. Neither type has settings yet
DO $$
DECLARE
    v_page_id int;
    v_row_id int;
    v_slot_id int;
    v_component_id int;
    v_page record;
BEGIN
    FOR v_page IN
        -- the settings as CustomComponentSettingsJson writes CardGridSettings.AllCollections and PostListSettings
        SELECT * FROM (VALUES
            ('Home', 'home', 'Collection viewer', 1,
                '{"source":{"type":"AllCollections"},"heading":{"type":"Automatic"},"cardsPerPage":24,"hasPageLinks":true,"layout":"EvenGrid"}', 0),
            ('Blog', 'posts', 'Post list', 2, '{}', 1))
            AS pages (name, address, component_name, component_type, component_settings, nav_order)
    LOOP
        INSERT INTO custom_components (type, name, settings, settings_version)
        VALUES (v_page.component_type, v_page.component_name, v_page.component_settings::jsonb, 1)
        RETURNING id INTO v_component_id;

        INSERT INTO custom_pages (name, address)
        VALUES (v_page.name, v_page.address)
        RETURNING id INTO v_page_id;

        -- RowWidths.Whole, RowAlignment.Start
        INSERT INTO page_rows (page_id, sort_order, widths, alignment)
        VALUES (v_page_id, 0, 1, 1)
        RETURNING id INTO v_row_id;

        INSERT INTO row_slots (row_id, sort_order)
        VALUES (v_row_id, 0)
        RETURNING id INTO v_slot_id;

        INSERT INTO slot_placements (slot_id, sort_order, component_id)
        VALUES (v_slot_id, 0, v_component_id);

        INSERT INTO nav_entries (page_id, sort_order) VALUES (v_page_id, v_page.nav_order);
    END LOOP;

    INSERT INTO home_page (page_id) SELECT id FROM custom_pages WHERE address = 'home';
    INSERT INTO blog_page (page_id) SELECT id FROM custom_pages WHERE address = 'posts';
END;
$$;
