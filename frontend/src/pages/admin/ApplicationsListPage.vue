<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '../../stores/admin'
import { useTableSort } from '../../composables/useTableSort'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import SortTh from '../../components/common/SortTh.vue'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(true)
const exporting = ref(false)
const alert = ref(null)
const statusFilter = ref('')

const statuses = ['draft', 'submitted', 'under_review', 'awarded', 'rejected']

const { sortKey, sortDir, setSort, applySort } = useTableSort('submitted', 'desc')

const sortedApplications = computed(() => applySort(adminStore.applications, {
  applicant: (a) => `${a.lastName} ${a.firstName}`.toLowerCase(),
  status: (a) => a.status,
  submitted: (a) => a.submittedAt ?? '',
}))

onMounted(async () => {
  await loadApplications()
})

async function loadApplications() {
  loading.value = true
  try {
    await adminStore.fetchApplications({
      status: statusFilter.value || undefined,
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
    await adminStore.exportData({ status: statusFilter.value || undefined })
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
        <h1 class="page-title">Applications</h1>
        <p class="page-subtitle">{{ adminStore.pagination.total }} total applications</p>
      </div>
      <BaseButton variant="secondary" :loading="exporting" @click="exportData">
        <span class="mdi mdi-download mr-1.5"></span>
        Export CSV
      </BaseButton>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Filters -->
    <div class="card p-4 flex items-center gap-4">
      <label class="text-sm font-semibold text-navy shrink-0">Filter by status:</label>
      <select
        v-model="statusFilter"
        @change="onStatusFilterChange"
        class="form-select w-48"
      >
        <option value="">All Statuses</option>
        <option v-for="s in statuses" :key="s" :value="s" class="capitalize">
          {{ s.replace('_', ' ') }}
        </option>
      </select>
    </div>

    <!-- Loading -->
    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading…
    </div>

    <!-- Table -->
    <div v-else class="card overflow-hidden">
      <table class="dialect-table">
        <thead>
          <tr>
            <SortTh column="applicant" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Applicant</SortTh>
            <SortTh column="status" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Status</SortTh>
            <SortTh column="submitted" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Submitted</SortTh>
            <th class="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="app in sortedApplications"
            :key="app.id"
            @click="goToDetail(app.id)"
          >
            <td>
              <p class="font-semibold text-navy">{{ app.firstName }} {{ app.lastName }}</p>
            </td>
            <td><StatusBadge :status="app.status" /></td>
            <td class="text-slate-400">
              {{ app.submittedAt ? new Date(app.submittedAt).toLocaleDateString() : '—' }}
            </td>
            <td class="text-right">
              <button @click.stop="goToDetail(app.id)" class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors">
                View
              </button>
            </td>
          </tr>
          <tr v-if="adminStore.applications.length === 0">
            <td colspan="4" class="px-6 py-10 text-center text-sm text-slate-400">No applications found.</td>
          </tr>
        </tbody>
      </table>

      <!-- Pagination -->
      <div class="flex items-center justify-between px-6 py-3 border-t border-[#E9EDF7] bg-[#F4F7FE]">
        <p class="text-sm text-slate-400">
          Page {{ adminStore.pagination.page }} of
          {{ Math.ceil(adminStore.pagination.total / adminStore.pagination.perPage) || 1 }}
        </p>
        <div class="flex gap-2">
          <button
            @click="prevPage"
            :disabled="adminStore.pagination.page <= 1"
            class="pagination-btn"
          >
            Previous
          </button>
          <button
            @click="nextPage"
            :disabled="adminStore.pagination.page >= Math.ceil(adminStore.pagination.total / adminStore.pagination.perPage)"
            class="pagination-btn"
          >
            Next
          </button>
        </div>
      </div>
    </div>
  </div>
</template>
