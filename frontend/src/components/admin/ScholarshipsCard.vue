<script setup>
import { ref, computed, onMounted } from 'vue'
import api from '../../services/api'
import { useConfirm } from '../../composables/useConfirm'
import { CONDITION_TYPES, conditionKind, conditionFields, describeCondition, useSchools } from '../../utils/conditionTargets'

// A cycle's scholarships: who's eligible, their eligibility and own questions (edited in the question builder at
// /admin/cycles/:cycleId/scholarships/:scholarshipId/eligibility|details), and the accounts that can award them.
const props = defineProps({
  cycleId: { type: String, required: true },
  // The cycle's application questions, with options: what eligibility can look at
  questions: { type: Array, default: () => [] },
  // The cycle's sections, to leave scholarships' own questions out of eligibility rules
  sections: { type: Array, default: () => [] },
})
const emit = defineEmits(['changed'])

const confirm = useConfirm()
const { schools, schoolName } = useSchools()
const scholarships = ref([])
const loading = ref(true)
const alert = ref(null)
const editing = ref(null)
const saving = ref(false)
const dialogError = ref(null)
const staff = ref([])

const questionById = computed(() => Object.fromEntries(props.questions.map((q) => [q.id, q])))
// A scholarship's own questions only show once someone is eligible, so they can't decide eligibility
const scholarshipSectionIds = computed(() => new Set(props.sections.filter((s) => s.scholarshipId).map((s) => s.id)))
const eligibilitySources = computed(() =>
  props.questions.filter((q) => CONDITION_TYPES.includes(q.type) && !scholarshipSectionIds.value.has(q.sectionId)))

async function load() {
  try {
    const res = await api.get(`/cycles/${props.cycleId}/scholarships`)
    scholarships.value = res.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load scholarships.' }
  } finally {
    loading.value = false
  }
}

onMounted(load)

// Accounts that can be made awarders (not applicants or reference providers)
async function loadStaff() {
  if (staff.value.length) return
  const res = await api.get('/admin/users')
  staff.value = res.data
    .filter((u) => !['applicant', 'referee'].includes(u.Role))
    .map((u) => ({ id: u.Id, title: `${u.FirstName} ${u.LastName}`.trim() || u.Email, subtitle: u.Email }))
}

function eligibilityText(s) {
  if (s.eligibility !== 'conditional') return 'Open to every applicant'
  const parts = s.conditions.map((c) => describeCondition(questionById.value[c.questionId], c, schoolName)).filter(Boolean)
  if (!parts.length) return 'No one is eligible (its conditions were removed)'
  return `Eligible when ${parts.join(s.eligibilityMatch === 'all' ? ' and ' : ' or ')}`
}

async function startAdd() {
  dialogError.value = null
  editing.value = { name: '', description: '', awardAmount: null, eligibility: 'open', eligibilityMatch: 'any', conditions: [], awarderIds: [] }
  await loadStaff()
}

async function startEdit(s) {
  dialogError.value = null
  editing.value = {
    id: s.id,
    name: s.name,
    description: s.description ?? '',
    awardAmount: s.awardAmount,
    eligibility: s.eligibility,
    eligibilityMatch: s.eligibilityMatch,
    conditions: s.conditions.map((c) => ({ ...c, textContains: c.textContains ?? '' })),
    awarderIds: s.awarders.map((a) => a.id),
  }
  await loadStaff()
}

function addCondition() {
  editing.value.conditions.push({ questionId: null, optionId: null, schoolId: null, textContains: '' })
}

function kindOf(questionId) {
  return conditionKind(questionById.value[questionId]?.type)
}

