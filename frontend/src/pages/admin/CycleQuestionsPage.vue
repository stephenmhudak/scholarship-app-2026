<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import { isRichText } from '../../utils/richText'
import { useConfirm } from '../../composables/useConfirm'
import ReferenceFormsCard from '../../components/admin/ReferenceFormsCard.vue'
import ScholarshipsCard from '../../components/admin/ScholarshipsCard.vue'
import { CONDITION_TYPES, conditionKind, conditionFields, describeCondition, useSchools } from '../../utils/conditionTargets'

const route = useRoute()
const confirm = useConfirm()
const cycleId = route.params.cycleId
// Set when editing a reference form's questions (/admin/cycles/:cycleId/references/:formId): no sections there
const formId = route.params.formId ?? null
// Set when editing one scholarship's questions (/admin/cycles/:cycleId/scholarships/:scholarshipId/eligibility|details)
const scholarshipId = route.params.scholarshipId ?? null
const scholarshipPart = route.params.part ?? null
// Only some of the cycle's questions are listed (a reference form or a scholarship): no sections or show-when editing
const isSubset = !!(formId || scholarshipId)
const questionParams = formId
  ? { referenceFormId: formId }
  : scholarshipId ? { scholarshipId, part: scholarshipPart } : {}

const cycleName = ref('')
const formTitle = ref('')
const questions = ref([])
const sections = ref([])
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)

const editing = ref(null)
const isNew = ref(false)
// Section id (null = ungrouped) whose list shows the new-question form; set by startAdd.
const addingToGroupId = ref(null)
const NEW_ROW = { id: '__new' }

// editingSection: { id?, title, description } — id present = editing existing, absent = creating
// insertAfterSectionId: string id = after that section, null = before all (first position)
//   When editingSection is null, insertAfterSectionId is irrelevant.
const editingSection = ref(null)
const savingSection = ref(false)
const insertAfterSectionId = ref(null)

const ALL_QUESTION_TYPES = [
  { value: 'short_answer',    label: 'Short Answer' },
  { value: 'long_answer',     label: 'Long Answer' },
  { value: 'multiple_choice', label: 'Multiple Choice' },
  { value: 'single_choice',   label: 'Single Choice' },
  { value: 'file_upload',     label: 'File Upload' },
  { value: 'school_select',   label: 'School Select' },
  { value: 'date',            label: 'Date' },
]
// Referees don't sign in, and the school list needs an account
const QUESTION_TYPES = formId ? ALL_QUESTION_TYPES.filter((t) => t.value !== 'school_select') : ALL_QUESTION_TYPES

const TEXT_TYPES = ['short_answer', 'long_answer']
const { schools, schoolName } = useSchools()

const sectionItems = computed(() => [
  { title: 'No section (ungrouped)', value: '' },
  ...sections.value.map((s) => ({ title: s.title, value: s.id })),
])

const hasOptions = computed(() =>
  editing.value && ['multiple_choice', 'single_choice'].includes(editing.value.type)
)

const showValidationRules = computed(() =>
  editing.value && TEXT_TYPES.includes(editing.value.type)
)

const canBeRichText = computed(() => editing.value?.type === 'long_answer')

// Rich text only supports length rules — format checks make no sense on formatted HTML.
const showFormatRules = computed(() =>
  showValidationRules.value && !(canBeRichText.value && editing.value.validationRules.richText)
)

const groupedQuestions = computed(() => {
  // A reference form or one scholarship's questions: one list, whatever section they live in
  if (isSubset) return [{ id: null, title: null, description: null, questions: questions.value }]
  const orderedSections = [...sections.value].sort((a, b) => a.order - b.order)
  const groups = orderedSections.map((s) => ({
    ...s,
    questions: questions.value.filter((q) => q.sectionId === s.id),
  }))

  const ungrouped = questions.value.filter((q) => !q.sectionId)
  if (ungrouped.length > 0 || sections.value.length === 0) {
    groups.push({ id: null, title: null, description: null, questions: ungrouped })
  }

  return groups
})

onMounted(async () => {
  await Promise.all([
    loadCycleName(),
    loadQuestions(),
    formId ? loadFormTitle() : scholarshipId ? loadScholarshipTitle() : loadSections(),
  ])
})

async function loadScholarshipTitle() {
  try {
    const res = await api.get(`/cycles/${cycleId}/scholarships`)
    const name = res.data.find((s) => s.id === scholarshipId)?.name ?? 'Scholarship'
    formTitle.value = `${name}: ${scholarshipPart === 'eligibility' ? 'eligibility questions' : 'scholarship questions'}`
  } catch {
    formTitle.value = 'Scholarship questions'
  }
}

// Sections scholarships manage (eligibility, or a scholarship's own): changed from the scholarship, not here
function isManagedSection(section) {
  return !!(section?.kind || section?.scholarshipId)
}

async function loadFormTitle() {
  try {
    const res = await api.get(`/cycles/${cycleId}/reference-forms`)
    formTitle.value = res.data.find((f) => f.id === formId)?.title ?? 'Reference'
  } catch {
    formTitle.value = 'Reference'
  }
}

async function loadCycleName() {
  try {
    const res = await api.get('/admin/cycles')
    const cycle = res.data.find((c) => c.id === cycleId)
    cycleName.value = cycle?.name ?? cycleId
  } catch {
    cycleName.value = cycleId
  }
}

