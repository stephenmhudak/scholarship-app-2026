<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '../../stores/admin'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(true)
const exporting = ref(false)
const alert = ref(null)
const statusFilter = ref('')

const statuses = ['', 'draft', 'submitted', 'under_review', 'awarded', 'rejected']

onMounted(async () => {
  await loadApplications()
})

async function loadApplications() {
  loading.value = true
  try {
    await adminStore.fetchApplications({
      status: statusFilter.value,
      page: adminStore.pagination.page,
    })
  } catch {
    alert.value = { type: 'error', message: 'Failed to load applications.' }
  } finally {
    loading.value = false
  }
}

async function exportData() {
  exporting.value = true
  try {
    await adminStore.exportData({ status: statusFilter.value })
  } catch {
    alert.value = { type: 'error', message: 'Export failed.' }
  } finally {
    exporting.value = false
  }
}

function onStatusFilterChange() {
  adminStore.pagination.page = 1
  loadApplications()
}

function goToDetail(id) {
  router.push(`/admin/applications/${id}`)
}

function prevPage() {
  if (adminStore.pagination.page > 1) {
    adminStore.pagination.page--
    loadApplications()
  }
}

function nextPage() {
  const totalPages = Math.ceil(adminStore.pagination.total / adminStore.pagination.perPage)
  if (adminStore.pagination.page < totalPages) {
    adminStore.pagination.page++
    loadApplications()
  }
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900">Applications</h1>
        <p class="text-gray-500 mt-1">{{ adminStore.pagination.total }} total applications</p>
      </div>
      <BaseButton variant="secondary" :loading="exporting" @click="exportData">
        <span class="mdi mdi-download mr-1"></span>
        Export CSV
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Filters -->
    <div class="flex items-center gap-4 bg-white border border-gray-200 rounded-xl p-4 shadow-sm">
      <label class="text-sm font-medium text-gray-700">Filter by status:</label>
      <select
        v-model="statusFilter"
        @change="onStatusFilterChange"
        class="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
      >
        <option value="">All Statuses</option>
        <option v-for="s in statuses.slice(1)" :key="s" :value="s" class="capitalize">
          {{ s.replace('_', ' ') }}
        </option>
      </select>
    </div>

    <!-- Table -->
    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading...
    </div>

    <div v-else class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Applicant</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">School</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Submitted</th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
          </tr>
        </thead>
        <tbody class="bg-white divide-y divide-gray-100">
          <tr
            v-for="app in adminStore.applications"
            :key="app.id"
            class="hover:bg-gray-50 cursor-pointer transition-colors"
            @click="goToDetail(app.id)"
          >
            <td class="px-6 py-4">
              <div>
                <p class="text-sm font-medium text-gray-900">{{ app.applicant_name }}</p>
                <p class="text-xs text-gray-500">{{ app.applicant_email }}</p>
              </div>
            </td>
            <td class="px-6 py-4 text-sm text-gray-600">{{ app.school_name || '—' }}</td>
            <td class="px-6 py-4"><StatusBadge :status="app.status" /></td>
            <td class="px-6 py-4 text-sm text-gray-500">
              {{ app.submitted_at ? new Date(app.submitted_at).toLocaleDateString() : '—' }}
            </td>
            <td class="px-6 py-4 text-right">
              <button @click.stop="goToDetail(app.id)" class="text-sm text-blue-600 hover:text-blue-700 font-medium">
                View
              </button>
            </td>
          </tr>
          <tr v-if="adminStore.applications.length === 0">
            <td colspan="5" class="px-6 py-8 text-center text-sm text-gray-500">No applications found.</td>
          </tr>
        </tbody>
      </table>

      <!-- Pagination -->
      <div class="flex items-center justify-between px-6 py-3 border-t border-gray-200 bg-gray-50">
        <p class="text-sm text-gray-500">
          Page {{ adminStore.pagination.page }} of
          {{ Math.ceil(adminStore.pagination.total / adminStore.pagination.perPage) || 1 }}
        </p>
        <div class="flex gap-2">
          <button
            @click="prevPage"
            :disabled="adminStore.pagination.page <= 1"
            class="px-3 py-1 text-sm border border-gray-300 rounded-lg hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Previous
          </button>
          <button
            @click="nextPage"
            :disabled="adminStore.pagination.page >= Math.ceil(adminStore.pagination.total / adminStore.pagination.perPage)"
            class="px-3 py-1 text-sm border border-gray-300 rounded-lg hover:bg-gray-100 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Next
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