async function save() {
  dialogError.value = null
  saving.value = true
  const e = editing.value
  const payload = {
    name: e.name,
    description: e.description || null,
    awardAmount: e.awardAmount === '' || e.awardAmount == null ? null : Number(e.awardAmount),
    eligibility: e.eligibility,
    eligibilityMatch: e.eligibilityMatch,
    conditions: e.eligibility === 'conditional'
      ? e.conditions.map((c) => ({ questionId: c.questionId, ...conditionFields(questionById.value[c.questionId]?.type, c) }))
      : [],
  }
  try {
    let id = e.id
    if (id) await api.put(`/cycles/${props.cycleId}/scholarships/${id}`, payload)
    else id = (await api.post(`/cycles/${props.cycleId}/scholarships`, payload)).data.id
    await api.put(`/cycles/${props.cycleId}/scholarships/${id}/awarders`, { userIds: e.awarderIds })
    const wasNew = !e.id
    editing.value = null
    await load()
    emit('changed')
    alert.value = { type: 'success', message: wasNew ? 'Scholarship added. Add its eligibility and scholarship questions next.' : 'Scholarship updated.' }
  } catch (err) {
    dialogError.value = err.response?.data?.error || 'Failed to save the scholarship.'
  } finally {
    saving.value = false
  }
}

async function remove(s) {
  if (!await confirm({
    title: 'Delete scholarship',
    text: `Delete "${s.name}", its eligibility questions and its own questions?`,
    confirmText: 'Delete',
    color: 'error',
  })) return
  const url = `/cycles/${props.cycleId}/scholarships/${s.id}`
  try {
    await api.delete(url).catch(async (err) => {
      if (err.response?.status !== 409) throw err
      const { answerCount, awardCount } = err.response.data
      const lost = [answerCount && `${answerCount} answer(s)`, awardCount && `${awardCount} award(s)`].filter(Boolean).join(' and ')
      if (!await confirm({
        title: 'Delete answers and awards too?',
        text: `Deleting this scholarship also deletes ${lost}.`,
        confirmText: 'Delete everything',
        color: 'error',
      })) return
      await api.delete(url, { params: { force: true } })
    })
    await load()
    emit('changed')
  } catch {
    alert.value = { type: 'error', message: 'Failed to delete the scholarship.' }
  }
}
</script>

