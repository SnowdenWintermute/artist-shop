DROP FUNCTION IF EXISTS hand_over_site;

-- Makes the admin p_to_user_id the site's owner and its owner p_from_user_id an admin, in one
-- transaction. False when p_from_user_id isn't the owner or p_to_user_id isn't an admin, such as
-- after they left, so nothing changed
CREATE FUNCTION hand_over_site (p_site_id int, p_from_user_id text, p_to_user_id text) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    -- locked, so the admin can't leave between this check and their promotion
    PERFORM
        1
    FROM
        site_members
    WHERE
        site_id = p_site_id
        AND user_id = p_to_user_id
        AND role = 2
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN false;
    END IF;

    -- the owner first: unique_site_members_owner allows one owner at a time. 1 is SiteRole.Owner, 2 Admin
    UPDATE site_members
    SET
        role = 2
    WHERE
        site_id = p_site_id
        AND user_id = p_from_user_id
        AND role = 1;

    IF NOT FOUND THEN
        RETURN false;
    END IF;

    UPDATE site_members
    SET
        role = 1
    WHERE
        site_id = p_site_id
        AND user_id = p_to_user_id;

    RETURN true;
END;
$$;
