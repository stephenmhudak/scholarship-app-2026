<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useScoringStore } from '../../stores/scoring'
import ScoringForm from '../../components/scoring/ScoringForm.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const route = useRoute()
const scoringStore = useScoringStore()

const applicationId = route.params.applicationId
const loading = ref(true)
const submitting = ref(false)
const alert = ref(null)

const applicantName = computed(() => {
  const app = scoringStore.currentApplication
  if (!app) return ''
  return `${app.applicantFirstName ?? ''} ${app.applicantLastName ?? ''}`.trim()
})

const formSections = computed(() => {
  if (!scoringStore.sections?.length) {
    return [{ id: null, label: 'Overall Score' }]
  }
  return scoringStore.sections.map((s) => ({ id: s.id, label: s.title }))
})

const answersGroupedBySections = computed(() => {
  const app = scoringStore.currentApplication
  if (!app?.answers?.length) return []

  const questionMap = scoringStore.questionMap
  const sectionMap = Object.fromEntries((scoringStore.sections ?? []).map((s) => [s.id, s]))

  const groups = {}
  for (const answer of app.answers) {
    const q = questionMap[answer.questionId]
    if (!q) continue
    const sectionId = q.sectionId ?? null
    const sectionTitle = sectionId ? (sectionMap[sectionId]?.title ?? 'Section') : 'Ungrouped'
    if (!groups[sectionId ?? 'null']) {
      groups[sectionId ?? 'null'] = { title: sectionTitle, answers: [] }
    }
    groups[sectionId ?? 'null'].answers.push({ ...answer, questionText: q.text })
  }

  return Object.values(groups)
})

onMounted(async () => {
  try {
    await scoringStore.fetchApplication(applicationId)
  } catch {
    alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

async function handleSubmitScore(payload) {
  submitting.value = true
  alert.value = null
  try {
    await scoringStore.submitScore(applicationId, payload)
    alert.value = { type: 'success', message: 'Score submitted successfully!' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to submit score.' }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-5xl">
    <div>
      <h1 class="page-title">Score Application</h1>
      <p v-if="applicantName" class="page-subtitle">{{ applicantName }}</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application…
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-else-if="scoringStore.currentApplication">
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Application Answers -->
        <div class="space-y-5">
          <h2 class="text-base font-bold text-navy">Application Answers</h2>
          <template v-if="answersGroupedBySections.length">
            <div v-for="group in answersGroupedBySections" :key="group.title" class="space-y-2">
              <p v-if="scoringStore.sections?.length" class="text-xs font-bold text-slate-400 uppercase tracking-widest px-1">
                {{ group.title }}
              </p>
              <div
                v-for="answer in group.answers"
                :key="answer.questionId"
                class="card p-4"
              >
                <p class="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1.5">
                  {{ answer.questionText || answer.questionId }}
                </p>
                <p class="text-sm text-navy whitespace-pre-wrap">
                  {{ answer.selectedOptions?.length ? answer.selectedOptions.join(', ') : (answer.textValue || '—') }}
                </p>
              </div>
            </div>
          </template>
          <p v-else class="text-sm text-slate-400">No answers available.</p>
        </div>

        <!-- Scoring Panel -->
        <div>
          <h2 class="text-base font-bold text-navy mb-4">
            {{ scoringStore.existingScores.length ? 'Edit Your Score' : 'Submit Your Score' }}
          </h2>
          <ScoringForm
            :sections="formSections"
            :existing-scores="scoringStore.existingScores"
            :submitting="submitting"
            @submit="handleSubmitScore"
          />
        </div>
      </div>
    </template>
  </div>
</template>
