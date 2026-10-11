-- Rich text is now an option on long_answer questions (ValidationRules.richText) rather than
-- its own question type. Convert existing rich_text questions; their HTML answers are unchanged.
UPDATE Questions
SET Type = 'long_answer',
    ValidationRules = JSON_SET(
        COALESCE(ValidationRules, JSON_OBJECT(
            'numberOnly', false, 'minLength', NULL, 'maxLength', NULL,
            'phoneFormat', false, 'emailFormat', false)),
        '$.richText', true)
WHERE Type = 'rich_text';
