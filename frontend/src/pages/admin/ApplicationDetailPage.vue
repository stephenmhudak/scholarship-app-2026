<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'
import { useAdminStore } from '../../stores/admin'
import { useConfirm } from '../../composables/useConfirm'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import AnswerDisplay from '../../components/application/AnswerDisplay.vue'
import { visibleQuestionIds, answerValue } from '../../utils/questionVisibility'

const route = useRoute()
const router = useRouter()
const adminStore = useAdminStore()
const confirm = useConfirm()

const appId = route.params.id
const application = ref(null)
const questions = ref([])
const loading = ref(true)
const updatingStatus = ref(false)
const newStatus = ref('')
const alert = ref(null)

// Scores, references and scholarships won, each loaded separately so one failing doesn't hide the rest
const scores = ref([])
const references = ref(null)
const awards = ref([])

const STATUS_LABELS = { draft: 'Draft', submitted: 'Submitted', under_review: 'Under Review', awarded: 'Awarded', rejected: 'Rejected', archived: 'Archived' }
// "Awarded" comes from awarding a scholarship (Award Scholarships), so it's only listed while it's the current status
const statusItems = computed(() => ['submitted', 'under_review', 'awarded', 'rejected', 'archived', 'draft']
  .filter((s) => (s !== 'awarded' && s !== 'draft') || s === application.value?.status)
  .map((s) => ({ title: STATUS_LABELS[s], value: s })))

const money = (n) => n == null ? null : Number(n).toLocaleString('en-US', { style: 'currency', currency: 'USD' })

// What a score is for: a section, a reference, or the application as a whole
function scoreTarget(s) {
  if (s.ReferenceRequestId) return `Reference: ${s.ReferenceTitle ?? 'Reference'}${s.ReferenceLabel ? ` (${s.ReferenceLabel})` : ''}`
  return s.SectionTitle ?? 'Overall'
}
const scorers = computed(() => {
  const by = new Map()
  for (const s of scores.value) {
    if (!by.has(s.ScoredById)) by.set(s.ScoredById, { id: s.ScoredById, name: `${s.ScorerFirstName} ${s.ScorerLastName}`, scores: [] })
    by.get(s.ScoredById).scores.push(s)
  }
  return [...by.values()].map((g) => ({ ...g, average: g.scores.reduce((t, s) => t + Number(s.Score), 0) / g.scores.length }))
})
// Same as Scoring Progress: the average of each section's (or reference's) average
const averageScore = computed(() => {
  if (!scores.value.length) return null
  const groups = new Map()
  for (const s of scores.value) {
    const key = `${s.SectionId ?? ''}|${s.ReferenceRequestId ?? ''}`
    groups.set(key, [...(groups.get(key) ?? []), Number(s.Score)])
  }
  const avgs = [...groups.values()].map((v) => v.reduce((a, b) => a + b, 0) / v.length)
  return avgs.reduce((a, b) => a + b, 0) / avgs.length
})

// Reference forms the application needs (or has links for), with what's come in
const referenceForms = computed(() => (references.value?.forms ?? []).filter((f) => f.required || f.requests.length))

const answersMap = computed(() =>
  Object.fromEntries((application.value?.answers ?? []).map((a) => [a.questionId, a]))
)

// Conditional questions the applicant's answers didn't call for aren't part of their application
const shownQuestions = computed(() => {
  const visible = visibleQuestionIds(questions.value, (id) => answerValue(answersMap.value[id]))
  return questions.value.filter((q) => visible.has(q.id))
})

const applicantName = computed(() => {
  if (!application.value) return ''
  return `${application.value.applicantFirstName ?? ''} ${application.value.applicantLastName ?? ''}`.trim()
})

