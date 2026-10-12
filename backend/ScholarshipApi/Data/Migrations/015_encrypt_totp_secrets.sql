-- Authenticator secrets are now AES-GCM encrypted with TwoFactor:EncryptionKey, which needs wider columns.
-- 014 stored them in plaintext and was never released, so drop any such enrolments rather than migrate them.
UPDATE Users
SET TotpSecret = NULL, TotpPendingSecret = NULL, TotpEnabledAt = NULL, TotpLastUsedStep = NULL
WHERE TotpSecret IS NOT NULL OR TotpPendingSecret IS NOT NULL;

DELETE FROM TwoFactorRecoveryCodes;

ALTER TABLE Users
    MODIFY COLUMN TotpSecret VARCHAR(255) NULL,
    MODIFY COLUMN TotpPendingSecret VARCHAR(255) NULL;
