<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useScoringStore } from '../../stores/scoring'
import ScoringForm from '../../components/scoring/ScoringForm.vue'
import ScoreCard from '../../components/scoring/ScoreCard.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const route = useRoute()
const scoringStore = useScoringStore()

const applicationId = route.params.applicationId
const loading = ref(true)
const submitting = ref(false)
const alert = ref(null)

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
  <div class="space-y-6 max-w-4xl">
    <div>
      <h1 class="text-2xl font-bold text-gray-900">Score Application</h1>
      <p v-if="scoringStore.currentApplication" class="text-gray-500 mt-1">
        {{ scoringStore.currentApplication.applicant_name }}
      </p>
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
          <div
            v-for="answer in scoringStore.currentApplication.answers"
            :key="answer.question_id"
            class="bg-white border border-gray-200 rounded-xl p-4 shadow-sm"
          >
            <p class="text-sm font-medium text-gray-700">{{ answer.question_label }}</p>
            <p class="text-sm text-gray-600 mt-2 whitespace-pre-wrap">
              {{ Array.isArray(answer.value) ? answer.value.join(', ') : answer.value }}
            </p>
          </div>
        </div>

        <!-- Scoring Panel -->
        <div class="space-y-6">
          <div>
            <h2 class="text-lg font-semibold text-gray-900 mb-4">Submit Your Score</h2>
            <ScoringForm :submitting="submitting" @submit="handleSubmitScore" />
          </div>

          <!-- Existing Scores -->
          <div v-if="scoringStore.scores.length" class="space-y-3">
            <h2 class="text-lg font-semibold text-gray-900">Previous Scores</h2>
            <ScoreCard v-for="score in scoringStore.scores" :key="score.id" :score="score" />
          </div>
        </div>
      </div>
    </template>
  </div>
</template>
