<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useScoringStore } from '../../stores/scoring'
import AnswerDisplay from '../../components/application/AnswerDisplay.vue'
import ReferenceResponse from '../../components/scoring/ReferenceResponse.vue'

const route = useRoute()
const scoringStore = useScoringStore()

const applicationId = route.params.applicationId
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)
const scores = ref([])

const applicantName = computed(() => {
  const app = scoringStore.currentApplication
  if (!app) return ''
  return `${app.applicantFirstName ?? ''} ${app.applicantLastName ?? ''}`.trim()
})

// Scholarship eligibility and scholarship sections are shown for context but not scored (the API doesn't count them)
const isScholarshipSection = (s) => !!(s.kind || s.scholarshipId)
const scoredSections = computed(() => (scoringStore.sections ?? []).filter((s) => !isScholarshipSection(s)))

// What gets a score: each section (or one overall score when the cycle has none), then each submitted reference
const formSections = computed(() => {
  const application = scoredSections.value.length
    ? scoredSections.value.map((s) => ({ key: s.id, id: s.id, label: s.title }))
    : [{ key: 'overall', id: null, label: 'Overall Score' }]
  const references = scoringStore.references.map((r) => ({
    key: `reference:${r.id}`,
    id: null,
    referenceRequestId: r.id,
    label: `${r.formTitle}${r.label ? ` — ${r.label}` : ''}`,
    reference: r,
  }))
  return [...application, ...references]
})

// Answers in scholarship sections, read-only (sections with no answers, e.g. a scholarship they weren't eligible for, are left out)
const scholarshipAnswerGroups = computed(() => {
  const scholarshipIds = new Set((scoringStore.sections ?? []).filter(isScholarshipSection).map((s) => s.id))
  return answersGroupedBySections.value.filter((g) => g.sectionId && scholarshipIds.has(g.sectionId))
})

// Section labels are only worth showing when there's more than one thing to score
const showLabels = computed(() => formSections.value.length > 1)

const answersGroupedBySections = computed(() => {
  const app = scoringStore.currentApplication
  if (!app?.answers?.length) return []

  const answersMap = Object.fromEntries(app.answers.map((a) => [a.questionId, a]))
  const sectionMap = Object.fromEntries((scoringStore.sections ?? []).map((s) => [s.id, s]))

  const groups = {}
  for (const q of scoringStore.questions) {
    const answer = answersMap[q.id]
    if (!answer) continue
    const sectionId = q.sectionId ?? null
    const key = sectionId ?? 'null'
    if (!groups[key]) {
      const section = sectionId ? sectionMap[sectionId] : null
      groups[key] = { sectionId, title: section?.title ?? null, order: section?.order ?? 9999, answers: [] }
    }
    groups[key].answers.push({ ...answer, questionText: q.text })
  }

  return Object.values(groups).sort((a, b) => a.order - b.order)
})

const sectionsData = computed(() => {
  const answersBySectionId = Object.fromEntries(
    answersGroupedBySections.value.map((g) => [g.sectionId ?? 'null', g.answers])
  )
  return scores.value.map((s, idx) => ({
    idx,
    key: s.key,
    sectionId: s.sectionId,
    label: s.label,
    reference: s.reference,
    answers: s.reference ? [] : (answersBySectionId[s.sectionId ?? 'null'] ?? []),
  }))
})

function buildScores() {
  scores.value = formSections.value.map((s) => {
    const existing = scoringStore.existingScores.find((e) =>
      (e.referenceRequestId ?? null) === (s.referenceRequestId ?? null) && (e.sectionId ?? null) === s.id)
    return {
      key: s.key,
      sectionId: s.id,
      referenceRequestId: s.referenceRequestId ?? null,
      reference: s.reference,
      label: s.label,
      score: existing?.scoreValue ?? null,
      comments: existing?.comments ?? '',
    }
  })
}

watch(() => [formSections.value, scoringStore.existingScores], buildScores, { deep: true, immediate: true })

onMounted(async () => {
  try {
    await scoringStore.fetchApplication(applicationId)
  } catch {
    alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

async function handleSave() {
  const filled = scores.value.filter((s) => s.score !== '' && s.score != null)
  if (!filled.length) return
  saving.value = true
  alert.value = null
  try {
    await scoringStore.submitScore(applicationId, {
      sectionScores: filled.map((s) => ({
        sectionId: s.sectionId,
        referenceRequestId: s.referenceRequestId,
        score: Number(s.score),
        comments: s.comments || null,
      })),
    })
    alert.value = { type: 'success', message: 'Scores saved.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to save scores.' }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-5xl">
    <div>
      <h1 class="page-title">Score Application</h1>
      <p v-if="applicantName" class="page-subtitle">{{ applicantName }}</p>
    </div>

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading application…
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <template v-if="!loading && scoringStore.currentApplication">
      <div class="space-y-8">
        <div v-for="group in scholarshipAnswerGroups" :key="group.sectionId" class="space-y-3">
          <p class="text-xs font-bold text-slate-400 uppercase tracking-widest">{{ group.title }} · not scored</p>
          <v-card class="pa-5 space-y-4">
            <div v-for="answer in group.answers" :key="answer.questionId">
              <p class="eyebrow mb-1.5">{{ answer.questionText }}</p>
              <AnswerDisplay :question="scoringStore.questionMap[answer.questionId]" :answer="answer" />
            </div>
          </v-card>
        </div>

        <div v-for="section in sectionsData" :key="section.key" class="space-y-3">
          <p v-if="showLabels" class="text-xs font-bold text-slate-400 uppercase tracking-widest">
            <template v-if="section.reference">Reference · </template>{{ section.label }}
          </p>

          <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 items-start">
            <!-- Answers -->
            <v-card class="pa-5 space-y-4" data-tour="answers">
              <ReferenceResponse v-if="section.reference" :reference="section.reference" show-account />
              <template v-else-if="section.answers.length">
                <div
                  v-for="answer in section.answers"
                  :key="answer.questionId"
                >
                  <p class="eyebrow mb-1.5">
                    {{ answer.questionText || answer.questionId }}
                  </p>
                  <AnswerDisplay :question="scoringStore.questionMap[answer.questionId]" :answer="answer" />
                </div>
              </template>
              <p v-else class="text-sm text-slate-400">No answers for this section.</p>
            </v-card>

            <!-- Score input -->
            <v-card class="pa-5 space-y-4" data-tour="score">
              <v-number-input
                v-model="scores[section.idx].score"
                label="Score (1–100)"
                :min="1"
                :max="100"
                control-variant="split"
                class="max-w-[14rem]"
                hide-details
              />
              <v-textarea
                v-model="scores[section.idx].comments"
                label="Comments"
                rows="3"
                placeholder="Enter your comments…"
                hide-details
              />
            </v-card>
          </div>
        </div>
      </div>

      <div class="flex justify-end">
        <v-btn color="primary" prepend-icon="mdi-content-save-outline" :loading="saving" data-tour="save-scores" @click="handleSave">
          Save Scores
        </v-btn>
      </div>
    </template>
  </div>
</template>
