-- What a component links to (LinkTarget), one row for one of a page, work, collection or post; an address
-- has no row. Derived from the settings whenever they're saved, never written on its own. The delete
-- functions read it to drop the links before the target goes; the cascade then takes the row
CREATE TABLE custom_component_links (
    component_id int NOT NULL,
    CONSTRAINT primary_key_custom_component_links PRIMARY KEY (component_id),
    CONSTRAINT foreign_key_custom_component_links_custom_components FOREIGN KEY (component_id) REFERENCES custom_components (id) ON DELETE CASCADE,
    page_id int,
    CONSTRAINT foreign_key_custom_component_links_custom_pages FOREIGN KEY (page_id) REFERENCES custom_pages (id) ON DELETE CASCADE,
    work_id int,
    CONSTRAINT foreign_key_custom_component_links_works FOREIGN KEY (work_id) REFERENCES works (id) ON DELETE CASCADE,
    collection_id int,
    CONSTRAINT foreign_key_custom_component_links_collections FOREIGN KEY (collection_id) REFERENCES collections (id) ON DELETE CASCADE,
    post_id int,
    CONSTRAINT foreign_key_custom_component_links_posts FOREIGN KEY (post_id) REFERENCES posts (id) ON DELETE CASCADE,
    CONSTRAINT check_custom_component_links_one_target CHECK (num_nonnulls(page_id, work_id, collection_id, post_id) = 1)
);

-- the delete functions' lookups, and the cascades
CREATE INDEX index_custom_component_links_page ON custom_component_links (page_id);
CREATE INDEX index_custom_component_links_work ON custom_component_links (work_id);
CREATE INDEX index_custom_component_links_collection ON custom_component_links (collection_id);
CREATE INDEX index_custom_component_links_post ON custom_component_links (post_id);
