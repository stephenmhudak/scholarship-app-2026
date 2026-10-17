-- Deactivated accounts can't sign in, and their sessions end. Their applications, scores, references and awards stay.
ALTER TABLE Users ADD COLUMN DeactivatedAt DATETIME NULL;