async function loadQuestions() {
  loading.value = true
  try {
    const res = await api.get(`/cycles/${cycleId}/questions`, { params: questionParams })
    questions.value = res.data
    const ids = new Set(res.data.map((q) => q.id))
    selectedIds.value = selectedIds.value.filter((id) => ids.has(id))
  } catch {
    alert.value = { type: 'error', message: 'Failed to load questions.' }
  } finally {
    loading.value = false
  }
}

async function loadSections() {
  try {
    const res = await api.get(`/cycles/${cycleId}/sections`)
    sections.value = res.data
  } catch {
    // non-fatal
  }
}

function blankQuestion() {
  return {
    text: '',
    description: '',
    type: 'short_answer',
    isRequired: true,
    sectionId: '',
    options: [],
    newOption: '',
    validationRules: { numberOnly: false, phoneFormat: false, emailFormat: false, minLength: null, maxLength: null, richText: false },
    visibility: 'always',
    conditionMatch: 'any',
    conditions: [],
  }
}

// ── Conditional questions (application only) ─────────────────────────────────

// Questions in the order applicants see them: sections in order, ungrouped last
const displayOrder = computed(() => groupedQuestions.value.flatMap((g) => g.questions))

// Questions that will come before this one once it's saved: everything above it, or, when it's new or moving to
// another section, everything up to the end of that section (where it lands)
const earlierQuestions = computed(() => {
  if (!editing.value) return []
  const original = questions.value.find((q) => q.id === editing.value.id)
  const sectionId = editing.value.sectionId || null
  if (original && (original.sectionId ?? null) === sectionId) {
    const idx = displayOrder.value.findIndex((q) => q.id === original.id)
    return displayOrder.value.slice(0, Math.max(idx, 0))
  }
  const groups = groupedQuestions.value
  const upTo = groups.findIndex((g) => g.id === sectionId)
  return groups.slice(0, upTo === -1 ? groups.length : upTo + 1).flatMap((g) => g.questions)
    .filter((q) => q.id !== editing.value.id)
})

// What a question can depend on: choice or text questions that come before it. Never a later one (the API refuses
// those, and drops existing ones when questions are reordered).
const conditionSources = computed(() => {
  if (isSubset) return []
  const earlier = new Set(earlierQuestions.value.map((q) => q.id))
  return questions.value.filter((q) => earlier.has(q.id) && CONDITION_TYPES.includes(q.type))
})

// Conditions in the editor that would point at a later question (e.g. after changing the section above)
const laterConditionCount = computed(() => {
  const allowed = new Set(conditionSources.value.map((q) => q.id))
  return (editing.value?.conditions ?? []).filter((c) => c.sourceQuestionId && !allowed.has(c.sourceQuestionId)).length
})

const questionById = computed(() => Object.fromEntries(questions.value.map((q) => [q.id, q])))

// 'option', 'school' or 'text': what a condition on this question looks for
function sourceKind(sourceQuestionId) {
  return conditionKind(questionById.value[sourceQuestionId]?.type)
}

function addCondition() {
  editing.value.conditions.push({ sourceQuestionId: null, optionId: null, schoolId: null, textContains: '' })
}

function conditionText(c) {
  const source = questionById.value[c.sourceQuestionId]
  if (!source) return null
  return describeCondition(source, c, schoolName)
}

function visibilityText(q) {
  const parts = (q.conditions ?? []).map(conditionText).filter(Boolean)
  if (!parts.length) return 'Hidden: the answers it depended on were removed'
  return `Shown when ${parts.join(q.conditionMatch === 'all' ? ' and ' : ' or ')}`
}

// A group's questions, plus a placeholder row for the form when adding a question to it.
function groupRows(group) {
  return isNew.value && addingToGroupId.value === group.id
    ? [...group.questions, NEW_ROW]
    : group.questions
}

function isEditingRow(q) {
  if (!editing.value) return false
  return isNew.value ? q === NEW_ROW : q.id === editing.value.id
}

function startAdd(sectionId = '') {
  isNew.value = true
  addingToGroupId.value = sectionId || null
  editing.value = { ...blankQuestion(), sectionId }
}

function startEdit(q) {
  isNew.value = false
  const vr = q.validationRules ?? {}
  editing.value = {
    id: q.id,
    text: q.text,
    description: q.description ?? '',
    type: q.type,
    isRequired: q.isRequired,
    sectionId: q.sectionId ?? '',
    order: q.order ?? 0,
    options: q.options?.map((o) => o.text) ?? [],
    newOption: '',
    validationRules: {
      numberOnly: vr.numberOnly ?? false,
      phoneFormat: vr.phoneFormat ?? false,
      emailFormat: vr.emailFormat ?? false,
      minLength: vr.minLength ?? null,
      maxLength: vr.maxLength ?? null,
      richText: vr.richText ?? false,
    },
    visibility: q.visibility ?? 'always',
    conditionMatch: q.conditionMatch ?? 'any',
    conditions: (q.conditions ?? []).map((c) => ({ ...c, textContains: c.textContains ?? '' })),
  }
}

function cancel() {
  editing.value = null
  isNew.value = false
}

function addOption() {
  const opt = editing.value.newOption.trim()
  if (!opt) return
  editing.value.options.push(opt)
  editing.value.newOption = ''
}

function removeOption(i) {
  editing.value.options.splice(i, 1)
}

