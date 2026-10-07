DROP FUNCTION IF EXISTS schedule_site_erasing_now;

-- The operator's deletion, which has no grace period and no owner to check: the site is due for
-- erasing from p_now, whether it was online or already being deleted. False when there's no such
-- site, as when it was erased since the page loaded
CREATE FUNCTION schedule_site_erasing_now (p_site_id int, p_now timestamptz) RETURNS boolean LANGUAGE plpgsql AS $$
BEGIN
    UPDATE sites
    SET
        erase_at = LEAST(erase_at, p_now)
    WHERE
        id = p_site_id;

    RETURN FOUND;
END;
$$;
