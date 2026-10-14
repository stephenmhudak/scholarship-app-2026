-- Conditions can also look at a School Select answer (a school id): reference requirements, conditional questions
-- and scorer filters match when the applicant chose this school. Deleting the school removes the condition.
ALTER TABLE ReferenceFormConditions ADD COLUMN SchoolId CHAR(36) NULL;
ALTER TABLE ReferenceFormConditions ADD CONSTRAINT FK_RefConditions_Schools FOREIGN KEY (SchoolId) REFERENCES Schools(Id) ON DELETE CASCADE;

ALTER TABLE QuestionConditions ADD COLUMN SchoolId CHAR(36) NULL;
ALTER TABLE QuestionConditions ADD CONSTRAINT FK_QuestionConditions_Schools FOREIGN KEY (SchoolId) REFERENCES Schools(Id) ON DELETE CASCADE;

ALTER TABLE ScorerFilters ADD COLUMN SchoolId CHAR(36) NULL;
ALTER TABLE ScorerFilters ADD CONSTRAINT FK_ScorerFilters_Schools FOREIGN KEY (SchoolId) REFERENCES Schools(Id) ON DELETE CASCADE;