// Reordering, moving or deleting questions makes the API drop conditions whose question no longer comes first
// (QuestionOrder.PruneConditionsAsync). Tell the admin which questions that changed; returns true if any.
function quoteList(texts) {
  const quoted = texts.slice(0, 3).map((t) => `"${t.length > 50 ? t.slice(0, 47) + '…' : t}"`)
  return texts.length > 3 ? `${quoted.join(', ')} and ${texts.length - 3} more` : quoted.join(', ')
}

function reportConditionChanges(result) {
  const removed = result?.conditionsRemovedFrom ?? []
  const nowShown = result?.nowAlwaysShown ?? []
  const parts = []
  if (removed.length) parts.push(`Removed conditions from ${quoteList(removed)}, because the question they depended on no longer comes before ${removed.length === 1 ? 'it' : 'them'}.`)
  if (nowShown.length) parts.push(`${quoteList(nowShown)} ${nowShown.length === 1 ? 'has' : 'have'} no conditions left, so ${nowShown.length === 1 ? 'it is' : 'they are'} now shown to every applicant.`)
  if (!parts.length) return false
  alert.value = { type: 'warning', message: parts.join(' ') }
  return true
}

async function save() {
  if (!editing.value.text.trim()) {
    alert.value = { type: 'error', message: 'Question text is required.' }
    return
  }

  saving.value = true
  alert.value = null

  const vr = editing.value.validationRules
  const isTextType = TEXT_TYPES.includes(editing.value.type)
  const richText = (canBeRichText.value && vr.richText) || false
  const allowsFormat = showFormatRules.value
  const hasRules = isTextType && ((allowsFormat && (vr.numberOnly || vr.phoneFormat || vr.emailFormat)) || vr.minLength || vr.maxLength || richText)
  const validationRules = hasRules
    ? {
        numberOnly: (allowsFormat && vr.numberOnly) || false,
        phoneFormat: (allowsFormat && vr.phoneFormat) || false,
        emailFormat: (allowsFormat && vr.emailFormat) || false,
        minLength: vr.minLength ? Number(vr.minLength) : null,
        maxLength: vr.maxLength ? Number(vr.maxLength) : null,
        richText,
      }
    : null

  const payload = {
    text: editing.value.text,
    description: editing.value.description || null,
    type: editing.value.type,
    isRequired: editing.value.isRequired,
    order: isNew.value ? questions.value.length : editing.value.order,
    sectionId: editing.value.sectionId || null,
    options: hasOptions.value ? editing.value.options : [],
    validationRules,
    visibility: formId ? 'always' : editing.value.visibility,
    conditionMatch: editing.value.conditionMatch,
    // Showing only a scholarship's questions, the source questions aren't loaded: send its conditions back as saved
    conditions: !formId && editing.value.visibility === 'conditional'
      ? editing.value.conditions.map((c) => ({
          sourceQuestionId: c.sourceQuestionId,
          ...(questionById.value[c.sourceQuestionId]
            ? conditionFields(questionById.value[c.sourceQuestionId].type, c)
            : { optionId: c.optionId ?? null, schoolId: c.schoolId ?? null, textContains: c.textContains || null }),
        }))
      : [],
  }

  try {
    const wasNew = isNew.value
    let result = null
    if (isNew.value) {
      await api.post(`/cycles/${cycleId}/questions`, payload, { params: questionParams })
    } else {
      result = (await api.put(`/cycles/${cycleId}/questions/${editing.value.id}`, payload)).data
    }
    editing.value = null
    isNew.value = false
    await loadQuestions()
    alert.value = { type: 'success', message: wasNew ? 'Question added.' : 'Question updated.' }
    reportConditionChanges(result)
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to save question.' }
  } finally {
    saving.value = false
  }
}

// What else goes when questions are deleted, from the API's 409 (see QuestionsController.DeleteQuestionsAsync)
function deleteConsequences({ answerCount, conditionCount, dependentCount }, it) {
  const consequences = []
  if (answerCount) consequences.push(`deletes ${answerCount} answer(s) to ${it}`)
  if (conditionCount) consequences.push(`removes ${conditionCount} reference condition(s) that use ${it}`)
  if (dependentCount) consequences.push(`removes ${dependentCount} condition(s) that show other questions (any left with no conditions will be shown to everyone)`)
  return consequences.join(' and ')
}

async function deleteQuestion(q) {
  if (!await confirm({ title: 'Delete question', text: `Delete "${q.text}"?`, confirmText: 'Delete', color: 'error' })) return
  try {
    const res = await api.delete(`/cycles/${cycleId}/questions/${q.id}`).catch(async (err) => {
      if (err.response?.status !== 409) throw err
      if (!await confirm({
        title: 'Delete question',
        text: `Deleting this question also ${deleteConsequences(err.response.data, 'it')}.`,
        confirmText: 'Delete',
        color: 'error',
      })) return null
      return api.delete(`/cycles/${cycleId}/questions/${q.id}`, { params: { force: true } })
    })
    await loadQuestions()
    reportConditionChanges(res?.data)
  } catch {
    alert.value = { type: 'error', message: 'Failed to delete question.' }
  }
}

// ── Selecting several questions to delete or move between sections ──────────

const selectedIds = ref([])
const bulkWorking = ref(false)
const selectedSet = computed(() => new Set(selectedIds.value))
const allSelected = computed(() => questions.value.length > 0 && questions.value.every((q) => selectedSet.value.has(q.id)))

