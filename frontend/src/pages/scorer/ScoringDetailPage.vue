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
const scored = ref(false)
const alert = ref(null)

const applicantName = computed(() => {
  const app = scoringStore.currentApplication
  if (!app) return ''
  return `${app.applicantFirstName ?? ''} ${app.applicantLastName ?? ''}`.trim()
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
    scored.value = true
    alert.value = { type: 'success', message: 'Score submitted successfully!' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to submit score.' }
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl">
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
        <div class="space-y-4">
          <h2 class="text-base font-bold text-navy">Application Answers</h2>
          <div v-if="scoringStore.currentApplication.answers?.length">
            <div
              v-for="answer in scoringStore.currentApplication.answers"
              :key="answer.questionId"
              class="card p-4 mb-3"
            >
              <p class="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1.5">
                {{ scoringStore.questionMap[answer.questionId] || answer.questionId }}
              </p>
              <p class="text-sm text-navy whitespace-pre-wrap">
                {{ answer.selectedOptions?.length ? answer.selectedOptions.join(', ') : (answer.textValue || '—') }}
              </p>
            </div>
          </div>
          <p v-else class="text-sm text-slate-400">No answers available.</p>
        </div>

        <!-- Scoring Panel -->
        <div>
          <h2 class="text-base font-bold text-navy mb-4">Submit Your Score</h2>
          <ScoringForm v-if="!scored" :submitting="submitting" @submit="handleSubmitScore" />
          <div v-else class="flex items-center gap-2 text-success-dark bg-success-light border border-success/20 rounded-xl p-4 text-sm font-medium">
            <span class="mdi mdi-check-circle-outline text-lg"></span>
            Score submitted. Thank you for your review.
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
