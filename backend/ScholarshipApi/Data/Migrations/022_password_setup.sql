-- One-time links that let someone create their own password, for counselors added by a school admin or an admin.
-- Until a link is used the account has no password (Users.PasswordHash = '') and can't sign in.
CREATE TABLE IF NOT EXISTS PasswordSetupTokens (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    TokenHash CHAR(64) NOT NULL UNIQUE,
    ExpiresAt DATETIME NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    CONSTRAINT FK_PasswordSetupTokens_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
