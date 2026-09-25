-- The artists' websites. Each site's own data is in a database of its own, named from this id (see
-- SiteDatabases), so nothing here says which database it is
CREATE TABLE sites (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_sites PRIMARY KEY (id),
    created_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

-- The host names a site is reached by. A request finds its site by its host, so a host belongs to
-- exactly one site
CREATE TABLE site_hosts (
    -- 253 characters is the longest name DNS allows
    host varchar(253) NOT NULL,
    CONSTRAINT primary_key_site_hosts PRIMARY KEY (host),
    -- lowercase, as HostName makes every host it reads, so a lookup never misses on case
    CONSTRAINT check_site_hosts_lowercase CHECK (host = lower(host)),
    site_id int NOT NULL,
    CONSTRAINT foreign_key_site_hosts_sites FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE CASCADE,
    -- the host the site's other hosts redirect to
    is_main boolean NOT NULL
);

-- a partial index: unique among the main hosts only, so a site has at most one
CREATE UNIQUE INDEX unique_site_hosts_main ON site_hosts (site_id)
WHERE
    is_main;
