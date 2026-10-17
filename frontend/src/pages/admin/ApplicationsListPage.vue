<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import { useAdminStore } from '../../stores/admin'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const router = useRouter()
const adminStore = useAdminStore()

const loading = ref(true)
const exporting = ref(false)
const alert = ref(null)
const statusFilter = ref('')
const cycleFilter = ref('')
const schoolFilter = ref('')
const search = ref('')
// Typing waits a moment before searching
const searchQuery = ref('')
let searchTimer = null
watch(search, (v) => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => { searchQuery.value = (v ?? '').trim() }, 300)
})
const page = ref(adminStore.pagination.page)
const perPage = ref(adminStore.pagination.perPage)

const statusItems = [
  { title: 'All statuses', value: '' },
  { title: 'Draft', value: 'draft' },
  { title: 'Submitted', value: 'submitted' },
  { title: 'Under review', value: 'under_review' },
  { title: 'Awarded', value: 'awarded' },
  { title: 'Rejected', value: 'rejected' },
  { title: 'Archived', value: 'archived' },
]
const cycles = ref([])
const schools = ref([])
const cycleItems = computed(() => [{ title: 'All application periods', value: '' }, ...cycles.value.map((c) => ({ title: c.name, value: c.id }))])
const schoolItems = computed(() => [{ title: 'All schools', value: '' }, ...schools.value.map((s) => ({ title: s.name, value: s.id }))])

onMounted(async () => {
  const [c, s] = await Promise.all([api.get('/admin/cycles').catch(() => null), api.get('/admin/schools').catch(() => null)])
  cycles.value = c?.data ?? []
  schools.value = s?.data ?? []
})

const filters = computed(() => ({
  status: statusFilter.value || undefined,
  search: searchQuery.value || undefined,
  cycleId: cycleFilter.value || undefined,
  schoolId: schoolFilter.value || undefined,
}))
const hasFilters = computed(() => Object.values(filters.value).some(Boolean))
function clearFilters() {
  statusFilter.value = cycleFilter.value = schoolFilter.value = search.value = searchQuery.value = ''
}
// v-data-table-server reloads (back to page 1) whenever `search` changes, so it gets every filter as one key
const filterKey = computed(() => JSON.stringify(filters.value))

// The API returns applications newest-submitted first and has no sort parameter, so columns aren't sortable.
const headers = [
  { title: 'Applicant', key: 'applicant' },
  { title: 'Application period', key: 'cycleName' },
  { title: 'School', key: 'schoolName' },
  { title: 'Status', key: 'status' },
  { title: 'Scholarships won', key: 'scholarshipsWon' },
  { title: 'Submitted', key: 'submittedAt' },
]

// v-data-table-server emits update:options on mount, on paging, and when `search` (the filters) changes.
async function loadApplications({ page, itemsPerPage }) {
  loading.value = true
  try {
    await adminStore.fetchApplications({ ...filters.value, page, pageSize: itemsPerPage })
  } catch {
    alert.value = { type: 'error', message: 'Failed to load applications.' }
  } finally {
    loading.value = false
  }
}

async function exportData() {
  exporting.value = true
  try {
    await adminStore.exportData(filters.value)
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
        <p class="page-subtitle">
          {{ adminStore.pagination.total }} application{{ adminStore.pagination.total === 1 ? '' : 's' }}{{ hasFilters ? ' match these filters' : '' }}
        </p>
      </div>
      <v-btn variant="outlined" prepend-icon="mdi-download" :loading="exporting" :title="hasFilters ? 'Exports the applications matching these filters' : 'Exports every application'" @click="exportData">
        Export CSV
      </v-btn>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Filters -->
    <v-card class="pa-4 flex flex-wrap items-center gap-3">
      <v-text-field
        v-model="search"
        prepend-inner-icon="mdi-magnify"
        placeholder="Search name or email"
        density="compact"
        hide-details
        clearable
        class="flex-1 min-w-[220px]"
      />
      <v-select v-model="cycleFilter" :items="cycleItems" aria-label="Application period" hide-details density="compact" class="min-w-[200px] max-w-[260px]" />
      <v-select v-model="schoolFilter" :items="schoolItems" aria-label="School" hide-details density="compact" class="min-w-[180px] max-w-[240px]" />
      <v-select v-model="statusFilter" :items="statusItems" aria-label="Status" hide-details density="compact" class="min-w-[160px] max-w-[220px]" />
      <v-btn v-if="hasFilters" variant="text" size="small" @click="clearFilters">Clear filters</v-btn>
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
        :search="filterKey"
        :loading="loading"
        disable-sort
        no-data-text="No applications found."
        class="clickable-rows"
        @update:options="loadApplications"
        @click:row="(e, { item }) => goToDetail(item.id)"
      >
        <template #item.applicant="{ item }">
          <RouterLink :to="`/admin/applications/${item.id}`" class="font-semibold text-navy no-underline hover:underline" @click.stop>
            {{ item.firstName }} {{ item.lastName }}
          </RouterLink>
        </template>
        <template #item.cycleName="{ value }">
          <span class="text-slate-400">{{ value }}</span>
        </template>
        <template #item.schoolName="{ value }">
          <span class="text-slate-400">{{ value ?? '—' }}</span>
        </template>
        <template #item.status="{ item }">
          <StatusBadge :status="item.status" />
        </template>
        <template #item.scholarshipsWon="{ value }">
          <div v-if="value?.length" class="flex flex-wrap gap-1 py-1">
            <v-chip v-for="name in value" :key="name" color="success" prepend-icon="mdi-trophy-outline">{{ name }}</v-chip>
          </div>
          <span v-else class="text-slate-400">—</span>
        </template>
        <template #item.submittedAt="{ item }">
          <span class="text-slate-400">{{ item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : '—' }}</span>
        </template>
      </v-data-table-server>
    </v-card>
  </div>
</template>
