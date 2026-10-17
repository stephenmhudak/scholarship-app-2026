<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import AnswerDisplay from '../../components/application/AnswerDisplay.vue'
import NoApplicationCard from '../../components/application/NoApplicationCard.vue'
import { visibleQuestionIds, answerValue } from '../../utils/questionVisibility'
import { downloadPdf } from '../../utils/pdf'

const route = useRoute()
const appId = route.params.id

const application = ref(null)
const questions = ref([])
const loading = ref(true)
const error = ref(null)
// /application/current/status 404s when the applicant hasn't started one — not an error.
const noApplication = ref(false)
// Scholarships this application won
const awards = ref([])
const downloading = ref(null)
const letterError = ref(null)

const money = (n) => n == null ? null : Number(n).toLocaleString('en-US', { style: 'currency', currency: 'USD' })

async function downloadLetter(award) {
  downloading.value = award.scholarshipId
  letterError.value = null
  try {
    await downloadPdf(`/applications/${application.value.id}/awards/${award.scholarshipId}/letter`, `${award.name} - award letter.pdf`)
  } catch {
    letterError.value = "Your award letter couldn't be downloaded. Please try again later."
  } finally {
    downloading.value = null
  }
}

const answersMap = computed(() =>
  Object.fromEntries((application.value?.answers ?? []).map((a) => [a.questionId, a]))
)

// Conditional questions the applicant's answers didn't call for aren't part of their application
const shownQuestions = computed(() => {
  const visible = visibleQuestionIds(questions.value, (id) => answerValue(answersMap.value[id]))
  return questions.value.filter((q) => visible.has(q.id))
})

onMounted(async () => {
  try {
    const response = await api.get(`/applications/${appId}`)
    application.value = response.data
    const [qRes, awardsRes] = await Promise.all([
      response.data.cycleId ? api.get(`/cycles/${response.data.cycleId}/questions`) : null,
      api.get(`/applications/${response.data.id}/awards`).catch(() => null),
    ])
    questions.value = qRes?.data ?? []
    awards.value = awardsRes?.data ?? []
  } catch (err) {
    if (appId === 'current' && err.response?.status === 404) noApplication.value = true
    else error.value = 'Failed to load application status.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <div>
      <h1 class="page-title">Application Status</h1>
      <p class="page-subtitle">{{ application?.cycleName || 'View your submission and current status.' }}</p>
    </div>

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading application…
    </div>

    <NoApplicationCard v-else-if="noApplication" />

    <v-alert v-else-if="error" type="error" :text="error" />

    <template v-else-if="application">
      <!-- Status Summary -->
      <v-card class="pa-6">
        <div class="flex items-center justify-between">
          <div>
            <p class="eyebrow mb-2">Current Status</p>
            <StatusBadge :status="application.status" plain />
          </div>
          <div class="text-right text-sm text-slate-400 space-y-0.5">
            <p>Submitted: {{ application.submittedAt ? new Date(application.submittedAt).toLocaleDateString() : '—' }}</p>
            <p>Started: {{ new Date(application.createdAt).toLocaleDateString() }}</p>
          </div>
        </div>
        <p v-if="application.status === 'archived'" class="text-sm text-slate-400 mt-4">
          This application wasn't submitted before the application period closed. You can still copy its answers into a later application.
        </p>
        <div v-else-if="application.status === 'draft' && application.isCycleOpen" class="mt-4">
          <v-btn color="primary" prepend-icon="mdi-file-document-edit-outline" :to="`/application/${application.id}`">
            Continue Application
          </v-btn>
        </div>
      </v-card>

      <!-- Scholarships won -->
      <v-card v-if="awards.length" title="Scholarships awarded" class="card-header-success">
        <v-card-text class="pa-6 space-y-4">
          <p class="text-sm text-navy">
            Congratulations! You've been awarded {{ awards.length === 1 ? 'this scholarship' : 'these scholarships' }}.
          </p>
          <v-alert v-if="letterError" type="error" :text="letterError" closable @click:close="letterError = null" />
          <div v-for="a in awards" :key="a.scholarshipId" class="rounded-lg border border-[#E9EDF7] pa-4 flex flex-wrap items-center gap-3">
            <v-icon icon="mdi-trophy-outline" color="success" />
            <div class="flex-1 min-w-[200px]">
              <p class="text-sm font-semibold text-navy">{{ a.name }}</p>
              <p v-if="a.description" class="text-xs text-slate-400">{{ a.description }}</p>
              <p class="text-xs text-slate-400">Awarded {{ new Date(a.awardedAt).toLocaleDateString() }}</p>
            </div>
            <span v-if="a.amount != null" class="text-base font-bold text-navy">{{ money(a.amount) }}</span>
            <v-btn
              v-if="a.hasLetter"
              variant="outlined"
              size="small"
              prepend-icon="mdi-file-pdf-box"
              :loading="downloading === a.scholarshipId"
              @click="downloadLetter(a)"
            >
              Award letter
            </v-btn>
          </div>
        </v-card-text>
      </v-card>

      <!-- Submitted Answers -->
      <v-card title="Your Answers">
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
          <p v-else class="text-sm text-slate-400">No answers submitted yet.</p>
        </v-card-text>
      </v-card>
    </template>
  </div>
</template>
