<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAdminStore } from '../../stores/admin'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(true)
const exporting = ref(false)
const alert = ref(null)
const statusFilter = ref('')
const page = ref(adminStore.pagination.page)
const perPage = ref(adminStore.pagination.perPage)

const statuses = ['draft', 'submitted', 'under_review', 'awarded', 'rejected', 'archived']
const statusItems = [
  { title: 'All Statuses', value: '' },
  ...statuses.map((s) => ({ title: s.replace('_', ' '), value: s })),
]

// The API returns applications newest-submitted first and has no sort parameter, so columns aren't sortable.
const headers = [
  { title: 'Applicant', key: 'applicant' },
  { title: 'Status', key: 'status' },
  { title: 'Submitted', key: 'submittedAt' },
  { title: 'Actions', key: 'actions', align: 'end' },
]

// v-data-table-server emits update:options on mount, on paging, and when `search` (the status filter) changes.
async function loadApplications({ page, itemsPerPage }) {
  loading.value = true
  try {
    await adminStore.fetchApplications({
      status: statusFilter.value || undefined,
      page,
      pageSize: itemsPerPage,
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

function goToDetail(id) {
  router.push(`/admin/applications/${id}`)
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <div>
        <h1 class="page-title">Applications</h1>
        <p class="page-subtitle">{{ adminStore.pagination.total }} total applications</p>
      </div>
      <v-btn variant="outlined" prepend-icon="mdi-download" :loading="exporting" @click="exportData">
        Export CSV
      </v-btn>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Filters -->
    <v-card class="pa-4 flex items-center gap-4">
      <label class="text-sm font-semibold text-navy shrink-0">Filter by status:</label>
      <v-select
        v-model="statusFilter"
        :items="statusItems"
        hide-details
        density="compact"
        class="max-w-[14rem] capitalize"
      />
    </v-card>

    <!-- Table -->
    <v-card class="overflow-hidden">
      <v-data-table-server
        v-model:page="page"
        v-model:items-per-page="perPage"
        :headers="headers"
        :items="adminStore.applications"
        :items-length="adminStore.pagination.total"
        :items-per-page-options="[10, 20, 50, 100]"
        :search="statusFilter"
        :loading="loading"
        disable-sort
        no-data-text="No applications found."
        class="clickable-rows"
        @update:options="loadApplications"
        @click:row="(e, { item }) => goToDetail(item.id)"
      >
        <template #item.applicant="{ item }">
          <span class="font-semibold text-navy">{{ item.firstName }} {{ item.lastName }}</span>
        </template>
        <template #item.status="{ item }">
          <StatusBadge :status="item.status" />
        </template>
        <template #item.submittedAt="{ item }">
          <span class="text-slate-400">{{ item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : '—' }}</span>
        </template>
        <template #item.actions="{ item }">
          <v-btn variant="text" color="primary" size="small" @click.stop="goToDetail(item.id)">View</v-btn>
        </template>
      </v-data-table-server>
    </v-card>
  </div>
</template>
