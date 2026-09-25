-- Who may administer each site. Accounts are in the identity database, which is a different database,
-- so user_id is Identity's user id with no foreign key
CREATE TABLE site_members (
    site_id int NOT NULL,
    CONSTRAINT foreign_key_site_members_sites FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE CASCADE,
    user_id text NOT NULL,
    CONSTRAINT primary_key_site_members PRIMARY KEY (site_id, user_id),
    -- SiteRole: 1 is the owner, 2 an admin
    role smallint NOT NULL,
    CONSTRAINT check_site_members_role CHECK (role IN (1, 2))
);

-- a partial index: unique among the owners only, so a site has at most one. add_site gives it one
CREATE UNIQUE INDEX unique_site_members_owner ON site_members (site_id)
WHERE
    role = 1;
