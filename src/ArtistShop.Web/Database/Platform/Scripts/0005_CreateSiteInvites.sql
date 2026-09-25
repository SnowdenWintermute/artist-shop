-- Invitations to become one of a site's admins, each for an email. Signing in with that email is
-- what accepts one: accounts need a confirmed email to sign in, so no token is needed
CREATE TABLE site_invites (
    site_id int NOT NULL,
    CONSTRAINT foreign_key_site_invites_sites FOREIGN KEY (site_id) REFERENCES sites (id) ON DELETE CASCADE,
    -- 254 characters is the longest address email allows
    email varchar(254) NOT NULL,
    -- lowercase, as EmailAddress makes every address it reads, so an invitation matches an account's
    -- email however either was typed
    CONSTRAINT check_site_invites_email_lowercase CHECK (email = lower(email)),
    -- one per email on each site; inviting again replaces it
    CONSTRAINT primary_key_site_invites PRIMARY KEY (site_id, email),
    invited_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL
);
