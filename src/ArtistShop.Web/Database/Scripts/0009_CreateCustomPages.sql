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
    alignment int NOT NULL,
    -- RowHeight's id: whether the row fills the screen
    height int NOT NULL,
    -- RowContentWidth's id: whether the row's content stops at the page's width
    content_width int NOT NULL,
    -- RowVerticalPadding's ids: the space above and below the row's content
    top_padding int NOT NULL,
    bottom_padding int NOT NULL,
    -- RowSidePadding's id: the space beside it
    side_padding int NOT NULL,
    -- RowGap's id: the space between its slots and between the components stacked in each
    gap int NOT NULL,
    -- the row's own theme, a saved one or a ThemePreset's id, or neither for the website's. A saved
    -- theme's deletion leaves the row on the website's
    theme_id int,
    CONSTRAINT foreign_key_page_rows_themes FOREIGN KEY (theme_id) REFERENCES themes (id) ON DELETE SET NULL,
    theme_preset int,
    CONSTRAINT check_page_rows_one_theme CHECK (num_nonnulls(theme_id, theme_preset) <= 1),
    -- RowBackgroundType's id
    background_type int NOT NULL,
    -- RowBackground.Color's hex, or the colour behind an image or slideshow, null there for the row's page colour
    background_color varchar(9),
    CONSTRAINT check_page_rows_background_color CHECK (background_type <> 2 OR background_color IS NOT NULL),
    -- an image background's source: a library image (its foreign key is added with uploaded_images in
    -- 0011), or a work and its picked image, null for its main one. Neither until one is chosen. Deleting
    -- the work or image clears these first, leaving the colour
    background_uploaded_image_key char(32),
    background_work_id int,
    CONSTRAINT foreign_key_page_rows_works FOREIGN KEY (background_work_id) REFERENCES works (id),
    background_work_image_key char(32),
    -- ImageFit's id, for an image or slideshow background
    background_fit int,
    CONSTRAINT check_page_rows_background_image CHECK (
        num_nonnulls(background_uploaded_image_key, background_work_id) <= 1
        AND (background_work_image_key IS NULL OR background_work_id IS NOT NULL)
        AND (background_type = 3 OR num_nonnulls(background_uploaded_image_key, background_work_id) = 0)
        AND (background_type IN (3, 4)) = (background_fit IS NOT NULL)
    ),
    -- A slideshow background's SlideSourceType id, and for WorksInCollection its collection: not a foreign key, so
    -- a collection deleted since leaves the row naming it and showing nothing, as a card grid's settings do. Its
    -- chosen slides are in slides
    background_slide_source int,
    background_slide_collection_id int,
    -- SlideshowInterval's id
    background_slide_interval int,
    CONSTRAINT check_page_rows_background_slideshow CHECK (
        (background_type = 4) = (background_slide_source IS NOT NULL)
        AND (background_type = 4) = (background_slide_interval IS NOT NULL)
        AND (background_slide_source = 1) = (background_slide_collection_id IS NOT NULL)
    )
);

-- deleting a work clears the backgrounds showing it
CREATE INDEX index_page_rows_background_work_id ON page_rows (background_work_id);

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

-- A card grid's custom list (CardGridSource.ChosenCards): each row is one collection or one work, in the
-- artist's order. Kept while the grid shows another source, so switching back brings them back
CREATE TABLE card_grid_chosen_cards (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_card_grid_chosen_cards PRIMARY KEY (id),
    component_id int NOT NULL,
    CONSTRAINT foreign_key_card_grid_chosen_cards_custom_components FOREIGN KEY (component_id) REFERENCES custom_components (id) ON DELETE CASCADE,
    collection_id int,
    CONSTRAINT foreign_key_card_grid_chosen_cards_collections FOREIGN KEY (collection_id) REFERENCES collections (id) ON DELETE CASCADE,
    work_id int,
    CONSTRAINT foreign_key_card_grid_chosen_cards_works FOREIGN KEY (work_id) REFERENCES works (id) ON DELETE CASCADE,
    CONSTRAINT check_card_grid_chosen_cards_one_item CHECK (num_nonnulls(collection_id, work_id) = 1),
    -- NULLs are distinct, so a collection's row doesn't collide with a work's
    CONSTRAINT unique_card_grid_chosen_cards_collection UNIQUE (component_id, collection_id),
    CONSTRAINT unique_card_grid_chosen_cards_work UNIQUE (component_id, work_id),
    sort_order int NOT NULL,
    -- DEFERRABLE so a reorder can swap two cards in one statement
    CONSTRAINT unique_card_grid_chosen_cards_sort_order UNIQUE (component_id, sort_order)
    DEFERRABLE
);

