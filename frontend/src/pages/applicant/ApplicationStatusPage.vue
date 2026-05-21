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

const answersMap = computed(() =>
  Object.fromEntries((application.value?.answers ?? []).map((a) => [a.questionId, a]))
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
      <h1 class="page-title">Application Status</h1>
      <p class="page-subtitle">View your submission and current status.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application…
    </div>

    <div v-else-if="error" class="text-sm font-medium text-danger">{{ error }}</div>

    <template v-else-if="application">
      <!-- Status Summary -->
      <div class="section-card">
        <div class="flex items-center justify-between">
          <div>
            <p class="text-xs font-bold text-slate-400 uppercase tracking-wide mb-2">Current Status</p>
            <StatusBadge :status="application.status" />
          </div>
          <div class="text-right text-sm text-slate-400 space-y-0.5">
            <p>Submitted: {{ application.submittedAt ? new Date(application.submittedAt).toLocaleDateString() : '—' }}</p>
            <p>Created: {{ new Date(application.createdAt).toLocaleDateString() }}</p>
          </div>
        </div>
      </div>

      <!-- Submitted Answers -->
      <div class="section-card">
        <h2 class="text-base font-bold text-navy mb-5">Your Answers</h2>
        <div v-if="questions.length" class="space-y-5">
          <div
            v-for="q in questions"
            :key="q.id"
            class="border-b border-[#E9EDF7] pb-5 last:border-0 last:pb-0"
          >
            <p class="text-xs font-bold text-slate-400 uppercase tracking-wide mb-1.5">{{ q.text }}</p>
            <p class="text-sm text-navy whitespace-pre-wrap">
              {{ answersMap[q.id]?.selectedOptions?.length
                  ? answersMap[q.id].selectedOptions.join(', ')
                  : (answersMap[q.id]?.textValue || '—') }}
            </p>
          </div>
        </div>
        <p v-else class="text-sm text-slate-400">No answers submitted yet.</p>
      </div>
    </template>
  </div>
</template>
