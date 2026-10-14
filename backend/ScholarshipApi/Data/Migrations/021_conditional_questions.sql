-- Application questions can be shown only when earlier answers match. 'always' (default) or 'conditional';
-- a conditional question is shown when any (or all, per ConditionMatch) of its QuestionConditions match.
ALTER TABLE Questions ADD COLUMN Visibility VARCHAR(20) NOT NULL DEFAULT 'always';
ALTER TABLE Questions ADD COLUMN ConditionMatch VARCHAR(10) NOT NULL DEFAULT 'any';

-- Matches when SourceQuestionId's answer includes OptionId (choice questions) or contains TextContains,
-- ignoring case (text questions). Removing the source question or option removes the condition.
CREATE TABLE IF NOT EXISTS QuestionConditions (
    Id CHAR(36) NOT NULL PRIMARY KEY,
    QuestionId CHAR(36) NOT NULL,
    SourceQuestionId CHAR(36) NOT NULL,
    OptionId CHAR(36) NULL,
    TextContains VARCHAR(255) NULL,
    CONSTRAINT FK_QuestionConditions_Questions FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_QuestionConditions_Sources FOREIGN KEY (SourceQuestionId) REFERENCES Questions(Id) ON DELETE CASCADE,
    CONSTRAINT FK_QuestionConditions_Options FOREIGN KEY (OptionId) REFERENCES QuestionOptions(Id) ON DELETE CASCADE
);
