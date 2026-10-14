import { htmlToText } from './richText'

/**
 * Which application questions an applicant sees, given their answers. Mirrors QuestionVisibility and
 * AnswerConditions on the server, which drop answers to hidden questions when a draft is saved.
 *
 * A question with visibility 'conditional' is shown when any (conditionMatch 'any') or all ('all') of its
 * conditions match; a condition only counts while its source question is itself shown. A condition matches when
 * the source answer includes optionId (choice questions) or contains textContains, ignoring case (text questions).
 *
 * @param {Array} questions - questions from GET /cycles/:id/questions (with visibility, conditionMatch, conditions)
 * @param {(questionId: string) => string|string[]|null|undefined} answerOf - the current answer: text (or a
 *   single-choice option id), or an array of option ids
 * @returns {Set<string>} ids of the questions to show
 */
export function visibleQuestionIds(questions, answerOf) {
  const byId = Object.fromEntries(questions.map((q) => [q.id, q]))
  const memo = new Map()
  const visiting = new Set()

  function matches(condition) {
    if (!isVisible(condition.sourceQuestionId)) return false
    const value = answerOf(condition.sourceQuestionId)
    if (condition.optionId) return Array.isArray(value) ? value.includes(condition.optionId) : value === condition.optionId
    if (!condition.textContains || typeof value !== 'string' || !value) return false
    // Rich text answers are HTML; match the visible text
    const text = value.includes('<') ? htmlToText(value) : value
    return text.toLowerCase().includes(condition.textContains.trim().toLowerCase())
  }

  function isVisible(id) {
    if (memo.has(id)) return memo.get(id)
    const question = byId[id]
    if (!question) return false
    if (question.visibility !== 'conditional') {
      memo.set(id, true)
      return true
    }
    // Saving rejects cycles, but never loop on one
    if (visiting.has(id)) return false
    visiting.add(id)
    const conditions = question.conditions ?? []
    // A conditional question whose conditions were all removed stays hidden
    const visible = conditions.length > 0 &&
      (question.conditionMatch === 'all' ? conditions.every(matches) : conditions.some(matches))
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