// Sections in form order, then ungrouped
const moveTargets = computed(() => [
  ...[...sections.value].sort((a, b) => a.order - b.order).map((s) => ({ title: s.title, value: s.id })),
  { title: 'Ungrouped', value: null },
])

function setSelected(ids, on) {
  const set = new Set(selectedIds.value)
  for (const id of ids) on ? set.add(id) : set.delete(id)
  selectedIds.value = [...set]
}

// 'all', 'some' or 'none' of a section's questions are selected
function groupSelection(group) {
  const count = group.questions.filter((q) => selectedSet.value.has(q.id)).length
  return count === 0 ? 'none' : count === group.questions.length ? 'all' : 'some'
}

async function moveSelected(sectionId) {
  const count = selectedIds.value.length
  bulkWorking.value = true
  alert.value = null
  try {
    const { data } = await api.put(`/cycles/${cycleId}/questions/move`, { questionIds: selectedIds.value, sectionId })
    selectedIds.value = []
    await loadQuestions()
    const target = moveTargets.value.find((t) => t.value === sectionId)?.title ?? 'the section'
    alert.value = { type: 'success', message: `Moved ${count} question${count === 1 ? '' : 's'} to ${target}.` }
    reportConditionChanges(data)
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to move questions.' }
  } finally {
    bulkWorking.value = false
  }
}

async function deleteSelected() {
  const count = selectedIds.value.length
  const them = count === 1 ? 'it' : 'them'
  if (!await confirm({
    title: 'Delete questions',
    text: `Delete ${count} question${count === 1 ? '' : 's'}?`,
    confirmText: 'Delete',
    color: 'error',
  })) return
  bulkWorking.value = true
  alert.value = null
  const url = `/cycles/${cycleId}/questions/bulk-delete`
  try {
    let deleted = true
    const res = await api.post(url, { questionIds: selectedIds.value }).catch(async (err) => {
      if (err.response?.status !== 409) throw err
      deleted = await confirm({
        title: 'Delete questions',
        text: `Deleting ${count === 1 ? 'this question' : 'these questions'} also ${deleteConsequences(err.response.data, them)}.`,
        confirmText: 'Delete',
        color: 'error',
      })
      return deleted ? api.post(url, { questionIds: selectedIds.value }, { params: { force: true } }) : null
    })
    if (!deleted) return
    selectedIds.value = []
    await loadQuestions()
    alert.value = { type: 'success', message: `Deleted ${count} question${count === 1 ? '' : 's'}.` }
    reportConditionChanges(res?.data)
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to delete questions.' }
  } finally {
    bulkWorking.value = false
  }
}

async function moveUp(i) {
  if (i === 0) return
  const updated = [...questions.value]
  ;[updated[i - 1], updated[i]] = [updated[i], updated[i - 1]]
  await reorder(updated)
}

async function moveDown(i) {
  if (i === questions.value.length - 1) return
  const updated = [...questions.value]
  ;[updated[i], updated[i + 1]] = [updated[i + 1], updated[i]]
  await reorder(updated)
}

async function reorder(ordered) {
  const items = ordered.map((q, idx) => ({ id: q.id, order: idx }))
  try {
    const { data } = await api.put(`/cycles/${cycleId}/questions/reorder`, items)
    await loadQuestions()
    reportConditionChanges(data)
  } catch {
    alert.value = { type: 'error', message: 'Failed to reorder questions.' }
  }
}

// Section management

function startAddSectionAfter(sectionId) {
  insertAfterSectionId.value = sectionId
  editingSection.value = { title: '', description: '' }
}

function startEditSection(s) {
  insertAfterSectionId.value = null
  editingSection.value = { id: s.id, title: s.title, description: s.description ?? '' }
}

function cancelSection() {
  editingSection.value = null
}

async function saveSection() {
  if (!editingSection.value.title.trim()) {
    alert.value = { type: 'error', message: 'Section title is required.' }
    return
  }

  savingSection.value = true
  alert.value = null

  const payload = {
    title: editingSection.value.title,
    description: editingSection.value.description || null,
  }

  try {
    if (editingSection.value.id) {
      await api.put(`/cycles/${cycleId}/sections/${editingSection.value.id}`, payload)
      await loadSections()
      alert.value = { type: 'success', message: 'Section updated.' }
    } else {
      const res = await api.post(`/cycles/${cycleId}/sections`, payload)
      const newId = res.data.id
      await loadSections()

      // Place the new section immediately after insertAfterSectionId
      const all = [...sections.value]
      const newIdx = all.findIndex((s) => s.id === newId)
      if (newIdx !== -1) {
        const [newSection] = all.splice(newIdx, 1)
        if (insertAfterSectionId.value === null) {
          all.unshift(newSection)
        } else {
          const insertIdx = all.findIndex((s) => s.id === insertAfterSectionId.value) + 1
          all.splice(insertIdx, 0, newSection)
        }
        await reorderSections(all)
      }

      if (!sectionReorderWarned) alert.value = { type: 'success', message: 'Section added.' }
    }

    editingSection.value = null
  } catch {
    alert.value = { type: 'error', message: 'Failed to save section.' }
  } finally {
    savingSection.value = false
  }
}

