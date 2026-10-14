<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../../services/api'
import { useConfirm } from '../../composables/useConfirm'

// A cycle's reference forms: how many of each an applicant needs, and whether that depends on their answers.
// Each form's questions are edited in the question builder (/admin/cycles/:cycleId/references/:formId).
const props = defineProps({
  cycleId: { type: String, required: true },
  // The cycle's application questions, with options: what conditions can look at
  questions: { type: Array, default: () => [] },
})

const confirm = useConfirm()
const forms = ref([])
const loading = ref(true)
const alert = ref(null)
const editing = ref(null)
const saving = ref(false)
const dialogError = ref(null)

const CHOICE_TYPES = ['single_choice', 'multiple_choice']
const TEXT_TYPES = ['short_answer', 'long_answer']

const conditionQuestions = computed(() =>
  props.questions.filter((q) => CHOICE_TYPES.includes(q.type) || TEXT_TYPES.includes(q.type))
)
const questionById = computed(() => Object.fromEntries(props.questions.map((q) => [q.id, q])))

async function load() {
  try {
    const res = await api.get(`/cycles/${props.cycleId}/reference-forms`)
    forms.value = res.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load references.' }
  } finally {
    loading.value = false
  }
}

onMounted(load)

function conditionText(c) {
  const q = questionById.value[c.questionId]
  if (!q) return null
  if (c.optionId) {
    const option = q.options?.find((o) => o.id === c.optionId)
    return `"${q.text}" is "${option?.text ?? '?'}"`
  }
  return `"${q.text}" contains "${c.textContains}"`
}

function requirementText(form) {
  if (form.requirement === 'always') return 'Always required'
  const parts = form.conditions.map(conditionText).filter(Boolean)
  return parts.length ? `Required when ${parts.join(' or ')}` : 'Never required (its conditions were removed)'
}

function startAdd() {
  dialogError.value = null
  editing.value = { title: '', description: '', requiredCount: 1, requirement: 'always', conditions: [] }
}

function startEdit(form) {
  dialogError.value = null
  editing.value = {
    id: form.id,
    title: form.title,
    description: form.description ?? '',
    requiredCount: form.requiredCount,
    requirement: form.requirement,
    conditions: form.conditions.map((c) => ({ ...c })),
  }
}

function addCondition() {
  editing.value.conditions.push({ questionId: null, optionId: null, textContains: '' })
}

function isChoice(questionId) {
  return CHOICE_TYPES.includes(questionById.value[questionId]?.type)
}

async function save() {
  dialogError.value = null
  saving.value = true
  const payload = {
    title: editing.value.title,
    description: editing.value.description || null,
    requiredCount: Number(editing.value.requiredCount) || 1,
    requirement: editing.value.requirement,
    conditions: editing.value.requirement === 'conditional'
      ? editing.value.conditions.map((c) => ({
          questionId: c.questionId,
          optionId: isChoice(c.questionId) ? c.optionId : null,
          textContains: isChoice(c.questionId) ? null : c.textContains,
        }))
      : [],
  }
  try {
    if (editing.value.id) await api.put(`/cycles/${props.cycleId}/reference-forms/${editing.value.id}`, payload)
    else await api.post(`/cycles/${props.cycleId}/reference-forms`, payload)
    const wasNew = !editing.value.id
    editing.value = null
    await load()
    alert.value = { type: 'success', message: wasNew ? 'Reference added. Add its questions next.' : 'Reference updated.' }
  } catch (err) {
    dialogError.value = err.response?.data?.error || 'Failed to save the reference.'
  } finally {
    saving.value = false
  }
}

async function remove(form) {
  if (!await confirm({ title: 'Delete reference', text: `Delete "${form.title}" and its questions?`, confirmText: 'Delete', color: 'error' })) return
  try {
    await api.delete(`/cycles/${props.cycleId}/reference-forms/${form.id}`).catch(async (err) => {
      const count = err.response?.status === 409 && err.response.data.requestCount
      if (!count) throw err
      if (!await confirm({
        title: 'Delete submitted references too?',
        text: `Applicants have made ${count} link(s) for this reference. Deleting it also deletes those links, any references already submitted through them, and their scores.`,
        confirmText: 'Delete everything',
        color: 'error',
      })) return
      await api.delete(`/cycles/${props.cycleId}/reference-forms/${form.id}`, { params: { force: true } })
    })
    await load()
  } catch {
    alert.value = { type: 'error', message: 'Failed to delete the reference.' }
  }
}
</script>

