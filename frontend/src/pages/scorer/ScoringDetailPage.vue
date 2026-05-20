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
      <h1 class="text-2xl font-bold text-gray-900">Score Application</h1>
      <p v-if="applicantName" class="text-gray-500 mt-1">{{ applicantName }}</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application...
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-else-if="scoringStore.currentApplication">
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Application Answers -->
        <div class="space-y-4">
          <h2 class="text-lg font-semibold text-gray-900">Application Answers</h2>
          <div v-if="scoringStore.currentApplication.answers?.length">
            <div
              v-for="answer in scoringStore.currentApplication.answers"
              :key="answer.questionId"
              class="bg-white border border-gray-200 rounded-xl p-4 shadow-sm mb-3"
            >
              <p class="text-sm font-medium text-gray-700">
                {{ scoringStore.questionMap[answer.questionId] || answer.questionId }}
              </p>
              <p class="text-sm text-gray-600 mt-2 whitespace-pre-wrap">
                {{ answer.selectedOptions?.length ? answer.selectedOptions.join(', ') : (answer.textValue || '—') }}
              </p>
            </div>
          </div>
          <p v-else class="text-sm text-gray-500">No answers available.</p>
        </div>

        <!-- Scoring Panel -->
        <div>
          <h2 class="text-lg font-semibold text-gray-900 mb-4">Submit Your Score</h2>
          <ScoringForm v-if="!scored" :submitting="submitting" @submit="handleSubmitScore" />
          <p v-else class="text-sm text-green-700 bg-green-50 border border-green-200 rounded-lg p-3">
            Score submitted. Thank you for your review.
          </p>
        </div>
      </div>
    </template>
  </div>
</template>
