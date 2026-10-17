<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import AnswerDisplay from '../../components/application/AnswerDisplay.vue'
import NoApplicationCard from '../../components/application/NoApplicationCard.vue'
import { visibleQuestionIds, answerValue } from '../../utils/questionVisibility'

const route = useRoute()
const appId = route.params.id

const application = ref(null)
const questions = ref([])
const loading = ref(true)
const error = ref(null)
// /application/current/status 404s when the applicant hasn't started one — not an error.
const noApplication = ref(false)

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
    if (response.data.cycleId) {
      const qRes = await api.get(`/cycles/${response.data.cycleId}/questions`)
      questions.value = qRes.data
    }
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
            <StatusBadge :status="application.status" />
          </div>
          <div class="text-right text-sm text-slate-400 space-y-0.5">
            <p>Submitted: {{ application.submittedAt ? new Date(application.submittedAt).toLocaleDateString() : '—' }}</p>
            <p>Created: {{ new Date(application.createdAt).toLocaleDateString() }}</p>
          </div>
        </div>
        <p v-if="application.status === 'archived'" class="text-sm text-slate-400 mt-4">
          This application wasn't submitted before the scholarship closed. You can still copy its answers into an application for another scholarship.
        </p>
        <div v-else-if="application.status === 'draft' && application.isCycleOpen" class="mt-4">
          <v-btn color="primary" prepend-icon="mdi-file-document-edit-outline" :to="`/application/${application.id}`">
            Continue Application
          </v-btn>
        </div>
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
