<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const route = useRoute()
const appId = route.params.id

const application = ref(null)
const questions = ref([])
const loading = ref(true)
const error = ref(null)

const questionMap = computed(() =>
  Object.fromEntries(questions.value.map((q) => [q.id, q.text]))
)

onMounted(async () => {
  try {
    const response = await api.get(`/applications/${appId}`)
    application.value = response.data
    if (response.data.cycleId) {
      const qRes = await api.get(`/cycles/${response.data.cycleId}/questions`)
      questions.value = qRes.data
    }
  } catch {
    error.value = 'Failed to load application status.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <div>
      <h1 class="text-2xl font-bold text-gray-900">Application Status</h1>
      <p class="text-gray-500 mt-1">View your submission and current status.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application...
    </div>

    <div v-else-if="error" class="text-red-600">{{ error }}</div>

    <template v-else-if="application">
      <!-- Status Summary -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <div class="flex items-center justify-between">
          <div>
            <p class="text-sm text-gray-500">Current Status</p>
            <div class="mt-1">
              <StatusBadge :status="application.status" />
            </div>
          </div>
          <div class="text-right text-sm text-gray-500">
            <p>Submitted: {{ application.submittedAt ? new Date(application.submittedAt).toLocaleDateString() : '—' }}</p>
            <p>Created: {{ new Date(application.createdAt).toLocaleDateString() }}</p>
          </div>
        </div>
      </div>

      <!-- Submitted Answers -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <h2 class="text-lg font-semibold text-gray-900 mb-4">Your Answers</h2>
        <div v-if="application.answers && application.answers.length" class="space-y-4">
          <div
            v-for="answer in application.answers"
            :key="answer.questionId"
            class="border-b border-gray-100 pb-4 last:border-0 last:pb-0"
          >
            <p class="text-sm font-medium text-gray-700">{{ questionMap[answer.questionId] || answer.questionId }}</p>
            <p class="text-sm text-gray-600 mt-1 whitespace-pre-wrap">
              {{ answer.selectedOptions?.length ? answer.selectedOptions.join(', ') : (answer.textValue || '—') }}
            </p>
          </div>
        </div>
        <p v-else class="text-sm text-gray-500">No answers submitted yet.</p>
      </div>
    </template>
  </div>
</template>
