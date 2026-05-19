<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import { useAdminStore } from '../../stores/admin'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import AssignScorerModal from '../../components/admin/AssignScorerModal.vue'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const route = useRoute()
const adminStore = useAdminStore()

const appId = route.params.id
const application = ref(null)
const loading = ref(true)
const updatingStatus = ref(false)
const newStatus = ref('')
const showAssignModal = ref(false)
const alert = ref(null)

const statuses = ['submitted', 'under_review', 'awarded', 'rejected']

onMounted(async () => {
  try {
    const response = await api.get(`/admin/applications/${appId}`)
    application.value = response.data
    newStatus.value = response.data.status
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

async function handleAssigned({ applicationId, scorerId }) {
  try {
    await adminStore.assignScorer(applicationId, scorerId)
    alert.value = { type: 'success', message: 'Scorer assigned successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to assign scorer.' }
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl">
    <div class="flex items-center gap-4">
      <RouterLink to="/admin/applications" class="text-sm text-blue-600 hover:underline">
        <span class="mdi mdi-arrow-left mr-1"></span>
        Back to Applications
      </RouterLink>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading application...
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-else-if="application">
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <div class="flex items-start justify-between">
          <div>
            <h1 class="text-2xl font-bold text-gray-900">{{ application.applicant_name }}</h1>
            <p class="text-gray-500 text-sm mt-1">{{ application.applicant_email }}</p>
          </div>
          <StatusBadge :status="application.status" />
        </div>

        <div class="grid grid-cols-2 gap-4 mt-6 text-sm">
          <div>
            <p class="text-gray-500">School</p>
            <p class="font-medium text-gray-900">{{ application.school_name || '—' }}</p>
          </div>
          <div>
            <p class="text-gray-500">Submitted</p>
            <p class="font-medium text-gray-900">
              {{ application.submitted_at ? new Date(application.submitted_at).toLocaleDateString() : '—' }}
            </p>
          </div>
          <div>
            <p class="text-gray-500">Assigned Scorer</p>
            <p class="font-medium text-gray-900">{{ application.scorer_name || 'Unassigned' }}</p>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center gap-4 mt-6 pt-4 border-t border-gray-100">
          <div class="flex items-center gap-2">
            <label class="text-sm font-medium text-gray-700">Change Status:</label>
            <select
              v-model="newStatus"
              class="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option v-for="s in statuses" :key="s" :value="s" class="capitalize">
                {{ s.replace('_', ' ') }}
              </option>
            </select>
            <BaseButton size="sm" :loading="updatingStatus" @click="updateStatus">
              Update
            </BaseButton>
          </div>

          <BaseButton variant="secondary" size="sm" @click="showAssignModal = true">
            <span class="mdi mdi-account-plus mr-1"></span>
            Assign Scorer
          </BaseButton>
        </div>
      </div>

      <!-- Application Answers -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <h2 class="text-lg font-semibold text-gray-900 mb-4">Application Answers</h2>
        <div v-if="application.answers?.length" class="space-y-4">
          <div
            v-for="answer in application.answers"
            :key="answer.question_id"
            class="border-b border-gray-100 pb-4 last:border-0"
          >
            <p class="text-sm font-medium text-gray-700">{{ answer.question_label }}</p>
            <p class="text-sm text-gray-600 mt-1 whitespace-pre-wrap">
              {{ Array.isArray(answer.value) ? answer.value.join(', ') : answer.value }}
            </p>
          </div>
        </div>
        <p v-else class="text-sm text-gray-500">No answers available.</p>
      </div>
    </template>

    <AssignScorerModal
      :show="showAssignModal"
      :applicationId="appId"
      @close="showAssignModal = false"
      @assigned="handleAssigned"
    />
  </div>
</template>
