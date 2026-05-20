<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const route = useRoute()
const cycleId = route.params.cycleId

const cycleName = ref('')
const questions = ref([])
const sections = ref([])
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)

const editing = ref(null)
const isNew = ref(false)

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

const hasOptions = computed(() =>
  editing.value && ['multiple_choice', 'single_choice'].includes(editing.value.type)
)

const showValidationRules = computed(() =>
  editing.value && TEXT_TYPES.includes(editing.value.type)
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
    type: 'short_answer',
    isRequired: true,
    sectionId: '',
    options: [],
    newOption: '',
    validationRules: { numberOnly: false, phoneFormat: false, emailFormat: false, minLength: null, maxLength: null },
  }
}

function startAdd(sectionId = '') {
  isNew.value = true
  editing.value = { ...blankQuestion(), sectionId }
}

function startEdit(q) {
  isNew.value = false
  const vr = q.validationRules ?? {}
  editing.value = {
    id: q.id,
    text: q.text,
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
  const hasRules = isTextType && (vr.numberOnly || vr.phoneFormat || vr.emailFormat || vr.minLength || vr.maxLength)
  const validationRules = hasRules
    ? {
        numberOnly: vr.numberOnly || false,
        phoneFormat: vr.phoneFormat || false,
        emailFormat: vr.emailFormat || false,
        minLength: vr.minLength ? Number(vr.minLength) : null,
        maxLength: vr.maxLength ? Number(vr.maxLength) : null,
      }
    : null

  const payload = {
    text: editing.value.text,
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
  if (!confirm(`Delete "${q.text}"?`)) return
  try {
    await api.delete(`/cycles/${cycleId}/questions/${q.id}`)
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
  if (!confirm(`Delete section "${s.title}"? Questions in this section will become ungrouped.`)) return
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
    <div class="flex items-center justify-between">
      <div>
        <div class="flex items-center gap-2 mb-1">
          <RouterLink to="/admin/settings" class="text-sm text-blue-600 hover:underline">
            <span class="mdi mdi-arrow-left mr-1"></span>Settings
          </RouterLink>
        </div>
        <h1 class="text-2xl font-bold text-gray-900">Questions</h1>
        <p class="text-gray-500 mt-0.5 text-sm">{{ cycleName }}</p>
      </div>
      <BaseButton v-if="!editing" @click="startAdd()">
        <span class="mdi mdi-plus mr-1"></span>Add Question
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Add / Edit Question Form -->
    <div v-if="editing" class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm space-y-5">
      <h2 class="text-base font-semibold text-gray-900">
        {{ isNew ? 'New Question' : 'Edit Question' }}
      </h2>

      <!-- Question text -->
      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Question Text <span class="text-red-500">*</span></label>
        <textarea
          v-model="editing.text"
          rows="2"
          placeholder="Enter your question…"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
        />
      </div>

      <!-- Type + Required -->
      <div class="flex items-end gap-4">
        <div class="space-y-1 flex-1">
          <label class="block text-sm font-medium text-gray-700">Type</label>
          <select
            v-model="editing.type"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option v-for="t in QUESTION_TYPES" :key="t.value" :value="t.value">{{ t.label }}</option>
          </select>
        </div>
        <div class="flex items-center gap-2 pb-2">
          <input
            id="q-required"
            v-model="editing.isRequired"
            type="checkbox"
            class="w-4 h-4 text-blue-600 border-gray-300 rounded"
          />
          <label for="q-required" class="text-sm font-medium text-gray-700">Required</label>
        </div>
      </div>

      <!-- Section assignment -->
      <div v-if="sections.length > 0" class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Section</label>
        <select
          v-model="editing.sectionId"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option value="">No section (ungrouped)</option>
          <option v-for="s in sections" :key="s.id" :value="s.id">{{ s.title }}</option>
        </select>
      </div>

      <!-- Options (MC / SC only) -->
      <div v-if="hasOptions" class="space-y-3">
        <label class="block text-sm font-medium text-gray-700">Answer Options</label>

        <div v-if="editing.options.length" class="space-y-2">
          <div
            v-for="(opt, i) in editing.options"
            :key="i"
            class="flex items-center gap-2 bg-gray-50 border border-gray-200 rounded-lg px-3 py-2"
          >
            <span class="text-sm text-gray-700 flex-1">{{ opt }}</span>
            <button type="button" @click="removeOption(i)" class="text-gray-400 hover:text-red-500 transition-colors">
              <span class="mdi mdi-close text-lg"></span>
            </button>
          </div>
        </div>

        <div class="flex gap-2">
          <input
            v-model="editing.newOption"
            type="text"
            placeholder="Add an option…"
            class="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            @keydown.enter.prevent="addOption"
          />
          <button
            type="button"
            @click="addOption"
            class="px-3 py-2 text-sm border border-gray-300 rounded-lg hover:bg-gray-50 transition-colors text-gray-700"
          >
            <span class="mdi mdi-plus"></span> Add
          </button>
        </div>

        <p v-if="editing.options.length === 0" class="text-xs text-amber-600">
          Add at least one option for this question type.
        </p>
      </div>

      <!-- Validation rules -->
      <div v-if="showValidationRules" class="space-y-4 border-t border-gray-100 pt-4">
        <p class="text-sm font-medium text-gray-700">Validation Rules</p>

        <div class="grid grid-cols-3 gap-3">
          <label class="flex items-center gap-2 cursor-pointer">
            <input v-model="editing.validationRules.numberOnly" type="checkbox" class="w-4 h-4 text-blue-600 border-gray-300 rounded" />
            <span class="text-sm text-gray-700">Numbers only</span>
          </label>
          <label class="flex items-center gap-2 cursor-pointer">
            <input v-model="editing.validationRules.phoneFormat" type="checkbox" class="w-4 h-4 text-blue-600 border-gray-300 rounded" />
            <span class="text-sm text-gray-700">Phone format</span>
          </label>
          <label class="flex items-center gap-2 cursor-pointer">
            <input v-model="editing.validationRules.emailFormat" type="checkbox" class="w-4 h-4 text-blue-600 border-gray-300 rounded" />
            <span class="text-sm text-gray-700">Email format</span>
          </label>
        </div>

        <div class="flex gap-4">
          <div class="space-y-1 flex-1">
            <label class="block text-xs font-medium text-gray-700">Min Length</label>
            <input
              v-model.number="editing.validationRules.minLength"
              type="number"
              min="0"
              placeholder="—"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div class="space-y-1 flex-1">
            <label class="block text-xs font-medium text-gray-700">Max Length</label>
            <input
              v-model.number="editing.validationRules.maxLength"
              type="number"
              min="0"
              placeholder="—"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
      </div>

      <!-- Actions -->
      <div class="flex gap-3 pt-2 border-t border-gray-100">
        <BaseButton :loading="saving" @click="save">
          <span class="mdi mdi-content-save mr-1"></span>
          {{ isNew ? 'Add Question' : 'Save Changes' }}
        </BaseButton>
        <BaseButton variant="secondary" @click="cancel">Cancel</BaseButton>
      </div>
    </div>

    <!-- Questions List -->
    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading questions…
    </div>

    <div v-else-if="questions.length === 0 && sections.length === 0 && !editing" class="text-center py-6">
      <span class="mdi mdi-comment-question-outline text-4xl text-gray-300 block mb-2"></span>
      <p class="text-gray-500 text-sm mb-4">No questions yet.</p>
    </div>

    <template v-else v-for="group in groupedQuestions" :key="group.id ?? '__ungrouped'">

      <!-- Named section header -->
      <template v-if="group.id !== null">
        <!-- Inline edit form (replaces header) -->
        <div v-if="editingSection?.id === group.id" class="flex items-center gap-2 pt-1">
          <input
            v-model="editingSection.title"
            type="text"
            placeholder="Section title…"
            class="flex-1 border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            @keydown.enter.prevent="saveSection"
            @keydown.escape.prevent="cancelSection"
          />
          <input
            v-model="editingSection.description"
            type="text"
            placeholder="Description…"
            class="flex-1 border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            @keydown.enter.prevent="saveSection"
            @keydown.escape.prevent="cancelSection"
          />
          <button
            @click="saveSection"
            :disabled="savingSection"
            class="px-3 py-1.5 text-xs font-medium bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 transition-colors"
          >
            Save
          </button>
          <button
            @click="cancelSection"
            class="px-3 py-1.5 text-xs font-medium border border-gray-300 text-gray-600 rounded-lg hover:bg-gray-50 transition-colors"
          >
            Cancel
          </button>
        </div>

        <!-- Normal section header -->
        <div v-else class="flex items-center gap-2 pt-1">
          <div class="flex flex-col">
            <button
              @click="moveSectionUp(group)"
              :disabled="sectionIndex(group.id) === 0"
              class="text-gray-300 hover:text-gray-500 disabled:opacity-30 disabled:cursor-not-allowed leading-none"
            >
              <span class="mdi mdi-chevron-up text-base"></span>
            </button>
            <button
              @click="moveSectionDown(group)"
              :disabled="sectionIndex(group.id) === sections.length - 1"
              class="text-gray-300 hover:text-gray-500 disabled:opacity-30 disabled:cursor-not-allowed leading-none"
            >
              <span class="mdi mdi-chevron-down text-base"></span>
            </button>
          </div>
          <div class="min-w-0">
            <h3 class="text-sm font-semibold text-gray-700 leading-tight">{{ group.title }}</h3>
            <p v-if="group.description" class="text-xs text-gray-400 truncate">{{ group.description }}</p>
          </div>
          <div class="flex-1 border-t border-gray-200 mx-1"></div>
          <button
            @click="startEditSection(group)"
            class="text-gray-400 hover:text-blue-600 transition-colors p-1"
            title="Edit section"
          >
            <span class="mdi mdi-pencil text-sm"></span>
          </button>
          <button
            @click="deleteSection(group)"
            class="text-gray-400 hover:text-red-500 transition-colors p-1"
            title="Delete section"
          >
            <span class="mdi mdi-trash-can text-sm"></span>
          </button>
        </div>
      </template>

      <!-- Ungrouped header (only when named sections exist) -->
      <div v-else-if="sections.length > 0" class="flex items-center gap-3 pt-1">
        <h3 class="text-sm font-semibold text-gray-400 shrink-0">Ungrouped</h3>
        <div class="flex-1 border-t border-gray-200 border-dashed"></div>
      </div>

      <!-- Questions + add question button -->
      <div class="space-y-2">
        <div v-if="group.questions.length === 0 && group.id !== null" class="text-xs text-gray-400 pl-1">
          No questions in this section.
        </div>

        <div
          v-for="q in group.questions"
          :key="q.id"
          class="bg-white rounded-xl border border-gray-200 p-4 shadow-sm flex items-start gap-3"
        >
          <!-- Order controls -->
          <div class="flex flex-col gap-0.5 pt-0.5">
            <button
              @click="moveUp(globalIndex(q))"
              :disabled="globalIndex(q) === 0"
              class="text-gray-400 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed"
            >
              <span class="mdi mdi-chevron-up text-xl leading-none"></span>
            </button>
            <button
              @click="moveDown(globalIndex(q))"
              :disabled="globalIndex(q) === questions.length - 1"
              class="text-gray-400 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed"
            >
              <span class="mdi mdi-chevron-down text-xl leading-none"></span>
            </button>
          </div>

          <!-- Content -->
          <div class="flex-1 min-w-0">
            <div class="flex items-start justify-between gap-2">
              <p class="text-sm font-medium text-gray-900 leading-snug">{{ q.text }}</p>
              <div class="flex gap-1.5 shrink-0">
                <button @click="startEdit(q)" class="text-gray-400 hover:text-blue-600 transition-colors p-1" title="Edit">
                  <span class="mdi mdi-pencil text-base"></span>
                </button>
                <button @click="deleteQuestion(q)" class="text-gray-400 hover:text-red-500 transition-colors p-1" title="Delete">
                  <span class="mdi mdi-trash-can text-base"></span>
                </button>
              </div>
            </div>
            <div class="flex items-center gap-2 mt-1.5 flex-wrap">
              <span class="text-xs bg-gray-100 text-gray-600 px-2 py-0.5 rounded-full">{{ typLabel(q.type) }}</span>
              <span v-if="q.isRequired" class="text-xs text-red-600 font-medium">Required</span>
              <span v-if="q.options?.length" class="text-xs text-gray-400">
                {{ q.options.length }} option{{ q.options.length !== 1 ? 's' : '' }}
              </span>
            </div>
            <div v-if="q.options?.length" class="mt-2 flex flex-wrap gap-1.5">
              <span
                v-for="opt in q.options"
                :key="opt.id"
                class="text-xs border border-gray-200 bg-gray-50 text-gray-600 px-2 py-0.5 rounded"
              >
                {{ opt.text }}
              </span>
            </div>
          </div>

          <span class="text-xs font-mono text-gray-300 pt-0.5">{{ globalIndex(q) + 1 }}</span>
        </div>

        <!-- Add question to this group -->
        <button
          v-if="!editing"
          @click="startAdd(group.id ?? '')"
          class="w-full flex items-center justify-center gap-1.5 py-2 text-xs text-gray-400 hover:text-blue-600 hover:bg-blue-50 rounded-lg border border-dashed border-gray-200 hover:border-blue-300 transition-colors"
        >
          <span class="mdi mdi-plus"></span>
          Add question{{ group.title ? ` to ${group.title}` : '' }}
        </button>
      </div>

      <!-- Add section after this named section -->
      <template v-if="group.id !== null && !editing">
        <!-- Inline create form -->
        <div
          v-if="editingSection && !editingSection.id && insertAfterSectionId === group.id"
          class="bg-gray-50 border border-dashed border-blue-300 rounded-xl p-4 space-y-3"
        >
          <p class="text-xs font-medium text-blue-700">New section after "{{ group.title }}"</p>
          <div class="space-y-2">
            <input
              v-model="editingSection.title"
              type="text"
              placeholder="Section title…"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              @keydown.enter.prevent="saveSection"
              @keydown.escape.prevent="cancelSection"
            />
            <input
              v-model="editingSection.description"
              type="text"
              placeholder="Description (optional)…"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
              @keydown.enter.prevent="saveSection"
              @keydown.escape.prevent="cancelSection"
            />
          </div>
          <div class="flex gap-2">
            <BaseButton :loading="savingSection" @click="saveSection">Add Section</BaseButton>
            <BaseButton variant="secondary" @click="cancelSection">Cancel</BaseButton>
          </div>
        </div>

        <!-- Add section button -->
        <button
          v-else-if="!editingSection"
          @click="startAddSectionAfter(group.id)"
          class="w-full flex items-center justify-center gap-1.5 py-1.5 text-xs text-gray-300 hover:text-blue-500 hover:bg-blue-50 rounded-lg border border-dashed border-gray-150 hover:border-blue-200 transition-colors"
        >
          <span class="mdi mdi-view-agenda-outline"></span>
          Add section after {{ group.title }}
        </button>
      </template>

    </template>

    <!-- Add first section (when no sections exist) -->
    <template v-if="sections.length === 0 && !editing">
      <div
        v-if="editingSection && !editingSection.id"
        class="bg-gray-50 border border-dashed border-blue-300 rounded-xl p-4 space-y-3"
      >
        <p class="text-xs font-medium text-blue-700">New section</p>
        <div class="space-y-2">
          <input
            v-model="editingSection.title"
            type="text"
            placeholder="Section title…"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            @keydown.enter.prevent="saveSection"
            @keydown.escape.prevent="cancelSection"
          />
          <input
            v-model="editingSection.description"
            type="text"
            placeholder="Description (optional)…"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            @keydown.enter.prevent="saveSection"
            @keydown.escape.prevent="cancelSection"
          />
        </div>
        <div class="flex gap-2">
          <BaseButton :loading="savingSection" @click="saveSection">Add Section</BaseButton>
          <BaseButton variant="secondary" @click="cancelSection">Cancel</BaseButton>
        </div>
      </div>

      <button
        v-else-if="!editingSection"
        @click="startAddSectionAfter(null)"
        class="w-full flex items-center justify-center gap-1.5 py-1.5 text-xs text-gray-300 hover:text-blue-500 hover:bg-blue-50 rounded-lg border border-dashed border-gray-150 hover:border-blue-200 transition-colors"
      >
        <span class="mdi mdi-view-agenda-outline"></span>
        Add section
      </button>
    </template>

  </div>
</template>
