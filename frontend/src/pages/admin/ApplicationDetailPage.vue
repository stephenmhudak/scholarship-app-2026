<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'
import { useAdminStore } from '../../stores/admin'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

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

const statuses = ['submitted', 'under_review', 'awarded', 'rejected']

const answersMap = computed(() =>
  Object.fromEntries((application.value?.answers ?? []).map((a) => [a.questionId, a]))
)

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
    <RouterLink to="/admin/applications" class="inline-flex items-center text-sm font-semibold text-primary hover:text-primary-700 transition-colors">
      <span class="mdi mdi-arrow-left mr-1"></span>
      Back to Applications
    </RouterLink>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application…
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-else-if="application">
      <!-- Applicant header card -->
      <div class="section-card">
        <div class="flex items-start justify-between">
          <div>
            <h1 class="text-2xl font-bold text-navy">{{ applicantName }}</h1>
            <p class="text-slate-400 text-sm mt-0.5">{{ application.applicantEmail }}</p>
          </div>
          <StatusBadge :status="application.status" />
        </div>

        <div class="grid grid-cols-2 gap-4 mt-6 text-sm">
          <div>
            <p class="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-1">Submitted</p>
            <p class="font-semibold text-navy">
              {{ application.submittedAt ? new Date(application.submittedAt).toLocaleDateString() : '—' }}
            </p>
          </div>
          <div>
            <p class="text-xs font-semibold text-slate-400 uppercase tracking-wide mb-1">Created</p>
            <p class="font-semibold text-navy">{{ new Date(application.createdAt).toLocaleDateString() }}</p>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex flex-wrap items-center gap-3 mt-6 pt-5 border-t border-[#E9EDF7]">
          <div class="flex items-center gap-2">
            <label class="text-sm font-semibold text-navy">Change Status:</label>
            <select
              v-model="newStatus"
              class="form-select w-44"
            >
              <option v-for="s in statuses" :key="s" :value="s" class="capitalize">
                {{ s.replace('_', ' ') }}
              </option>
            </select>
            <BaseButton size="sm" :loading="updatingStatus" @click="updateStatus">
              Update
            </BaseButton>
          </div>

          <BaseButton
            v-if="application.status !== 'draft'"
            variant="secondary"
            size="sm"
            @click="router.push(`/scoring/${appId}`)"
          >
            <span class="mdi mdi-star-outline mr-1.5"></span>
            Score Application
          </BaseButton>
        </div>
      </div>

      <!-- Answers card -->
      <div class="section-card">
        <h2 class="text-base font-bold text-navy mb-5">Application Answers</h2>
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
        <p v-else class="text-sm text-slate-400">No answers available.</p>
      </div>
    </template>

  </div>
</template>
