<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import ApplicationReferences from '../../components/application/ApplicationReferences.vue'
import api from '../../services/api'

const authStore = useAuthStore()
const route = useRoute()

const openCycles = ref([])
const applications = ref([])
const loading = ref(true)
const error = ref(null)
// NewApplicationPage redirects here with ?error= when an application can't be started.
const startError = ref(route.query.error || null)

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

    <v-alert v-if="startError" type="error" :text="startError" closable @click:close="startError = null" />

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading your applications…
    </div>

    <v-alert v-else-if="error" type="error" :text="error" />

    <template v-else>
      <!-- Open Scholarships -->
      <v-card title="Open Scholarships">
        <v-card-text class="pa-6">
          <p v-if="openCycles.length === 0" class="text-sm text-slate-400">
            No scholarships are accepting applications right now.
          </p>
          <v-list v-else lines="two" class="py-0">
            <template v-for="(cycle, i) in openCycles" :key="cycle.id">
              <v-divider v-if="i > 0" />
              <v-list-item class="px-0 py-3" :subtitle="`Closes ${formatDate(cycle.closeDate)}`">
                <template #title>
                  <div class="flex items-center gap-2">
                    <span class="text-sm font-semibold text-navy">{{ cycle.name }}</span>
                    <StatusBadge v-if="appByCycle[cycle.id]" :status="appByCycle[cycle.id].status" />
                  </div>
                </template>
                <template #append>
                  <div class="flex flex-wrap gap-2">
                    <v-btn
                      v-if="!appByCycle[cycle.id]"
                      color="primary"
                      prepend-icon="mdi-plus"
                      :to="{ path: '/application/new', query: { cycleId: cycle.id } }"
                    >
                      Start Application
                    </v-btn>
                    <template v-else>
                      <v-btn
                        v-if="appByCycle[cycle.id].status === 'draft'"
                        color="primary"
                        prepend-icon="mdi-file-document-edit-outline"
                        :to="`/application/${appByCycle[cycle.id].id}`"
                      >
                        Continue Application
                      </v-btn>
                      <v-btn
                        variant="outlined"
                        prepend-icon="mdi-clipboard-check-outline"
                        :to="`/application/${appByCycle[cycle.id].id}/status`"
                      >
                        View Status
                      </v-btn>
                    </template>
                  </div>
                </template>
              </v-list-item>
            </template>
          </v-list>
        </v-card-text>
      </v-card>

      <!-- Past Applications -->
      <v-card v-if="pastApplications.length" title="Past Applications">
        <v-card-text class="pa-6">
          <v-list lines="two" class="py-0">
            <template v-for="(app, i) in pastApplications" :key="app.id">
              <v-divider v-if="i > 0" />
              <v-list-item
                class="px-0"
                :subtitle="app.submittedAt ? `Submitted ${formatDate(app.submittedAt)}` : `Started ${formatDate(app.createdAt)}`"
              >
                <template #title>
                  <div class="flex items-center gap-2">
                    <span class="text-sm font-semibold text-navy">{{ app.cycleName || 'Scholarship' }}</span>
                    <StatusBadge :status="app.status" />
                  </div>
                </template>
                <template #append>
                  <v-btn variant="text" color="primary" :to="`/application/${app.id}/status`">View</v-btn>
                </template>
              </v-list-item>
            </template>
          </v-list>
        </v-card-text>
      </v-card>

      <!-- References: status for each open application -->
      <ApplicationReferences
        v-for="app in openApplications"
        :key="app.id"
        :applicationId="app.id"
        :title="openApplications.length > 1 ? `References — ${app.cycleName}` : 'References'"
        compact
      />
    </template>
  </div>
</template>
