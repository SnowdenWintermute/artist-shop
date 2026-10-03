-- The value a member last left an input at, such as the dashboard's artwork type, so the page comes
-- back with it. Hung off the membership: removing an admin, deleting an account or erasing a site
-- deletes the member's row, and these go with it. A value is only ever a hint about which of the
-- page's current options to show, so one the page no longer offers is ignored
CREATE TABLE site_member_input_values (
    site_id int NOT NULL,
    user_id text NOT NULL,
    CONSTRAINT foreign_key_site_member_input_values_site_members FOREIGN KEY (site_id, user_id) REFERENCES site_members (site_id, user_id) ON DELETE CASCADE,
    -- one of the names RememberedInputs lists
    input_name text NOT NULL,
    CONSTRAINT primary_key_site_member_input_values PRIMARY KEY (site_id, user_id, input_name),
    -- an option's value, such as an id, so short
    value varchar(100) NOT NULL
);
