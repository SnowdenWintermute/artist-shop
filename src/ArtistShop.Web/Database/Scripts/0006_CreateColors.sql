-- defined by us; the ids must match the ColorRole enum in C#
CREATE TABLE color_roles (
    id int,
    CONSTRAINT primary_key_color_roles PRIMARY KEY (id),
    name varchar(50) NOT NULL,
    CONSTRAINT unique_color_roles_name UNIQUE (name)
);

INSERT INTO
    color_roles (id, name)
VALUES
    (1, 'Page'),
    (2, 'Ink'),
    (3, 'Accent'),
    (4, 'Bar'),
    (5, 'Panel'),
    (6, 'Placeholder'),
    (7, 'InkFaded'),
    (8, 'InkUnavailable'),
    (9, 'Link'),
    (10, 'LinkHover'),
    (11, 'OnAccent'),
    (12, 'Rule'),
    (13, 'Lightbox'),
    (14, 'OnLightbox'),
    (15, 'Backdrop');

-- The colours the website's public pages use, one row per role the artist chose. A role without a
-- row is derived from the others, so a new role needs no row for every website
CREATE TABLE colors (
    color_role_id int,
    CONSTRAINT primary_key_colors PRIMARY KEY (color_role_id),
    CONSTRAINT foreign_key_colors_color_role FOREIGN KEY (color_role_id) REFERENCES color_roles (id),
    -- "#rrggbb", or "#rrggbbaa" for one that's partly see-through, lowercase as C# writes it
    color varchar(9) NOT NULL,
    CONSTRAINT check_colors_hex CHECK (color ~ '^#[0-9a-f]{6}([0-9a-f]{2})?$')
);
