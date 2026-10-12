CREATE TABLE IF NOT EXISTS PasswordResetCodes (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    UserId CHAR(36) NOT NULL,
    CodeHash CHAR(64) NOT NULL,
    Attempts INT NOT NULL DEFAULT 0,
    ExpiresAt DATETIME NOT NULL,
    UsedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    INDEX IX_PasswordResetCodes_UserId (UserId),
    CONSTRAINT FK_PasswordResetCodes_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