<template>
  <v-card title="Scholarships" data-tour="scholarships">
    <template #append>
      <v-btn variant="tonal" prepend-icon="mdi-plus" data-tour="add-scholarship" @click="startAdd">Add scholarship</v-btn>
    </template>

    <v-card-text class="pa-6 space-y-4">
      <p class="text-sm text-slate-400">
        Each scholarship's eligibility questions appear for every applicant in a "Scholarship Eligibility" section. Its own
        questions appear, in a section named after it, only to applicants who are eligible. The accounts you choose award
        it from the Award Scholarships page.
      </p>

      <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

      <div v-if="loading" class="flex items-center gap-3 text-slate-400">
        <v-progress-circular indeterminate color="primary" size="22" width="2" />
        Loading scholarships…
      </div>

      <p v-else-if="scholarships.length === 0" class="text-sm text-slate-400">No scholarships in this application period yet.</p>

      <div v-for="s in scholarships" :key="s.id" class="rounded-lg border border-[#E9EDF7] pa-4 space-y-2" data-tour="scholarship">
        <div class="flex items-start gap-3">
          <div class="flex-1 min-w-0 space-y-1">
            <p class="text-sm font-semibold text-navy">{{ s.name }}</p>
            <p v-if="s.description" class="text-xs text-slate-400">{{ s.description }}</p>
            <p v-if="s.awardAmount != null" class="text-xs text-navy">
              Award amount: {{ Number(s.awardAmount).toLocaleString(undefined, { style: 'currency', currency: 'USD' }) }}
            </p>
            <p class="text-xs" :class="s.eligibility === 'open' ? 'text-slate-400' : 'text-navy'">{{ eligibilityText(s) }}</p>
            <p class="text-xs text-slate-400">
              Awarded by: {{ s.awarders.length ? s.awarders.map((a) => a.name).join(', ') : 'super admins only' }}
              <template v-if="s.awardCount"> · {{ s.awardCount }} awarded</template>
            </p>
          </div>
          <div class="flex shrink-0">
            <v-btn icon="mdi-pencil" variant="text" size="small" title="Edit scholarship" @click="startEdit(s)" />
            <v-btn icon="mdi-trash-can" variant="text" size="small" color="error" title="Delete scholarship" @click="remove(s)" />
          </div>
        </div>
        <div class="flex flex-wrap gap-2">
          <v-btn variant="outlined" size="small" :to="`/admin/cycles/${cycleId}/scholarships/${s.id}/eligibility`">
            Eligibility questions ({{ s.eligibilityQuestionCount }})
          </v-btn>
          <v-btn variant="outlined" size="small" :to="`/admin/cycles/${cycleId}/scholarships/${s.id}/details`">
            Scholarship questions ({{ s.questionCount }})
          </v-btn>
        </div>
      </div>
    </v-card-text>
  </v-card>

  <v-dialog :model-value="!!editing" max-width="720" @update:model-value="(open) => { if (!open) editing = null }">
    <v-card v-if="editing" :title="editing.id ? 'Edit Scholarship' : 'Add Scholarship'">
      <template #append>
        <v-btn icon="mdi-close" variant="text" @click="editing = null" />
      </template>
      <v-card-text class="pa-6 space-y-5">
        <v-alert v-if="dialogError" type="error" :text="dialogError" />
        <v-text-field v-model="editing.name" label="Name *" hide-details="auto" />
        <v-textarea v-model="editing.description" label="Description" rows="3" hide-details="auto" />
        <v-text-field
          v-model="editing.awardAmount"
          type="number"
          min="0"
          step="0.01"
          prefix="$"
          label="Award amount"
          hint="Pre-fills each award, and fills {{award_amount}} in its letters."
          persistent-hint
          class="max-w-[18rem]"
        />

        <div class="space-y-3">
          <p class="text-sm font-bold text-navy">Who's eligible</p>
          <v-radio-group v-model="editing.eligibility" hide-details>
            <v-radio label="Every applicant" value="open" />
            <v-radio label="Only applicants whose answers match" value="conditional" />
          </v-radio-group>

          <template v-if="editing.eligibility === 'conditional'">
            <v-select
              v-model="editing.eligibilityMatch"
              :items="[{ title: 'Any of these match', value: 'any' }, { title: 'All of these match', value: 'all' }]"
              label="Eligible when"
              hide-details
              class="max-w-[16rem]"
            />
            <div v-for="(c, i) in editing.conditions" :key="i" class="flex items-start gap-2">
              <v-select
                v-model="c.questionId"
                :items="eligibilitySources"
                item-title="text"
                item-value="id"
                label="Question"
                hide-details
                class="flex-1 min-w-0"
                @update:model-value="c.optionId = null; c.schoolId = null; c.textContains = ''"
              />
              <v-select
                v-if="c.questionId && kindOf(c.questionId) === 'option'"
                v-model="c.optionId"
                :items="questionById[c.questionId]?.options ?? []"
                item-title="text"
                item-value="id"
                label="Answer is"
                hide-details
                class="flex-1 min-w-0"
              />
              <v-autocomplete
                v-else-if="c.questionId && kindOf(c.questionId) === 'school'"
                v-model="c.schoolId"
                :items="schools"
                item-title="name"
                item-value="id"
                label="School is"
                hide-details
                class="flex-1 min-w-0"
              />
              <v-text-field v-else-if="c.questionId" v-model="c.textContains" label="Answer contains" hide-details class="flex-1 min-w-0" />
              <v-btn icon="mdi-close" variant="text" size="small" class="mt-2" title="Remove" @click="editing.conditions.splice(i, 1)" />
            </div>
            <p v-if="eligibilitySources.length === 0" class="text-xs font-medium text-warning-dark">
              Save the scholarship, then add eligibility questions to build its rule from.
            </p>
            <v-btn v-else variant="outlined" size="small" prepend-icon="mdi-plus" @click="addCondition">Add condition</v-btn>
          </template>
        </div>

        <v-autocomplete
          v-model="editing.awarderIds"
          :items="staff"
          item-value="id"
          label="Accounts that can award it"
          hint="Super admins can award every scholarship."
          persistent-hint
          multiple
          chips
          closable-chips
        />

        <div class="flex gap-3">
          <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="save">
            {{ editing.id ? 'Save Changes' : 'Add Scholarship' }}
          </v-btn>
          <v-btn variant="outlined" @click="editing = null">Cancel</v-btn>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>