-- the cascades from a deleted collection or work, and the work page's "mentioned in" list
CREATE INDEX index_card_grid_chosen_cards_collection ON card_grid_chosen_cards (collection_id);

CREATE INDEX index_card_grid_chosen_cards_work ON card_grid_chosen_cards (work_id);

-- The chosen slides of a carousel or a row's slideshow background, in the artist's order: one owner, a
-- component or a row. Each is one image: a library image (its foreign key is added with uploaded_images in
-- 0011), or a work and its picked image, null for its main one. The same image may be on a list twice. Kept
-- while the owner shows a collection instead, so switching back brings them back. Deleting the image, the work
-- or the owner deletes the slide
CREATE TABLE slides (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_slides PRIMARY KEY (id),
    component_id int,
    CONSTRAINT foreign_key_slides_custom_components FOREIGN KEY (component_id) REFERENCES custom_components (id) ON DELETE CASCADE,
    row_id int,
    CONSTRAINT foreign_key_slides_page_rows FOREIGN KEY (row_id) REFERENCES page_rows (id) ON DELETE CASCADE,
    CONSTRAINT check_slides_one_owner CHECK (num_nonnulls(component_id, row_id) = 1),
    uploaded_image_key char(32),
    work_id int,
    CONSTRAINT foreign_key_slides_works FOREIGN KEY (work_id) REFERENCES works (id) ON DELETE CASCADE,
    work_image_key char(32),
    CONSTRAINT check_slides_one_image CHECK (
        num_nonnulls(uploaded_image_key, work_id) = 1
        AND (work_image_key IS NULL OR work_id IS NOT NULL)
    ),
    sort_order int NOT NULL,
    -- DEFERRABLE so a reorder can swap two slides in one statement. NULLs are distinct, so a row's slides
    -- don't collide with a component's
    CONSTRAINT unique_slides_component_sort_order UNIQUE (component_id, sort_order)
    DEFERRABLE,
    CONSTRAINT unique_slides_row_sort_order UNIQUE (row_id, sort_order)
    DEFERRABLE
);

-- the cascade from a deleted work, and the delete warnings
CREATE INDEX index_slides_work ON slides (work_id);

-- the page posts link up to: one row per website, as home_page. It's chosen from the pages holding a
-- post list, and can't be deleted while it's the blog page
CREATE TABLE blog_page (
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_blog_page PRIMARY KEY (id),
    CONSTRAINT check_blog_page_one_row CHECK (id),
    page_id int NOT NULL,
    CONSTRAINT foreign_key_blog_page_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id)
);

-- the page collections link up to, as blog_page. It's chosen from the pages holding a card grid of all
-- collections, and can't be deleted while it's the collections page
CREATE TABLE collections_page (
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_collections_page PRIMARY KEY (id),
    CONSTRAINT check_collections_page_one_row CHECK (id),
    page_id int NOT NULL,
    CONSTRAINT foreign_key_collections_page_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id)
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

        -- RowWidths.Whole, RowAlignment.Start, RowHeight.Natural, RowContentWidth.Page, RowPadding.New, RowGap.Medium, RowBackgroundType.None
        INSERT INTO page_rows (page_id, sort_order, widths, alignment, height, content_width, top_padding, bottom_padding, side_padding, gap, background_type)
        VALUES (v_page_id, 0, 1, 1, 1, 1, 1, 1, 3, 3, 1)
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
    INSERT INTO collections_page (page_id) SELECT id FROM custom_pages WHERE address = 'home';
END;
$$;
