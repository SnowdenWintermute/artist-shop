-- The codes the platform operator hands out, each letting one person sign up for a website. Only a
-- hash is kept, so nothing read from here signs anyone up
CREATE TABLE sign_up_codes (
    id int GENERATED ALWAYS AS IDENTITY,
    CONSTRAINT primary_key_sign_up_codes PRIMARY KEY (id),
    -- SHA-256 of the code's 16 random bytes. A code that long can't be guessed, so a fast hash is
    -- enough; slow ones are for passwords people choose
    code_hash bytea NOT NULL,
    CONSTRAINT unique_sign_up_codes_code_hash UNIQUE (code_hash),
    CONSTRAINT check_sign_up_codes_code_hash_length CHECK (octet_length(code_hash) = 32),
    -- who the code is for, so the operator can tell unused codes apart
    note varchar(200) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    expires_at timestamptz NOT NULL
);
