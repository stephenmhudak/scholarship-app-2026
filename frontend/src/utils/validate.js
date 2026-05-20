const PHONE_RE = /^\+?[\d\s\-().]{7,20}$/
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

/**
 * Validate a single answer against a question's isRequired flag and validationRules.
 * Returns an error string or null.
 */
export function validateAnswer(question, value) {
  const isEmpty = value === null || value === undefined || value === '' ||
    (Array.isArray(value) && value.length === 0)

  if (question.isRequired && isEmpty) return 'This field is required.'
  if (isEmpty) return null

  const rules = question.validationRules
  if (!rules) return null

  const str = String(value)

  if (rules.numberOnly && !/^-?\d+(\.\d+)?$/.test(str.trim()))
    return 'Must be a number.'

  if (rules.minLength && str.length < rules.minLength)
    return `Must be at least ${rules.minLength} characters.`

  if (rules.maxLength && str.length > rules.maxLength)
    return `Must be no more than ${rules.maxLength} characters.`

  if (rules.phoneFormat && !PHONE_RE.test(str.trim()))
    return 'Must be a valid phone number.'

  if (rules.emailFormat && !EMAIL_RE.test(str.trim()))
    return 'Must be a valid email address.'

  return null
}

/**
 * Validate all questions in a form. Returns a map of { [questionId]: errorString }.
 * Only includes questions that have errors.
 */
export function validateForm(questions, answers) {
  const errors = {}
  for (const q of questions) {
    const err = validateAnswer(q, answers[q.id])
    if (err) errors[q.id] = err
  }
  return errors
}
