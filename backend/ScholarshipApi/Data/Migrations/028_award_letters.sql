-- What a scholarship is worth (pre-fills awards, and merges into letters when an award has no amount of its own)
ALTER TABLE Scholarships ADD COLUMN AwardAmount DECIMAL(12,2) NULL;

-- Award letters: a cycle's base letters (ScholarshipId null, one marked IsDefault) and scholarships' own letters
-- (ScholarshipId set, at most one each). Body is sanitized HTML with merge fields like {{awardee_name}}.
CREATE TABLE IF NOT EXISTS LetterTemplates (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    CycleId CHAR(36) NOT NULL,
    ScholarshipId CHAR(36) NULL UNIQUE,
    Name VARCHAR(255) NOT NULL,
    Body MEDIUMTEXT NOT NULL,
    IsDefault TINYINT(1) NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL,
    UpdatedAt DATETIME NOT NULL,
    UpdatedById CHAR(36) NULL,
    CONSTRAINT FK_LetterTemplates_Cycles FOREIGN KEY (CycleId) REFERENCES ScholarshipCycles(Id) ON DELETE CASCADE,
    CONSTRAINT FK_LetterTemplates_Scholarships FOREIGN KEY (ScholarshipId) REFERENCES Scholarships(Id) ON DELETE CASCADE,
    CONSTRAINT FK_LetterTemplates_Users FOREIGN KEY (UpdatedById) REFERENCES Users(Id) ON DELETE SET NULL
);
