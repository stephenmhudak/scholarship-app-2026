-- Bumped on password, two-factor and admin account changes; tokens carry the version they were issued with,
-- so a bump ends every earlier session.
ALTER TABLE Users ADD COLUMN SessionVersion INT NOT NULL DEFAULT 0;
