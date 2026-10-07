-- What the platform keeps about an account, as opposed to the identity database's sign-in details. One row
-- per account, made when it signs in. Accounts are in the identity database, which is a different
-- database, so user_id is Identity's user id with no foreign key; AccountDeletion deletes the row, and
-- everything hung off it goes with it
CREATE TABLE account_profiles (user_id text NOT NULL, CONSTRAINT primary_key_account_profiles PRIMARY KEY (user_id));

-- the hints an account dismissed, which don't show on any website it administers
CREATE TABLE account_dismissed_hints (
    user_id text NOT NULL,
    CONSTRAINT foreign_key_account_dismissed_hints_account_profiles FOREIGN KEY (user_id) REFERENCES account_profiles (user_id) ON DELETE CASCADE,
    -- a HintType's name. A name since removed is only ignored
    hint varchar(100) NOT NULL,
    CONSTRAINT primary_key_account_dismissed_hints PRIMARY KEY (user_id, hint)
);
