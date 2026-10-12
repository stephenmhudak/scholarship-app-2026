ALTER TABLE Users
    ADD COLUMN TotpSecret VARCHAR(64) NULL,
    ADD COLUMN TotpPendingSecret VARCHAR(64) NULL,
    ADD COLUMN TotpEnabledAt DATETIME NULL,
    ADD COLUMN TotpLastUsedStep BIGINT NULL;

CREATE TABLE IF NOT EXISTS TwoFactorRecoveryCodes (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    CodeHash CHAR(64) NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    INDEX IX_TwoFactorRecoveryCodes_UserId (UserId),
    CONSTRAINT FK_TwoFactorRecoveryCodes_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Issued after a correct password for a 2FA-enrolled user; exchanged with a TOTP or recovery code for a JWT
CREATE TABLE IF NOT EXISTS TwoFactorChallenges (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    TokenHash CHAR(64) NOT NULL UNIQUE,
    Attempts INT NOT NULL DEFAULT 0,
    ExpiresAt DATETIME NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    INDEX IX_TwoFactorChallenges_UserId_CreatedAt (UserId, CreatedAt),
    CONSTRAINT FK_TwoFactorChallenges_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
