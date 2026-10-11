ALTER TABLE Users ADD COLUMN EmailVerifiedAt DATETIME NULL;

-- Existing accounts predate verification; treat them as verified
UPDATE Users SET EmailVerifiedAt = CreatedAt WHERE EmailVerifiedAt IS NULL;

CREATE TABLE IF NOT EXISTS EmailVerificationTokens (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    TokenHash CHAR(64) NOT NULL UNIQUE,
    ExpiresAt DATETIME NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    INDEX IX_EmailVerificationTokens_UserId (UserId),
    CONSTRAINT FK_EmailVerificationTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
