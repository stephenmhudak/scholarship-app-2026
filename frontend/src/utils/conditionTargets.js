import { ref } from 'vue'
import api from '../services/api'

// What a condition (reference requirement, conditional question, scorer filter) can look at in an answer, matching
// ConditionTarget on the server: a selection (choice option, or a school for School Select), or text.

export const CHOICE_TYPES = ['single_choice', 'multiple_choice']
export const TEXT_TYPES = ['short_answer', 'long_answer']
export const CONDITION_TYPES = [...CHOICE_TYPES, 'school_select', ...TEXT_TYPES]

/** 'option', 'school' or 'text' for a question type that conditions can use, otherwise null. */
export function conditionKind(type) {
  if (CHOICE_TYPES.includes(type)) return 'option'
  if (type === 'school_select') return 'school'
  if (TEXT_TYPES.includes(type)) return 'text'
  return null
}

/** The API fields for a condition on a question of this type, with the unused ones cleared. */
export function conditionFields(type, c) {
  const kind = conditionKind(type)
  return {
    optionId: kind === 'option' ? c.optionId : null,
    schoolId: kind === 'school' ? c.schoolId : null,
    textContains: kind === 'text' ? c.textContains : null,
  }
}

/** A readable condition: '"Question" is "Answer"' / '"Question" contains "text"'. */
export function describeCondition(question, c, schoolName) {
  if (!question) return null
  if (c.optionId) return `"${question.text}" is "${question.options?.find((o) => o.id === c.optionId)?.text ?? '?'}"`
  if (c.schoolId) return `"${question.text}" is "${schoolName(c.schoolId)}"`
  return `"${question.text}" contains "${c.textContains}"`
}

// The school list for School Select conditions, loaded once per page load
const schools = ref([])
let loading = null

export function useSchools() {
  if (!loading) {
    loading = api.get('/schools')
      .then((res) => { schools.value = res.data })
      .catch(() => { loading = null })
  }
  const schoolName = (id) => schools.value.find((s) => s.id === id)?.name ?? 'Unknown school'
  return { schools, schoolName }
}
