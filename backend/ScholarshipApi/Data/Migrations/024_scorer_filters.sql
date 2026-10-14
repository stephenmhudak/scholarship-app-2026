-- Scorers can be limited to applications whose answers match. 'all' (default), or 'filtered': only applications
-- matching any of their ScorerFilters. A filtered scorer whose filters are all removed (their question or option
-- was deleted) sees nothing, never everything.
ALTER TABLE Users ADD COLUMN ScoringScope VARCHAR(20) NOT NULL DEFAULT 'all';

-- Matches when the application's answer to QuestionId includes OptionId (choice questions) or contains
-- TextContains, ignoring case (text questions).
CREATE TABLE IF NOT EXISTS ScorerFilters (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    ScorerId CHAR(36) NOT NULL,
    QuestionId CHAR(36) NOT NULL,
    OptionId CHAR(36) NULL,
    TextContains VARCHAR(255) NULL,
    CONSTRAINT FK_ScorerFilters_Users FOREIGN KEY (ScorerId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScorerFilters_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScorerFilters_Options FOREIGN KEY (OptionId) REFERENCES QuestionOptions(Id) ON DELETE CASCADE
);