async function deleteSection(s) {
  if (!await confirm({
    title: 'Delete section',
    text: `Delete section "${s.title}"? Questions in this section will become ungrouped.`,
    confirmText: 'Delete',
    color: 'error',
  })) return
  try {
    const { data } = await api.delete(`/cycles/${cycleId}/sections/${s.id}`)
    await Promise.all([loadSections(), loadQuestions()])
    reportConditionChanges(data)
  } catch {
    alert.value = { type: 'error', message: 'Failed to delete section.' }
  }
}

async function moveSectionUp(s) {
  const idx = sections.value.findIndex((x) => x.id === s.id)
  if (idx <= 0) return
  const updated = [...sections.value]
  ;[updated[idx - 1], updated[idx]] = [updated[idx], updated[idx - 1]]
  await reorderSections(updated)
}

async function moveSectionDown(s) {
  const idx = sections.value.findIndex((x) => x.id === s.id)
  if (idx === -1 || idx === sections.value.length - 1) return
  const updated = [...sections.value]
  ;[updated[idx], updated[idx + 1]] = [updated[idx + 1], updated[idx]]
  await reorderSections(updated)
}

// Set when the last section reorder reported condition changes, so saveSection doesn't cover it with "Section added."
let sectionReorderWarned = false

async function reorderSections(ordered) {
  sectionReorderWarned = false
  const items = ordered.map((s, idx) => ({ id: s.id, order: idx }))
  try {
    const { data } = await api.put(`/cycles/${cycleId}/sections/reorder`, items)
    // Questions too: reordering sections can remove conditions (and change which questions are conditional)
    await Promise.all([loadSections(), loadQuestions()])
    if (reportConditionChanges(data)) sectionReorderWarned = true
  } catch {
    alert.value = { type: 'error', message: 'Failed to reorder sections.' }
  }
}

function sectionIndex(sectionId) {
  return sections.value.findIndex((s) => s.id === sectionId)
}

function typLabel(type) {
  return QUESTION_TYPES.find((t) => t.value === type)?.label ?? type
}