onMounted(async () => {
  try {
    const response = await api.get(`/applications/${appId}`)
    application.value = response.data
    newStatus.value = response.data.status
    const [qRes, scoresRes, refsRes, awardsRes] = await Promise.all([
      response.data.cycleId ? api.get(`/cycles/${response.data.cycleId}/questions`) : null,
      api.get(`/admin/applications/${appId}/scores`).catch(() => null),
      api.get(`/applications/${appId}/references`).catch(() => null),
      api.get(`/applications/${appId}/awards`).catch(() => null),
    ])
    questions.value = qRes?.data ?? []
    scores.value = scoresRes?.data ?? []
    references.value = refsRes?.data ?? null
    awards.value = awardsRes?.data ?? []
  } catch {
    alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

async function updateStatus() {
  if (!newStatus.value || newStatus.value === application.value.status) return
  if (awards.value.length && !await confirm({
    title: 'Change status?',
    text: `This application has been awarded ${awards.value.length === 1 ? 'a scholarship' : `${awards.value.length} scholarships`}. Changing its status won't remove ${awards.value.length === 1 ? 'that award' : 'those awards'}; remove ${awards.value.length === 1 ? 'it' : 'them'} on the Award Scholarships page if that's what you mean to do.`,
    confirmText: `Set to ${STATUS_LABELS[newStatus.value]}`,
  })) {
    newStatus.value = application.value.status
    return
  }

  updatingStatus.value = true
  try {
    await adminStore.updateStatus(appId, newStatus.value)
    application.value.status = newStatus.value
    alert.value = { type: 'success', message: 'Status updated successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to update status.' }
  } finally {
    updatingStatus.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl">
    <v-btn variant="text" color="primary" prepend-icon="mdi-arrow-left" to="/admin/applications" class="-ml-4">
      Back to Applications
    </v-btn>

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading application…
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <template v-if="!loading && application">
      <!-- Applicant header card -->
      <v-card class="pa-6" data-tour="applicant-header">
        <div class="flex items-start justify-between">
          <div>
            <h1 class="text-2xl font-bold text-navy">{{ applicantName }}</h1>
            <p class="text-slate-400 text-sm mt-0.5">{{ application.applicantEmail }}</p>
          </div>
          <StatusBadge :status="application.status" />
        </div>

        <div class="grid grid-cols-2 gap-4 mt-6 text-sm">
          <div>
            <p class="eyebrow mb-1">Submitted</p>
            <p class="font-semibold text-navy">
              {{ application.submittedAt ? new Date(application.submittedAt).toLocaleDateString() : '—' }}
            </p>
          </div>
          <div>
            <p class="eyebrow mb-1">Started</p>
            <p class="font-semibold text-navy">{{ new Date(application.createdAt).toLocaleDateString() }}</p>
          </div>
        </div>

        <!-- Actions -->
        <v-divider class="mt-6 mb-5" />
        <div class="flex flex-wrap items-center gap-3">
          <div class="flex items-start gap-2" data-tour="status">
            <v-select
              v-model="newStatus"
              :items="statusItems"
              label="Status"
              density="compact"
              hint="To mark it Awarded, award a scholarship on the Award Scholarships page."
              persistent-hint
              class="w-72"
            />
            <v-btn color="primary" :loading="updatingStatus" :disabled="newStatus === application.status" @click="updateStatus">
              Update
            </v-btn>
          </div>

          <v-btn
            v-if="application.status !== 'draft'"
            variant="outlined"
            prepend-icon="mdi-star-outline"
            @click="router.push(`/scoring/${appId}`)"
          >
            Score Application
          </v-btn>
        </div>
      </v-card>

      <!-- Scholarships won -->
      <v-card v-if="awards.length" title="Scholarships awarded" class="card-header-success" data-tour="awards">
        <v-card-text class="pa-6 space-y-3">
          <div v-for="a in awards" :key="a.scholarshipId" class="flex flex-wrap items-center gap-3">
            <v-icon icon="mdi-trophy-outline" color="success" />
            <div class="flex-1 min-w-[200px]">
              <p class="text-sm font-semibold text-navy">{{ a.name }}</p>
              <p class="text-xs text-slate-400">Awarded {{ new Date(a.awardedAt).toLocaleDateString() }}</p>
            </div>
            <span v-if="a.amount != null" class="text-sm font-bold text-navy">{{ money(a.amount) }}</span>
          </div>
        </v-card-text>
      </v-card>

      <!-- Scores -->
      <v-card title="Scores" data-tour="scores">
        <template v-if="averageScore != null" #append>
          <v-chip variant="flat" color="white" class="font-semibold">Average {{ averageScore.toFixed(1) }}</v-chip>
        </template>
        <v-card-text class="pa-6">
          <p v-if="!scorers.length" class="text-sm text-slate-400">No one has scored this application yet.</p>
          <div v-else class="space-y-5">
            <div v-for="(g, i) in scorers" :key="g.id" class="space-y-2">
              <v-divider v-if="i > 0" class="mb-5" />
              <div class="flex items-center justify-between gap-3">
                <p class="text-sm font-semibold text-navy">{{ g.name }}</p>
                <span class="text-xs text-slate-400">Average {{ g.average.toFixed(1) }}</span>
              </div>
              <div v-for="s in g.scores" :key="s.Id" class="flex gap-3 text-sm">
                <span class="w-12 shrink-0 font-bold text-navy text-right">{{ Number(s.Score) }}</span>
                <div class="min-w-0">
                  <p class="text-navy">{{ scoreTarget(s) }}</p>
                  <p v-if="s.Comments" class="text-slate-400 whitespace-pre-line">{{ s.Comments }}</p>
                </div>
              </div>
            </div>
          </div>
        </v-card-text>
      </v-card>

      <!-- References -->
      <v-card v-if="referenceForms.length" title="References" data-tour="references">
        <v-card-text class="pa-6 space-y-4">
          <div v-for="f in referenceForms" :key="f.formId" class="space-y-1.5">
            <div class="flex items-center justify-between gap-3">
              <p class="text-sm font-semibold text-navy">{{ f.title }}</p>
              <v-chip v-if="!f.required" color="secondary">No longer needed</v-chip>
              <v-chip v-else :color="f.received >= f.requiredCount ? 'success' : 'warning'">
                {{ f.received }} of {{ f.requiredCount }} received
              </v-chip>
            </div>
            <p v-if="!f.requests.length" class="text-xs text-slate-400">The applicant hasn't sent a link yet.</p>
            <div v-for="r in f.requests" :key="r.id" class="flex items-center gap-2 text-sm">
              <v-icon :icon="r.status === 'submitted' ? 'mdi-check-circle' : 'mdi-clock-outline'" :color="r.status === 'submitted' ? 'success' : 'warning'" size="18" />
              <span class="text-navy">{{ r.label || f.title }}</span>
              <span class="text-xs text-slate-400">
                {{ r.status === 'submitted' ? `Received ${new Date(r.submittedAt).toLocaleDateString()}` : `Link sent ${new Date(r.createdAt).toLocaleDateString()}, waiting` }}
              </span>
            </div>
          </div>
          <p class="text-xs text-slate-400">Scorers read the references themselves when they score the application.</p>
        </v-card-text>
      </v-card>

      <!-- Answers card -->
      <v-card title="Application Answers" data-tour="answers">
        <v-card-text class="pa-6">
          <div v-if="shownQuestions.length" class="space-y-5">
            <template v-for="(q, i) in shownQuestions" :key="q.id">
              <v-divider v-if="i > 0" />
              <div>
                <p class="eyebrow mb-1.5">{{ q.text }}</p>
                <AnswerDisplay :question="q" :answer="answersMap[q.id]" />
              </div>
            </template>
          </div>
          <p v-else class="text-sm text-slate-400">No answers available.</p>
        </v-card-text>
      </v-card>
    </template>
  </div>
</template>