<template>
  <v-card title="References">
    <template #append>
      <v-btn variant="tonal" prepend-icon="mdi-plus" @click="startAdd">Add reference</v-btn>
    </template>

    <v-card-text class="pa-6 space-y-4">
      <p class="text-sm text-slate-400">
        Applicants send each reference's link to the person writing it, who answers its questions and signs online.
        Only scorers see what's submitted, and each reference is scored on its own. Applicants can't submit until
        every required reference is in.
      </p>

      <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

      <div v-if="loading" class="flex items-center gap-3 text-slate-400">
        <v-progress-circular indeterminate color="primary" size="22" width="2" />
        Loading references…
      </div>

      <p v-else-if="forms.length === 0" class="text-sm text-slate-400">No references are required for this cycle.</p>

      <div v-for="form in forms" :key="form.id" class="rounded-lg border border-[#E9EDF7] pa-4 flex items-start gap-3">
        <div class="flex-1 min-w-0 space-y-1.5">
          <p class="text-sm font-semibold text-navy">{{ form.title }}</p>
          <p v-if="form.description" class="text-xs text-slate-400">{{ form.description }}</p>
          <div class="flex flex-wrap items-center gap-2">
            <v-chip color="primary" class="font-semibold">{{ form.requiredCount }} needed</v-chip>
            <v-chip>{{ form.questionCount }} question{{ form.questionCount === 1 ? '' : 's' }}</v-chip>
          </div>
          <p class="text-xs" :class="form.requirement === 'always' ? 'text-slate-400' : 'text-navy'">{{ requirementText(form) }}</p>
        </div>
        <div class="flex shrink-0 items-center">
          <v-btn variant="outlined" size="small" :to="`/admin/cycles/${cycleId}/references/${form.id}`">Questions</v-btn>
          <v-btn icon="mdi-pencil" variant="text" size="small" title="Edit reference" @click="startEdit(form)" />
          <v-btn icon="mdi-trash-can" variant="text" size="small" color="error" title="Delete reference" @click="remove(form)" />
        </div>
      </div>
    </v-card-text>
  </v-card>

  <v-dialog :model-value="!!editing" max-width="640" @update:model-value="(open) => { if (!open) editing = null }">
    <v-card v-if="editing" :title="editing.id ? 'Edit Reference' : 'Add Reference'">
      <template #append>
        <v-btn icon="mdi-close" variant="text" @click="editing = null" />
      </template>
      <v-card-text class="pa-6 space-y-5">
        <v-alert v-if="dialogError" type="error" :text="dialogError" />
        <v-text-field v-model="editing.title" label="Title *" placeholder="e.g. Teacher Reference" hide-details="auto" />
        <v-textarea v-model="editing.description" label="Instructions for the referee" rows="2" hide-details="auto" />
        <v-number-input v-model="editing.requiredCount" label="How many are needed" :min="1" :max="10" control-variant="split" class="max-w-[14rem]" hide-details />

        <v-radio-group v-model="editing.requirement" hide-details>
          <v-radio label="Always required" value="always" />
          <v-radio label="Only required when the applicant's answers match" value="conditional" />
        </v-radio-group>

        <div v-if="editing.requirement === 'conditional'" class="space-y-3">
          <p class="text-sm text-slate-400">Required when <strong>any</strong> of these match the applicant's saved answers:</p>
          <div v-for="(c, i) in editing.conditions" :key="i" class="flex items-start gap-2">
            <v-select
              v-model="c.questionId"
              :items="conditionQuestions"
              item-title="text"
              item-value="id"
              label="Question"
              hide-details
              class="flex-1 min-w-0"
              @update:model-value="c.optionId = null; c.textContains = ''"
            />
            <v-select
              v-if="c.questionId && isChoice(c.questionId)"
              v-model="c.optionId"
              :items="questionById[c.questionId]?.options ?? []"
              item-title="text"
              item-value="id"
              label="Answer is"
              hide-details
              class="flex-1 min-w-0"
            />
            <v-text-field
              v-else-if="c.questionId"
              v-model="c.textContains"
              label="Answer contains"
              hide-details
              class="flex-1 min-w-0"
            />
            <v-btn icon="mdi-close" variant="text" size="small" class="mt-2" title="Remove" @click="editing.conditions.splice(i, 1)" />
          </div>
          <p v-if="conditionQuestions.length === 0" class="text-xs font-medium text-warning-dark">
            Add a choice or text question to the application first.
          </p>
          <v-btn v-else variant="outlined" size="small" prepend-icon="mdi-plus" @click="addCondition">Add condition</v-btn>
        </div>

        <div class="flex gap-3">
          <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="save">
            {{ editing.id ? 'Save Changes' : 'Add Reference' }}
          </v-btn>
          <v-btn variant="outlined" @click="editing = null">Cancel</v-btn>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>
