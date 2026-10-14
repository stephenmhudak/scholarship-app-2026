import { htmlToText } from './richText'

/**
 * Which application questions an applicant sees, given their answers. Mirrors QuestionVisibility and
 * AnswerConditions on the server, which drop answers to hidden questions when a draft is saved.
 *
 * A question with visibility 'conditional' is shown when any (conditionMatch 'any') or all ('all') of its
 * conditions match; a condition only counts while its source question is itself shown. A question in a conditional
 * scholarship's section also has a `gate` (that scholarship's eligibility rule, same shape) that must match. A condition matches when
 * the source answer includes optionId (choice questions) or schoolId (School Select), or contains textContains,
 * ignoring case (text questions).
 *
 * @param {Array} questions - questions from GET /cycles/:id/questions (with visibility, conditionMatch, conditions)
 * @param {(questionId: string) => string|string[]|null|undefined} answerOf - the current answer: text (or a
 *   single-choice option id), or an array of option ids
 * @returns {Set<string>} ids of the questions to show
 */
/**
 * Whether one answer meets a condition (as AnswerConditions on the server): it includes the condition's optionId or
 * schoolId, or, for text, contains textContains, ignoring case. Shared by conditional questions and references.
 */
export function answerMatches(condition, value) {
  const selected = condition.optionId ?? condition.schoolId
  if (selected) return Array.isArray(value) ? value.includes(selected) : value === selected
  if (!condition.textContains || typeof value !== 'string' || !value) return false
  // Rich text answers are HTML; match the visible text
  const text = value.includes('<') ? htmlToText(value) : value
  return text.toLowerCase().includes(condition.textContains.trim().toLowerCase())
}

export function visibleQuestionIds(questions, answerOf) {
  const byId = Object.fromEntries(questions.map((q) => [q.id, q]))
  const memo = new Map()
  const visiting = new Set()

  function matches(condition) {
    return isVisible(condition.sourceQuestionId) && answerMatches(condition, answerOf(condition.sourceQuestionId))
  }

  // any/all of these conditions match; none at all never does
  function allowed(conditions, match) {
    return conditions.length > 0 && (match === 'all' ? conditions.every(matches) : conditions.some(matches))
  }

  function isVisible(id) {
    if (memo.has(id)) return memo.get(id)
    const question = byId[id]
    if (!question) return false
    // Saving rejects cycles, but never loop on one
    if (visiting.has(id)) return false
    visiting.add(id)
    let visible = question.visibility !== 'conditional' || allowed(question.conditions ?? [], question.conditionMatch)
    // A scholarship's own questions only show while the applicant is eligible for it
    if (visible && question.gate) visible = allowed(question.gate.conditions ?? [], question.gate.match)
    visiting.delete(id)
    memo.set(id, visible)
    return visible
  }

  return new Set(questions.filter((q) => isVisible(q.id)).map((q) => q.id))
}

/** A saved answer from the API ({ textValue, selectedOptions }) in the shape visibleQuestionIds expects. */
export function answerValue(answer) {
  if (!answer) return null
  return answer.selectedOptions ?? answer.textValue ?? null
}
