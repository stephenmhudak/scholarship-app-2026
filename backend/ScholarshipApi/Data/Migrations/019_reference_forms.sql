-- References: per-cycle forms (with their own questions) that a referee fills in and signs through a
-- link the applicant shares. Replaces the old References/ReferenceDocuments upload, which had no way
-- to be requested from the app; those tables are left in place, unused.

CREATE TABLE IF NOT EXISTS ReferenceForms (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    CycleId CHAR(36) NOT NULL,
    Title VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    -- How many references of this form an applicant needs before submitting
    RequiredCount INT NOT NULL DEFAULT 1,
    -- 'always', or 'conditional': only required when one of the form's conditions matches the application's answers
    Requirement VARCHAR(20) NOT NULL DEFAULT 'always',
    `Order` INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL,
    CONSTRAINT FK_ReferenceForms_Cycles FOREIGN KEY (CycleId) REFERENCES ScholarshipCycles(Id) ON DELETE CASCADE
);

-- A condition matches when the application question's answer includes OptionId (choice questions)
-- or contains TextContains, ignoring case (text questions).
CREATE TABLE IF NOT EXISTS ReferenceFormConditions (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    FormId CHAR(36) NOT NULL,
    QuestionId CHAR(36) NOT NULL,
    OptionId CHAR(36) NULL,
    TextContains VARCHAR(255) NULL,
    CONSTRAINT FK_RefConditions_Forms FOREIGN KEY (FormId) REFERENCES ReferenceForms(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefConditions_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefConditions_Options FOREIGN KEY (OptionId) REFERENCES QuestionOptions(Id) ON DELETE CASCADE
);

-- Reference form questions live alongside application questions; application queries filter on ReferenceFormId IS NULL.
ALTER TABLE Questions ADD COLUMN ReferenceFormId CHAR(36) NULL;
ALTER TABLE Questions ADD CONSTRAINT FK_Questions_ReferenceForms FOREIGN KEY (ReferenceFormId) REFERENCES ReferenceForms(Id) ON DELETE CASCADE;

-- One per link. The 10-character code is stored hashed for lookup and AES-GCM encrypted so the applicant can copy it again.
CREATE TABLE IF NOT EXISTS ReferenceRequests (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    ApplicationId CHAR(36) NOT NULL,
    FormId CHAR(36) NOT NULL,
    Label VARCHAR(255) NULL,
    CodeHash CHAR(64) NOT NULL UNIQUE,
    CodeEncrypted VARCHAR(255) NOT NULL,
    Status VARCHAR(20) NOT NULL DEFAULT 'pending',
    SignerName VARCHAR(200) NULL,
    SignaturePath VARCHAR(1000) NULL,
    SubmittedAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL,
    CONSTRAINT FK_RefRequests_Applications FOREIGN KEY (ApplicationId) REFERENCES Applications(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefRequests_Forms FOREIGN KEY (FormId) REFERENCES ReferenceForms(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ReferenceAnswers (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    RequestId CHAR(36) NOT NULL,
    QuestionId CHAR(36) NOT NULL,
    TextValue TEXT NULL,
    SelectedOptions JSON NULL,
    CONSTRAINT FK_RefAnswers_Requests FOREIGN KEY (RequestId) REFERENCES ReferenceRequests(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefAnswers_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ReferenceFiles (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    RequestId CHAR(36) NOT NULL,
    QuestionId CHAR(36) NULL,
    FileName VARCHAR(255) NOT NULL,
    StoragePath VARCHAR(1000) NOT NULL,
    UploadedAt DATETIME NOT NULL,
    CONSTRAINT FK_RefFiles_Requests FOREIGN KEY (RequestId) REFERENCES ReferenceRequests(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RefFiles_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE SET NULL
);

-- A score is for a section (SectionId), the whole application (both null), or one reference (ReferenceRequestId)
ALTER TABLE Scores ADD COLUMN ReferenceRequestId CHAR(36) NULL;
ALTER TABLE Scores ADD CONSTRAINT FK_Scores_ReferenceRequests FOREIGN KEY (ReferenceRequestId) REFERENCES ReferenceRequests(Id) ON DELETE CASCADE;
