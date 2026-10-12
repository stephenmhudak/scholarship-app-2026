<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import { isRichText } from '../../utils/richText'
import { useConfirm } from '../../composables/useConfirm'

const route = useRoute()
const confirm = useConfirm()
const cycleId = route.params.cycleId

const cycleName = ref('')
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

const QUESTION_TYPES = [
  { value: 'short_answer',    label: 'Short Answer' },
  { value: 'long_answer',     label: 'Long Answer' },
  { value: 'multiple_choice', label: 'Multiple Choice' },
  { value: 'single_choice',   label: 'Single Choice' },
  { value: 'file_upload',     label: 'File Upload' },
  { value: 'school_select',   label: 'School Select' },
  { value: 'date',            label: 'Date' },
]

const TEXT_TYPES = ['short_answer', 'long_answer']

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
  await Promise.all([loadCycleName(), loadQuestions(), loadSections()])
})

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
    const res = await api.get(`/cycles/${cycleId}/questions`)
    questions.value = res.data
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
  }
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
  }

  try {
    const wasNew = isNew.value
    if (isNew.value) {
      await api.post(`/cycles/${cycleId}/questions`, payload)
    } else {
      await api.put(`/cycles/${cycleId}/questions/${editing.value.id}`, payload)
    }
    editing.value = null
    isNew.value = false
    await loadQuestions()
    alert.value = { type: 'success', message: wasNew ? 'Question added.' : 'Question updated.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to save question.' }
  } finally {
    saving.value = false
  }
}

async function deleteQuestion(q) {
  if (!await confirm({ title: 'Delete question', text: `Delete "${q.text}"?`, confirmText: 'Delete', color: 'error' })) return
  try {
    await api.delete(`/cycles/${cycleId}/questions/${q.id}`).catch(async (err) => {
      const count = err.response?.status === 409 && err.response.data.answerCount
      if (!count) throw err
      if (!await confirm({
        title: 'Delete answers too?',
        text: `${count} application(s) have answered this question. Deleting it also deletes their answers.`,
        confirmText: 'Delete question and answers',
        color: 'error',
      })) return
      await api.delete(`/cycles/${cycleId}/questions/${q.id}`, { params: { force: true } })
    })
    await loadQuestions()
  } catch {
    alert.value = { type: 'error', message: 'Failed to delete question.' }
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
    await api.put(`/cycles/${cycleId}/questions/reorder`, items)
    await loadQuestions()
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

      alert.value = { type: 'success', message: 'Section added.' }
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
    await api.delete(`/cycles/${cycleId}/sections/${s.id}`)
    await Promise.all([loadSections(), loadQuestions()])
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

async function reorderSections(ordered) {
  const items = ordered.map((s, idx) => ({ id: s.id, order: idx }))
  try {
    await api.put(`/cycles/${cycleId}/sections/reorder`, items)
    await loadSections()
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
      <v-btn variant="text" color="primary" size="small" prepend-icon="mdi-arrow-left" to="/admin/settings" class="-ml-3 mb-1">
        Settings
      </v-btn>
      <h1 class="page-title">Questions</h1>
      <p class="page-subtitle">{{ cycleName }}</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Questions List -->
    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading questions…
    </div>

    <v-card v-else-if="questions.length === 0 && sections.length === 0 && !editing">
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
          <v-btn icon="mdi-pencil" variant="text" size="small" title="Edit section" @click="startEditSection(group)" />
          <v-btn icon="mdi-trash-can" variant="text" size="small" color="error" title="Delete section" @click="deleteSection(group)" />
        </div>
      </template>

      <!-- Ungrouped header (only when named sections exist) -->
      <div v-else-if="sections.length > 0" class="flex items-center gap-3 pt-1">
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

          <v-card v-else class="pa-4 flex items-start gap-3">
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
                <span v-if="q.isRequired" class="text-xs text-danger font-semibold">Required</span>
                <span v-if="q.options?.length" class="text-xs text-slate-400">
                  {{ q.options.length }} option{{ q.options.length !== 1 ? 's' : '' }}
                </span>
              </div>
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
    <template v-if="sections.length === 0 && !editing">
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

  </div>
</template>
