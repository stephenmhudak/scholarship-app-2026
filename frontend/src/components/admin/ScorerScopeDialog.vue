<script setup>
import { ref, computed, watch } from 'vue'
import api from '../../services/api'

// Which applications a scorer can see and score: all of them, or only those whose answers match any of these
// filters (e.g. "Intended major" contains "nursing"). Enforced by the API (ApplicationAccess.ScorerAllowedAsync).
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  user: { type: Object, default: null },
})
const emit = defineEmits(['update:modelValue', 'saved'])

const CHOICE_TYPES = ['single_choice', 'multiple_choice']
const TEXT_TYPES = ['short_answer', 'long_answer']

const loading = ref(false)
const saving = ref(false)
const error = ref(null)
const scope = ref('all')
const filters = ref([])
const cycles = ref([])
// Filterable application questions per cycle, loaded when a cycle is picked
const questionsByCycle = ref({})

const open = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
})

watch(() => props.modelValue, async (isOpen) => {
  if (!isOpen || !props.user) return
  loading.value = true
  error.value = null
  try {
    const [scopeRes, cyclesRes] = await Promise.all([
      api.get(`/admin/users/${props.user.Id}/scoring-scope`),
      api.get('/admin/cycles'),
    ])
    cycles.value = cyclesRes.data
    scope.value = scopeRes.data.scope
    filters.value = scopeRes.data.filters.map((f) => ({
      cycleId: f.CycleId,
      questionId: f.QuestionId,
      optionId: f.OptionId,
      textContains: f.TextContains ?? '',
    }))
    await Promise.all([...new Set(filters.value.map((f) => f.cycleId))].map(loadQuestions))
  } catch {
    error.value = 'Failed to load this scorer\'s access.'
  } finally {
    loading.value = false
  }
})

async function loadQuestions(cycleId) {
  if (!cycleId || questionsByCycle.value[cycleId]) return
  const res = await api.get(`/cycles/${cycleId}/questions`)
  questionsByCycle.value[cycleId] = res.data.filter((q) => [...CHOICE_TYPES, ...TEXT_TYPES].includes(q.type))
}

function questionOf(filter) {
  return questionsByCycle.value[filter.cycleId]?.find((q) => q.id === filter.questionId)
}

async function pickCycle(filter) {
  filter.questionId = null
  filter.optionId = null
  filter.textContains = ''
  await loadQuestions(filter.cycleId)
}

function addFilter() {
  filters.value.push({ cycleId: cycles.value[0]?.id ?? null, questionId: null, optionId: null, textContains: '' })
  loadQuestions(cycles.value[0]?.id)
}

async function save() {
  saving.value = true
  error.value = null
  try {
    await api.put(`/admin/users/${props.user.Id}/scoring-scope`, {
      scope: scope.value,
      filters: scope.value === 'filtered'
        ? filters.value.map((f) => {
            const isChoice = CHOICE_TYPES.includes(questionOf(f)?.type)
            return { questionId: f.questionId, optionId: isChoice ? f.optionId : null, textContains: isChoice ? null : f.textContains }
          })
        : [],
    })
    emit('saved', scope.value)
    open.value = false
  } catch (err) {
    error.value = err.response?.data?.error || 'Failed to save.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="760">
    <v-card :title="`Scoring access — ${user?.FirstName ?? ''} ${user?.LastName ?? ''}`">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </template>
      <v-card-text class="space-y-5">
        <div v-if="loading" class="flex items-center gap-3 text-slate-400">
          <v-progress-circular indeterminate color="primary" size="22" width="2" />
          Loading…
        </div>

        <template v-else>
          <v-alert v-if="error" type="error" :text="error" />

          <v-radio-group v-model="scope" hide-details>
            <v-radio label="All applications" value="all" />
            <v-radio label="Only applications with certain answers" value="filtered" />
          </v-radio-group>

          <div v-if="scope === 'filtered'" class="space-y-3">
            <p class="text-sm text-slate-400">
              This scorer only sees, and can only score, applications that match <strong>any</strong> of these.
              With none, they see nothing.
            </p>
            <div v-for="(f, i) in filters" :key="i" class="grid grid-cols-1 sm:grid-cols-[1fr_1.5fr_1.2fr_auto] gap-2 items-start">
              <v-select
                v-model="f.cycleId"
                :items="cycles"
                item-title="name"
                item-value="id"
                label="Cycle"
                hide-details
                @update:model-value="pickCycle(f)"
              />
              <v-select
                v-model="f.questionId"
                :items="questionsByCycle[f.cycleId] ?? []"
                item-title="text"
                item-value="id"
                label="Question"
                hide-details
                @update:model-value="f.optionId = null; f.textContains = ''"
              />
              <v-select
                v-if="f.questionId && CHOICE_TYPES.includes(questionOf(f)?.type)"
                v-model="f.optionId"
                :items="questionOf(f)?.options ?? []"
                item-title="text"
                item-value="id"
                label="Answer is"
                hide-details
              />
              <v-text-field v-else-if="f.questionId" v-model="f.textContains" label="Answer contains" hide-details />
              <div v-else />
              <v-btn icon="mdi-close" variant="text" size="small" class="mt-2" title="Remove" @click="filters.splice(i, 1)" />
            </div>
            <v-btn variant="outlined" size="small" prepend-icon="mdi-plus" :disabled="!cycles.length" @click="addFilter">Add answer</v-btn>
          </div>

          <div class="flex justify-end gap-2">
            <v-btn variant="text" @click="open = false">Cancel</v-btn>
            <v-btn color="primary" :loading="saving" @click="save">Save</v-btn>
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>