function globalIndex(q) {
  return questions.value.findIndex((x) => x.id === q.id)
}
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <!-- Header -->
    <div>
      <v-btn v-if="isSubset" variant="text" color="primary" size="small" prepend-icon="mdi-arrow-left" :to="`/admin/cycles/${cycleId}/questions`" class="-ml-3 mb-1">
        Application questions
      </v-btn>
      <v-btn v-else variant="text" color="primary" size="small" prepend-icon="mdi-arrow-left" to="/admin/scholarship-settings" class="-ml-3 mb-1">
        Application Periods
      </v-btn>
      <h1 class="page-title">{{ isSubset ? formTitle : 'Questions' }}</h1>
      <p class="page-subtitle">{{ formId ? `Reference questions · ${cycleName}` : cycleName }}</p>
    </div>

    <p v-if="scholarshipPart === 'eligibility'" class="text-sm text-slate-400">
      Every applicant answers these in the "Scholarship Eligibility" section. Use them in the scholarship's
      "Who's eligible" rule to decide who can see and complete its scholarship questions.
    </p>
    <p v-else-if="scholarshipPart === 'details'" class="text-sm text-slate-400">
      Only applicants who are eligible for this scholarship see these, in a section named after it. Required ones only
      apply to those applicants.
    </p>

    <p v-if="formId" class="text-sm text-slate-400">
      The Reference Provider sees the applicant's name and school above these questions, and finishes by giving their name,
      drawing their signature, and the date. You don't need questions for those.
    </p>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Selection toolbar -->
    <v-card v-if="!loading && questions.length && !editing" class="px-4 py-2 flex items-center gap-3 flex-wrap">
      <v-checkbox-btn
        :model-value="allSelected"
        :indeterminate="selectedIds.length > 0 && !allSelected"
        title="Select all questions"
        class="flex-none"
        @update:model-value="(on) => setSelected(questions.map((q) => q.id), on)"
      />
      <span class="text-sm font-semibold text-navy">
        {{ selectedIds.length ? `${selectedIds.length} selected` : 'Select questions to move or delete' }}
      </span>
      <div class="flex-1" />
      <template v-if="selectedIds.length">
        <v-menu v-if="!isSubset && sections.length">
          <template #activator="{ props }">
            <v-btn v-bind="props" variant="outlined" prepend-icon="mdi-folder-move-outline" :loading="bulkWorking">
              Move to section
            </v-btn>
          </template>
          <v-list density="compact">
            <v-list-item v-for="t in moveTargets" :key="t.value ?? '__ungrouped'" :title="t.title" @click="moveSelected(t.value)" />
          </v-list>
        </v-menu>
        <v-btn color="error" variant="tonal" prepend-icon="mdi-trash-can" :loading="bulkWorking" @click="deleteSelected">Delete</v-btn>
        <v-btn variant="text" @click="selectedIds = []">Clear</v-btn>
      </template>
    </v-card>

    <!-- Questions List -->
    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading questions…
    </div>

    <v-card v-else-if="questions.length === 0 && sections.length === 0 && !editing && !isSubset">
      <v-empty-state icon="mdi-comment-question-outline" text="No questions yet." />
    </v-card>

    <template v-else v-for="group in groupedQuestions" :key="group.id ?? '__ungrouped'">

      <!-- Named section header -->
      <template v-if="group.id !== null">
        <!-- Inline edit form (replaces header) -->
        <div v-if="editingSection?.id === group.id" class="flex items-center gap-2 pt-1">
          <v-text-field
            v-model="editingSection.title"
            placeholder="Section title…"
            density="compact"
            hide-details
            class="flex-1"
            @keydown.enter.prevent="saveSection"
            @keydown.escape.prevent="cancelSection"
          />
          <v-text-field
            v-model="editingSection.description"
            placeholder="Description…"
            density="compact"
            hide-details
            class="flex-1"
            @keydown.enter.prevent="saveSection"
            @keydown.escape.prevent="cancelSection"
          />
          <v-btn color="primary" size="small" :loading="savingSection" @click="saveSection">Save</v-btn>
          <v-btn variant="outlined" size="small" @click="cancelSection">Cancel</v-btn>
        </div>

        <!-- Normal section header -->
        <div v-else class="flex items-center gap-2 pt-1">
          <v-checkbox-btn
            :model-value="groupSelection(group) === 'all'"
            :indeterminate="groupSelection(group) === 'some'"
            :disabled="!group.questions.length || !!editing"
            :title="`Select all questions in ${group.title}`"
            class="flex-none"
            @update:model-value="(on) => setSelected(group.questions.map((q) => q.id), on)"
          />
          <div class="flex flex-col">
            <v-btn
              icon="mdi-chevron-up"
              variant="text"
              size="x-small"
              density="compact"
              title="Move section up"
              :disabled="sectionIndex(group.id) === 0"
              @click="moveSectionUp(group)"
            />
            <v-btn
              icon="mdi-chevron-down"
              variant="text"
              size="x-small"
              density="compact"
              title="Move section down"
              :disabled="sectionIndex(group.id) === sections.length - 1"
              @click="moveSectionDown(group)"
            />
          </div>
          <div class="min-w-0">
            <h3 class="text-sm font-bold text-navy leading-tight">{{ group.title }}</h3>
            <p v-if="group.description" class="text-xs text-slate-400 truncate">{{ group.description }}</p>
          </div>
          <v-divider class="mx-1 flex-1" />
          <v-chip v-if="isManagedSection(group)" color="primary" prepend-icon="mdi-school-outline" title="Managed from the Scholarships card below">
            Scholarship
          </v-chip>
          <template v-else>
            <v-btn icon="mdi-pencil" variant="text" size="small" title="Edit section" @click="startEditSection(group)" />
            <v-btn icon="mdi-trash-can" variant="text" size="small" color="error" title="Delete section" @click="deleteSection(group)" />
          </template>
        </div>
      </template>

      <!-- Ungrouped header (only when named sections exist) -->
      <div v-else-if="sections.length > 0" class="flex items-center gap-3 pt-1">
        <v-checkbox-btn
          :model-value="groupSelection(group) === 'all'"
          :indeterminate="groupSelection(group) === 'some'"
          :disabled="!group.questions.length || !!editing"
          title="Select all ungrouped questions"
          class="flex-none"
          @update:model-value="(on) => setSelected(group.questions.map((q) => q.id), on)"
        />
        <h3 class="text-sm font-bold text-slate-400 shrink-0">Ungrouped</h3>
        <div class="flex-1 border-t border-dashed border-[#E9EDF7]"></div>
      </div>

      <!-- Questions + add question button -->
      <div class="space-y-2">
        <div v-if="groupRows(group).length === 0 && group.id !== null" class="text-xs text-slate-400 pl-1">
          No questions in this section.
        </div>

        <template v-for="q in groupRows(group)" :key="q.id">
          <!-- Add / Edit Question Form (inline, in place of the question) -->
          <v-card v-if="isEditingRow(q)" class="border-primary/30" :title="isNew ? 'New Question' : 'Edit Question'">
            <v-card-text class="pa-6 space-y-5">
              <v-textarea
                v-model="editing.text"
                label="Question Text *"
                rows="2"
                placeholder="Enter your question…"
                hide-details="auto"
              />

              <v-textarea
                v-model="editing.description"
                label="Additional Information"
                rows="2"
                placeholder="Optional help text shown below the question…"
                hide-details="auto"
              />

              <!-- Type + Required -->
              <div class="flex items-center gap-4">
                <v-select v-model="editing.type" :items="QUESTION_TYPES" item-title="label" item-value="value" label="Type" hide-details class="flex-1" />
                <v-checkbox v-model="editing.isRequired" label="Required" hide-details class="flex-none" />
              </div>

              <!-- Section assignment -->
              <v-select v-if="sections.length > 0" v-model="editing.sectionId" :items="sectionItems" label="Section" hide-details />

              <!-- Options (MC / SC only) -->
              <div v-if="hasOptions" class="space-y-3">
                <p class="text-sm font-semibold text-navy">Answer Options</p>

                <div v-if="editing.options.length" class="flex flex-wrap gap-2">
                  <v-chip
                    v-for="(opt, i) in editing.options"
                    :key="i"
                    size="default"
                    closable
                    @click:close="removeOption(i)"
                  >
                    {{ opt }}
                  </v-chip>
                </div>

                <div class="flex items-center gap-2">
                  <v-text-field
                    v-model="editing.newOption"
                    placeholder="Add an option…"
                    density="compact"
                    hide-details
                    class="flex-1"
                    @keydown.enter.prevent="addOption"
                  />
                  <v-btn variant="outlined" prepend-icon="mdi-plus" @click="addOption">Add</v-btn>
                </div>

                <p v-if="editing.options.length === 0" class="text-xs font-medium text-warning-dark">
                  Add at least one option for this question type.
                </p>
              </div>

              <!-- When the question is shown (application questions only) -->
              <template v-if="!isSubset">
                <v-divider />
                <div class="space-y-3">
                  <p class="text-sm font-bold text-navy">Show this question</p>
                  <v-radio-group v-model="editing.visibility" hide-details>
                    <v-radio label="Always" value="always" />
                    <v-radio label="Only when earlier answers match" value="conditional" />
                  </v-radio-group>

                  <template v-if="editing.visibility === 'conditional'">
                    <v-select
                      v-model="editing.conditionMatch"
                      :items="[{ title: 'Any of these match', value: 'any' }, { title: 'All of these match', value: 'all' }]"
                      label="Show when"
                      hide-details
                      class="max-w-[16rem]"
                    />
                    <div v-for="(c, i) in editing.conditions" :key="i" class="flex items-start gap-2">
                      <v-select
                        v-model="c.sourceQuestionId"
                        :items="conditionSources"
                        item-title="text"
                        item-value="id"
                        label="Question"
                        hide-details
                        class="flex-1 min-w-0"
                        @update:model-value="c.optionId = null; c.schoolId = null; c.textContains = ''"
                      />
                      <v-select
                        v-if="c.sourceQuestionId && sourceKind(c.sourceQuestionId) === 'option'"
                        v-model="c.optionId"
                        :items="questionById[c.sourceQuestionId]?.options ?? []"
                        item-title="text"
                        item-value="id"
                        label="Answer is"
                        hide-details
                        class="flex-1 min-w-0"
                      />
                      <v-autocomplete
                        v-else-if="c.sourceQuestionId && sourceKind(c.sourceQuestionId) === 'school'"
                        v-model="c.schoolId"
                        :items="schools"
                        item-title="name"
                        item-value="id"
                        label="School is"
                        hide-details
                        class="flex-1 min-w-0"
                      />
                      <v-text-field
                        v-else-if="c.sourceQuestionId"
                        v-model="c.textContains"
                        label="Answer contains"
                        hide-details
                        class="flex-1 min-w-0"
                      />
                      <v-btn icon="mdi-close" variant="text" size="small" class="mt-2" title="Remove" @click="editing.conditions.splice(i, 1)" />
                    </div>
                    <p v-if="laterConditionCount" class="text-xs font-medium text-danger-dark">
                      {{ laterConditionCount }} condition{{ laterConditionCount === 1 ? ' uses a question' : 's use questions' }}
                      that would come after this one. Remove {{ laterConditionCount === 1 ? 'it' : 'them' }} or keep this question in its section.
                    </p>
                    <p v-if="conditionSources.length === 0" class="text-xs font-medium text-warning-dark">
                      There are no earlier choice, school or text questions to depend on.
                    </p>
                    <v-btn v-else variant="outlined" size="small" prepend-icon="mdi-plus" @click="addCondition">Add condition</v-btn>
                    <p class="text-xs text-slate-400">
                      Applicants won't see this question, and it isn't required, until the answers match. If they change
                      those answers later, it's hidden again and its answer is discarded.
                    </p>
                  </template>
                </div>
              </template>

              <!-- Validation rules -->
              <template v-if="showValidationRules">
                <v-divider />
                <div class="space-y-4">
                  <p class="text-sm font-bold text-navy">Validation Rules</p>

                  <v-checkbox
                    v-if="canBeRichText"
                    v-model="editing.validationRules.richText"
                    label="Rich text"
                    hint="Applicants can use bold, lists, headings and links"
                    persistent-hint
                  />

                  <div v-if="showFormatRules" class="grid grid-cols-3 gap-3">
                    <v-checkbox v-model="editing.validationRules.numberOnly" label="Numbers only" />
                    <v-checkbox v-model="editing.validationRules.phoneFormat" label="Phone format" />
                    <v-checkbox v-model="editing.validationRules.emailFormat" label="Email format" />
                  </div>

                  <div class="flex gap-4">
                    <v-number-input v-model="editing.validationRules.minLength" label="Min Length" :min="0" control-variant="hidden" hide-details class="flex-1" />
                    <v-number-input v-model="editing.validationRules.maxLength" label="Max Length" :min="0" control-variant="hidden" hide-details class="flex-1" />
                  </div>
                </div>
              </template>

              <!-- Actions -->
              <v-divider />
              <div class="flex gap-3">
                <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="save">
                  {{ isNew ? 'Add Question' : 'Save Changes' }}
                </v-btn>
                <v-btn variant="outlined" @click="cancel">Cancel</v-btn>
              </div>
            </v-card-text>
          </v-card>

          <v-card v-else class="pa-4 flex items-start gap-3" :class="{ 'border-primary': selectedSet.has(q.id) }">
            <v-checkbox-btn
              :model-value="selectedSet.has(q.id)"
              :disabled="!!editing"
              title="Select question"
              class="flex-none"
              @update:model-value="(on) => setSelected([q.id], on)"
            />
            <!-- Order controls -->
            <div class="flex flex-col">
              <v-btn
                icon="mdi-chevron-up"
                variant="text"
                size="small"
                density="compact"
                title="Move up"
                :disabled="globalIndex(q) === 0"
                @click="moveUp(globalIndex(q))"
              />
              <v-btn
                icon="mdi-chevron-down"
                variant="text"
                size="small"
                density="compact"
                title="Move down"
                :disabled="globalIndex(q) === questions.length - 1"
                @click="moveDown(globalIndex(q))"
              />
            </div>

            <!-- Content -->
            <div class="flex-1 min-w-0">
              <div class="flex items-start justify-between gap-2">
                <p class="text-sm font-semibold text-navy leading-snug">{{ q.text }}</p>
                <div class="flex shrink-0">
                  <v-btn icon="mdi-pencil" variant="text" size="small" title="Edit" @click="startEdit(q)" />
                  <v-btn icon="mdi-trash-can" variant="text" size="small" color="error" title="Delete" @click="deleteQuestion(q)" />
                </div>
              </div>
              <div class="flex items-center gap-2 mt-1.5 flex-wrap">
                <v-chip color="primary" class="font-semibold">{{ typLabel(q.type) }}</v-chip>
                <v-chip v-if="isRichText(q)" color="primary" class="font-semibold">Rich Text</v-chip>
                <v-chip v-if="q.visibility === 'conditional'" color="warning" class="font-semibold">Conditional</v-chip>
                <span v-if="q.isRequired" class="text-xs text-danger font-semibold">Required</span>
                <span v-if="q.options?.length" class="text-xs text-slate-400">
                  {{ q.options.length }} option{{ q.options.length !== 1 ? 's' : '' }}
                </span>
              </div>
              <p v-if="q.visibility === 'conditional'" class="text-xs text-navy mt-1.5">{{ visibilityText(q) }}</p>
              <div v-if="q.options?.length" class="mt-2 flex flex-wrap gap-1.5">
                <v-chip v-for="opt in q.options" :key="opt.id" variant="outlined" rounded="lg">
                  {{ opt.text }}
                </v-chip>
              </div>
            </div>

            <span class="text-xs font-mono text-slate-400/60 pt-1.5">{{ globalIndex(q) + 1 }}</span>
          </v-card>
        </template>

        <!-- Add question to this group -->
        <v-btn
          v-if="!editing"
          block
          variant="outlined"
          size="small"
          prepend-icon="mdi-plus"
          class="border-dashed text-slate-400"
          @click="startAdd(group.id ?? '')"
        >
          Add question{{ group.title ? ` to ${group.title}` : '' }}
        </v-btn>
      </div>

      <!-- Add section after this named section -->
      <template v-if="group.id !== null && !editing">
        <!-- Inline create form -->
        <v-sheet
          v-if="editingSection && !editingSection.id && insertAfterSectionId === group.id"
          rounded="lg"
          class="bg-primary/5 border border-dashed border-primary/30 pa-4 space-y-3"
        >
          <p class="text-xs font-semibold text-primary">New section after "{{ group.title }}"</p>
          <v-text-field v-model="editingSection.title" placeholder="Section title…" density="compact" hide-details
            @keydown.enter.prevent="saveSection" @keydown.escape.prevent="cancelSection" />
          <v-text-field v-model="editingSection.description" placeholder="Description (optional)…" density="compact" hide-details
            @keydown.enter.prevent="saveSection" @keydown.escape.prevent="cancelSection" />
          <div class="flex gap-2">
            <v-btn color="primary" :loading="savingSection" @click="saveSection">Add Section</v-btn>
            <v-btn variant="outlined" @click="cancelSection">Cancel</v-btn>
          </div>
        </v-sheet>

        <!-- Add section button -->
        <v-btn
          v-else-if="!editingSection"
          block
          variant="text"
          size="small"
          prepend-icon="mdi-view-agenda-outline"
          class="border border-dashed text-slate-400"
          @click="startAddSectionAfter(group.id)"
        >
          Add section after {{ group.title }}
        </v-btn>
      </template>

    </template>

    <!-- Add first section (when no sections exist) -->
    <template v-if="sections.length === 0 && !editing && !isSubset">
      <v-sheet
        v-if="editingSection && !editingSection.id"
        rounded="lg"
        class="bg-primary/5 border border-dashed border-primary/30 pa-4 space-y-3"
      >
        <p class="text-xs font-semibold text-primary">New section</p>
        <v-text-field v-model="editingSection.title" placeholder="Section title…" density="compact" hide-details
          @keydown.enter.prevent="saveSection" @keydown.escape.prevent="cancelSection" />
        <v-text-field v-model="editingSection.description" placeholder="Description (optional)…" density="compact" hide-details
          @keydown.enter.prevent="saveSection" @keydown.escape.prevent="cancelSection" />
        <div class="flex gap-2">
          <v-btn color="primary" :loading="savingSection" @click="saveSection">Add Section</v-btn>
          <v-btn variant="outlined" @click="cancelSection">Cancel</v-btn>
        </div>
      </v-sheet>

      <v-btn
        v-else-if="!editingSection"
        block
        variant="text"
        size="small"
        prepend-icon="mdi-view-agenda-outline"
        class="border border-dashed text-slate-400"
        @click="startAddSectionAfter(null)"
      >
        Add section
      </v-btn>
    </template>

    <ReferenceFormsCard v-if="!isSubset && !loading" :cycleId="cycleId" :questions="questions" />
    <ScholarshipsCard
      v-if="!isSubset && !loading"
      :cycleId="cycleId"
      :questions="questions"
      :sections="sections"
      @changed="loadSections(); loadQuestions()"
    />

  </div>
</template>
