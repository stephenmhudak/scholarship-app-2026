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
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)

const editing = ref(null)
const isNew = ref(false)

const QUESTION_TYPES = [
  { value: 'short_answer',    label: 'Short Answer' },
  { value: 'long_answer',     label: 'Long Answer' },
  { value: 'multiple_choice', label: 'Multiple Choice' },
  { value: 'single_choice',   label: 'Single Choice' },
  { value: 'file_upload',     label: 'File Upload' },
]

const hasOptions = computed(() =>
  editing.value && ['multiple_choice', 'single_choice'].includes(editing.value.type)
)

onMounted(async () => {
  await Promise.all([loadCycleName(), loadQuestions()])
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

function blankQuestion() {
  return {
    text: '',
    type: 'short_answer',
    isRequired: true,
    options: [],
    newOption: '',
  }
}

function startAdd() {
  isNew.value = true
  editing.value = blankQuestion()
}

function startEdit(q) {
  isNew.value = false
  editing.value = {
    id: q.id,
    text: q.text,
    type: q.type,
    isRequired: q.isRequired,
    options: q.options?.map((o) => o.text) ?? [],
    newOption: '',
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

  const payload = {
    text: editing.value.text,
    type: editing.value.type,
    isRequired: editing.value.isRequired,
    order: isNew.value ? questions.value.length : editing.value.order ?? 0,
    options: hasOptions.value ? editing.value.options : [],
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
  const items = ordered.map((q, i) => ({ id: q.id, order: i }))
  try {
    await api.put(`/cycles/${cycleId}/questions/reorder`, items)
    await loadQuestions()
  } catch {
    alert.value = { type: 'error', message: 'Failed to reorder questions.' }
  }
}

function typLabel(type) {
  return QUESTION_TYPES.find((t) => t.value === type)?.label ?? type
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
      <BaseButton v-if="!editing" @click="startAdd">
        <span class="mdi mdi-plus mr-1"></span>
        Add Question
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Add / Edit Form -->
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
            id="required"
            v-model="editing.isRequired"
            type="checkbox"
            class="w-4 h-4 text-blue-600 border-gray-300 rounded"
          />
          <label for="required" class="text-sm font-medium text-gray-700">Required</label>
        </div>
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
            <button
              type="button"
              @click="removeOption(i)"
              class="text-gray-400 hover:text-red-500 transition-colors"
            >
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

        <p v-if="hasOptions && editing.options.length === 0" class="text-xs text-amber-600">
          Add at least one option for this question type.
        </p>
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

    <div v-else-if="questions.length === 0 && !editing" class="bg-white rounded-xl border border-gray-200 p-10 text-center shadow-sm">
      <span class="mdi mdi-comment-question-outline text-4xl text-gray-300 block mb-2"></span>
      <p class="text-gray-500 text-sm">No questions yet. Add the first one above.</p>
    </div>

    <div v-else class="space-y-2">
      <div
        v-for="(q, i) in questions"
        :key="q.id"
        class="bg-white rounded-xl border border-gray-200 p-4 shadow-sm flex items-start gap-3"
      >
        <!-- Order controls -->
        <div class="flex flex-col gap-0.5 pt-0.5">
          <button
            @click="moveUp(i)"
            :disabled="i === 0"
            class="text-gray-400 hover:text-gray-600 disabled:opacity-30 disabled:cursor-not-allowed"
          >
            <span class="mdi mdi-chevron-up text-xl leading-none"></span>
          </button>
          <button
            @click="moveDown(i)"
            :disabled="i === questions.length - 1"
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
              <button
                @click="startEdit(q)"
                class="text-gray-400 hover:text-blue-600 transition-colors p-1"
                title="Edit"
              >
                <span class="mdi mdi-pencil text-base"></span>
              </button>
              <button
                @click="deleteQuestion(q)"
                class="text-gray-400 hover:text-red-500 transition-colors p-1"
                title="Delete"
              >
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
          <!-- Preview options -->
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

        <!-- Question number -->
        <span class="text-xs font-mono text-gray-300 pt-0.5">{{ i + 1 }}</span>
      </div>
    </div>
  </div>
</template>
