-- The themes the website saved on the Theme page. The presets aren't here: they're in C#
CREATE TABLE themes (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_themes PRIMARY KEY (id),
    name varchar(100) COLLATE case_insensitive NOT NULL,
    CONSTRAINT unique_themes_name UNIQUE (name),
    CONSTRAINT check_themes_name_not_blank CHECK (btrim(name) <> ''),
    -- only what the theme chose, keyed by C#'s enum names, as {"colors": {"Page": "#202020"}}. One
    -- document rather than a column or a row per setting, since a theme is only ever read and saved
    -- whole, and a new setting then needs no migration
    settings jsonb NOT NULL,
    CONSTRAINT check_themes_settings_object CHECK (jsonb_typeof(settings) = 'object')
);

-- The theme the public pages use: one of the website's own, or a preset by the ThemePreset enum's id
CREATE TABLE theme_in_use (
    -- one row per website: the key can only be true, so a second row has nowhere to go
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_theme_in_use PRIMARY KEY (id),
    CONSTRAINT check_theme_in_use_one_row CHECK (id),
    theme_id int,
    -- delete_theme moves the website onto a preset before deleting the theme it uses
    CONSTRAINT foreign_key_theme_in_use_theme FOREIGN KEY (theme_id) REFERENCES themes (id),
    preset int,
    CONSTRAINT check_theme_in_use_one_theme CHECK (num_nonnulls(theme_id, preset) = 1)
);

-- a new website starts on Paper, ThemePreset.Paper's id
INSERT INTO theme_in_use (preset) VALUES (1);
