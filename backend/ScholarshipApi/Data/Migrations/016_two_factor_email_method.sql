-- TwoFactorMethod is now what says 2FA is on: 'totp' (authenticator app) or 'email' (code sent at each sign-in)
ALTER TABLE Users
    ADD COLUMN TwoFactorMethod VARCHAR(16) NULL,
    ADD COLUMN EmailOtpSetupHash CHAR(64) NULL,
    ADD COLUMN EmailOtpSetupExpiresAt DATETIME NULL;

UPDATE Users SET TwoFactorMethod = 'totp' WHERE TotpEnabledAt IS NOT NULL;

-- Email-method sign-ins carry their emailed code on the challenge
ALTER TABLE TwoFactorChallenges
    ADD COLUMN EmailCodeHash CHAR(64) NULL,
    ADD COLUMN EmailCodeSentAt DATETIME NULL;
