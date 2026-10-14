-- Scholarships offered in a cycle. Each has eligibility questions (in the cycle's shared "Scholarship Eligibility"
-- section), its own questions (in a section of its own, shown only to eligible applicants), an eligibility rule,
-- the accounts allowed to award it, and its awards.

CREATE TABLE IF NOT EXISTS Scholarships (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    CycleId CHAR(36) NOT NULL,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    -- 'open' (everyone is eligible) or 'conditional' (any/all of ScholarshipConditions match the answers)
    Eligibility VARCHAR(20) NOT NULL DEFAULT 'open',
    EligibilityMatch VARCHAR(10) NOT NULL DEFAULT 'any',
    `Order` INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL,
    CONSTRAINT FK_Scholarships_Cycles FOREIGN KEY (CycleId) REFERENCES ScholarshipCycles(Id) ON DELETE CASCADE
);

-- Same shape as the other conditions: an application question plus the option, school or text it must match
CREATE TABLE IF NOT EXISTS ScholarshipConditions (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    ScholarshipId CHAR(36) NOT NULL,
    QuestionId CHAR(36) NOT NULL,
    OptionId CHAR(36) NULL,
    SchoolId CHAR(36) NULL,
    TextContains VARCHAR(255) NULL,
    CONSTRAINT FK_ScholarshipConditions_Scholarships FOREIGN KEY (ScholarshipId) REFERENCES Scholarships(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScholarshipConditions_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScholarshipConditions_Options FOREIGN KEY (OptionId) REFERENCES QuestionOptions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScholarshipConditions_Schools FOREIGN KEY (SchoolId) REFERENCES Schools(Id) ON DELETE CASCADE
);

-- The accounts allowed to award a scholarship (super admins can award any)
CREATE TABLE IF NOT EXISTS ScholarshipAwarders (
    ScholarshipId CHAR(36) NOT NULL,
    UserId CHAR(36) NOT NULL,
    PRIMARY KEY (ScholarshipId, UserId),
    CONSTRAINT FK_ScholarshipAwarders_Scholarships FOREIGN KEY (ScholarshipId) REFERENCES Scholarships(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScholarshipAwarders_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ScholarshipAwards (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    ScholarshipId CHAR(36) NOT NULL,
    ApplicationId CHAR(36) NOT NULL,
    Amount DECIMAL(12,2) NULL,
    Note TEXT NULL,
    AwardedById CHAR(36) NULL,
    AwardedAt DATETIME NOT NULL,
    CONSTRAINT UQ_ScholarshipAwards UNIQUE (ScholarshipId, ApplicationId),
    CONSTRAINT FK_ScholarshipAwards_Scholarships FOREIGN KEY (ScholarshipId) REFERENCES Scholarships(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScholarshipAwards_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScholarshipAwards_Users FOREIGN KEY (AwardedById) REFERENCES Users(Id) ON DELETE SET NULL
);

-- Sections managed by scholarships: Kind 'eligibility' (one per cycle, shared) or a scholarship's own section
-- (ScholarshipId set; its questions are only shown to eligible applicants)
ALTER TABLE Sections ADD COLUMN Kind VARCHAR(20) NULL;
ALTER TABLE Sections ADD COLUMN ScholarshipId CHAR(36) NULL;
ALTER TABLE Sections ADD CONSTRAINT FK_Sections_Scholarships FOREIGN KEY (ScholarshipId) REFERENCES Scholarships(Id) ON DELETE CASCADE;

-- Eligibility questions belong to a scholarship (they live in the eligibility section)
ALTER TABLE Questions ADD COLUMN ScholarshipId CHAR(36) NULL;
ALTER TABLE Questions ADD CONSTRAINT FK_Questions_Scholarships FOREIGN KEY (ScholarshipId) REFERENCES Scholarships(Id) ON DELETE CASCADE;

-- Shown in navigation for anyone assigned to award a scholarship (added per user at sign-in); super admins have all
INSERT IGNORE INTO Permissions (Id, Name, DisplayName, Description) VALUES
    ('b0000000-0000-0000-0000-000000000014', 'award_scholarships', 'Award Scholarships', 'Award the scholarships you are assigned to');
