<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import api from '../../services/api'

const authStore = useAuthStore()
const route = useRoute()

const openCycles = ref([])
const applications = ref([])
const references = ref([])
const loading = ref(true)
const error = ref(null)
// NewApplicationPage redirects here with ?error= when an application can't be started.
const startError = route.query.error || null

const appByCycle = computed(() => Object.fromEntries(applications.value.map((a) => [a.cycleId, a])))

// Applications whose cycle is no longer accepting applications (submitted, archived, or decided).
const pastApplications = computed(() =>
  applications.value
    .filter((a) => !openCycles.value.some((c) => c.id === a.cycleId))
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))
)

const openApplications = computed(() => openCycles.value.map((c) => appByCycle.value[c.id]).filter(Boolean))

function formatDate(value) {
  return value ? new Date(value).toLocaleDateString() : '—'
}

onMounted(async () => {
  try {
    const [cyclesRes, appsRes] = await Promise.all([
      api.get('/applications/open-cycles'),
      api.get('/applications/my'),
    ])
    openCycles.value = cyclesRes.data
    applications.value = appsRes.data

    const refLists = await Promise.all(
      openApplications.value.map((a) =>
        api.get(`/applications/${a.id}/references`).then((res) => res.data.map((r) => ({ ...r, cycleName: a.cycleName })))
      )
    )
    references.value = refLists.flat()
  } catch {
    error.value = 'Failed to load dashboard data.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Dashboard</h1>
      <p class="page-subtitle">Welcome back, {{ authStore.fullName || 'Applicant' }}!</p>
    </div>

    <BaseAlert v-if="startError" type="error" :message="startError" dismissible />

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading your applications…
    </div>

    <div v-else-if="error" class="text-sm font-medium text-danger">{{ error }}</div>

    <template v-else>
      <!-- Open Scholarships -->
      <div class="section-card">
        <h2 class="text-base font-bold text-navy mb-4">Open Scholarships</h2>
        <p v-if="openCycles.length === 0" class="text-sm text-slate-400">
          No scholarships are accepting applications right now.
        </p>
        <ul v-else class="divide-y divide-[#E9EDF7]">
          <li
            v-for="cycle in openCycles"
            :key="cycle.id"
            class="flex flex-wrap items-center justify-between gap-3 py-4 first:pt-0 last:pb-0"
          >
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <p class="text-sm font-semibold text-navy">{{ cycle.name }}</p>
                <StatusBadge v-if="appByCycle[cycle.id]" :status="appByCycle[cycle.id].status" />
              </div>
              <p class="text-xs text-slate-400 mt-0.5">Closes {{ formatDate(cycle.closeDate) }}</p>
            </div>

            <div class="flex gap-2">
              <RouterLink
                v-if="!appByCycle[cycle.id]"
                :to="{ path: '/application/new', query: { cycleId: cycle.id } }"
                class="inline-flex items-center gap-1.5 text-sm font-semibold bg-primary text-white px-4 py-2.5 rounded-xl hover:bg-primary-700 transition-colors shadow-sm"
              >
                <span class="mdi mdi-plus"></span>
                Start Application
              </RouterLink>
              <template v-else>
                <RouterLink
                  v-if="appByCycle[cycle.id].status === 'draft'"
                  :to="`/application/${appByCycle[cycle.id].id}`"
                  class="inline-flex items-center gap-1.5 text-sm font-semibold bg-primary text-white px-4 py-2.5 rounded-xl hover:bg-primary-700 transition-colors shadow-sm"
                >
                  <span class="mdi mdi-file-document-edit-outline"></span>
                  Continue Application
                </RouterLink>
                <RouterLink
                  :to="`/application/${appByCycle[cycle.id].id}/status`"
                  class="inline-flex items-center gap-1.5 text-sm font-semibold bg-white text-navy border border-[#E9EDF7] px-4 py-2.5 rounded-xl hover:bg-[#F4F7FE] transition-colors"
                >
                  <span class="mdi mdi-clipboard-check-outline"></span>
                  View Status
                </RouterLink>
              </template>
            </div>
          </li>
        </ul>
      </div>

      <!-- Past Applications -->
      <div v-if="pastApplications.length" class="section-card">
        <h2 class="text-base font-bold text-navy mb-4">Past Applications</h2>
        <ul class="divide-y divide-[#E9EDF7]">
          <li
            v-for="app in pastApplications"
            :key="app.id"
            class="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0 last:pb-0"
          >
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <p class="text-sm font-semibold text-navy">{{ app.cycleName || 'Scholarship' }}</p>
                <StatusBadge :status="app.status" />
              </div>
              <p class="text-xs text-slate-400 mt-0.5">
                {{ app.submittedAt ? `Submitted ${formatDate(app.submittedAt)}` : `Started ${formatDate(app.createdAt)}` }}
              </p>
            </div>
            <RouterLink
              :to="`/application/${app.id}/status`"
              class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors"
            >
              View
            </RouterLink>
          </li>
        </ul>
      </div>

      <!-- References Card -->
      <div class="section-card">
        <h2 class="text-base font-bold text-navy mb-4">Reference Letters</h2>
        <div v-if="references.length === 0" class="text-sm text-slate-400">
          No reference requests yet. Reference requests will appear here after you start your application.
        </div>
        <ul v-else class="divide-y divide-[#E9EDF7]">
          <li
            v-for="ref in references"
            :key="ref.id"
            class="flex items-center justify-between py-3"
          >
            <div>
              <p class="text-sm font-semibold text-navy">{{ ref.label || 'Reference #' + ref.id.slice(0, 8) }}</p>
              <p class="text-xs text-slate-400">
                <template v-if="openApplications.length > 1">{{ ref.cycleName }} · </template>
                Expires {{ formatDate(ref.expiresAt) }}
              </p>
            </div>
            <span
              :class="[
                'text-xs px-2.5 py-1 rounded-full font-semibold border',
                ref.status === 'received'
                  ? 'bg-success-light text-success-dark border-success/20'
                  : 'bg-warning-light text-[#8C6500] border-warning/20',
              ]"
            >
              {{ ref.status === 'received' ? 'Received' : 'Pending' }}
            </span>
          </li>
        </ul>
      </div>
    </template>
  </div>
</template>
