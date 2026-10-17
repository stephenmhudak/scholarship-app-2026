<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'
import { useAdminStore } from '../../stores/admin'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import AnswerDisplay from '../../components/application/AnswerDisplay.vue'
import { visibleQuestionIds, answerValue } from '../../utils/questionVisibility'

const route = useRoute()
const router = useRouter()
const adminStore = useAdminStore()

const appId = route.params.id
const application = ref(null)
const questions = ref([])
const loading = ref(true)
const updatingStatus = ref(false)
const newStatus = ref('')
const alert = ref(null)

const statuses = ['submitted', 'under_review', 'awarded', 'rejected', 'archived']
const statusItems = statuses.map((s) => ({ title: s.replace('_', ' '), value: s }))

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
    if (response.data.cycleId) {
      const qRes = await api.get(`/cycles/${response.data.cycleId}/questions`)
      questions.value = qRes.data
    }
  } catch {
    alert.value = { type: 'error', message: 'Failed to load application.' }
  } finally {
    loading.value = false
  }
})

async function updateStatus() {
  if (!newStatus.value || newStatus.value === application.value.status) return

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
      <v-card class="pa-6">
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
          <div class="flex items-center gap-2">
            <label class="text-sm font-semibold text-navy">Change Status:</label>
            <v-select
              v-model="newStatus"
              :items="statusItems"
              density="compact"
              hide-details
              class="w-44 capitalize"
            />
            <v-btn color="primary" :loading="updatingStatus" @click="updateStatus">
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

      <!-- Answers card -->
      <v-card title="Application Answers">
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
