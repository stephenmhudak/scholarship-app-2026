-- Track scorer assignments separately from actual scores
CREATE TABLE IF NOT EXISTS ApplicationScorers (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    ApplicationId CHAR(36) NOT NULL,
    ScoredById CHAR(36) NOT NULL,
    AssignedAt DATETIME NOT NULL,
    CONSTRAINT FK_AppScorers_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AppScorers_Users FOREIGN KEY (ScoredById) REFERENCES Users(Id),
    CONSTRAINT UQ_AppScorers UNIQUE (ApplicationId, ScoredById)
);

-- Migrate existing scorer assignments (Score=0 placeholder rows + real scores)
INSERT IGNORE INTO ApplicationScorers (Id, ApplicationId, ScoredById, AssignedAt)
SELECT UUID(), ApplicationId, ScoredById, ScoredAt FROM Scores;

-- Add per-section scoring support
ALTER TABLE Scores ADD COLUMN SectionId CHAR(36) NULL;
ALTER TABLE Scores ADD CONSTRAINT FK_Scores_Sections FOREIGN KEY (SectionId) REFERENCES Sections(Id) ON DELETE SET NULL;

-- Remove Score=0 placeholder rows (assignments now tracked in ApplicationScorers)
DELETE FROM Scores WHERE Score = 0;
