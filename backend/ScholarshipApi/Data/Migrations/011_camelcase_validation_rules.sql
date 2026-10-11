-- ValidationRules were serialized with PascalCase keys (NumberOnly, ...) but the frontend
-- reads camelCase (numberOnly, ...). Rewrite existing rows to camelCase.
UPDATE Questions
SET ValidationRules = JSON_OBJECT(
    'numberOnly',  COALESCE(JSON_EXTRACT(ValidationRules, '$.NumberOnly'),  CAST('false' AS JSON)),
    'minLength',   COALESCE(JSON_EXTRACT(ValidationRules, '$.MinLength'),   CAST('null' AS JSON)),
    'maxLength',   COALESCE(JSON_EXTRACT(ValidationRules, '$.MaxLength'),   CAST('null' AS JSON)),
    'phoneFormat', COALESCE(JSON_EXTRACT(ValidationRules, '$.PhoneFormat'), CAST('false' AS JSON)),
    'emailFormat', COALESCE(JSON_EXTRACT(ValidationRules, '$.EmailFormat'), CAST('false' AS JSON))
)
WHERE ValidationRules IS NOT NULL
  AND JSON_CONTAINS_PATH(ValidationRules, 'one',
        '$.NumberOnly', '$.MinLength', '$.MaxLength', '$.PhoneFormat', '$.EmailFormat');
