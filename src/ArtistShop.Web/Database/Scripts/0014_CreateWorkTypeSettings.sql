-- Settings for every work type: one row per website, as wording
CREATE TABLE work_type_settings (
    id boolean NOT NULL DEFAULT true,
    CONSTRAINT primary_key_work_type_settings PRIMARY KEY (id),
    CONSTRAINT check_work_type_settings_one_row CHECK (id),
    -- null until the artist saves, for WorkTypeSettings.Default's, so the default lives only in C#.
    -- DimensionDisplay's id
    dimension_display int
);

INSERT INTO work_type_settings DEFAULT VALUES;
