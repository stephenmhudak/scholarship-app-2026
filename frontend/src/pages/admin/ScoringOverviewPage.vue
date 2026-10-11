<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const router = useRouter()
const loading = ref(true)
const alert = ref(null)
const applications = ref([])
const expanded = ref([])

const headers = [
  { title: '', key: 'data-table-expand', width: 48 },
  { title: 'Applicant', key: 'applicant', value: (a) => `${a.lastName} ${a.firstName}` },
  { title: 'Status', key: 'status' },
  { title: 'Scorers', key: 'scoredCount' },
  { title: 'Avg Score', key: 'overallAverage', value: (a) => a.overallAverage ?? -1 },
  { title: 'Action', key: 'actions', sortable: false, align: 'end' },
]

onMounted(async () => {
  try {
    const res = await api.get('/admin/scoring/overview')
    applications.value = res.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load scoring overview.' }
  } finally {
    loading.value = false
  }
})

function scoringProgress(app) {
  if (app.scoredCount === 0) return { label: 'No scores', color: 'text-slate-400' }
  return { label: `${app.scoredCount} scored`, color: 'text-success' }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Scoring Overview</h1>
      <p class="page-subtitle">Track scoring progress and averages across all submitted applications.</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <v-card v-if="!loading && !applications.length">
      <v-empty-state icon="mdi-clipboard-text-outline" text="No submitted applications yet." />
    </v-card>

    <v-card v-else class="overflow-hidden">
      <v-data-table
        v-model:expanded="expanded"
        :headers="headers"
        :items="applications"
        item-value="applicationId"
        :loading="loading"
        loading-text="Loading overview…"
        show-expand
        expand-on-click
        class="clickable-rows"
      >
        <template #item.applicant="{ item }">
          <p class="font-semibold text-navy">{{ item.firstName }} {{ item.lastName }}</p>
          <p class="text-xs text-slate-400">
            {{ item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : '—' }}
          </p>
        </template>
        <template #item.status="{ item }">
          <StatusBadge :status="item.status" />
        </template>
        <template #item.scoredCount="{ item }">
          <span :class="`text-sm font-medium ${scoringProgress(item).color}`">
            {{ scoringProgress(item).label }}
          </span>
        </template>
        <template #item.overallAverage="{ item }">
          <span v-if="item.overallAverage != null" class="text-sm font-bold text-navy">{{ item.overallAverage }}</span>
          <span v-else class="text-sm text-slate-400">—</span>
        </template>
        <template #item.actions="{ item }">
          <v-btn
            variant="text"
            color="primary"
            size="small"
            append-icon="mdi-arrow-right"
            @click.stop="router.push(`/admin/applications/${item.applicationId}`)"
          >
            View
          </v-btn>
        </template>

        <!-- Expanded section averages -->
        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="bg-[#F4F7FE] px-6 pb-4 pt-2">
              <div v-if="item.sectionAverages.length">
                <p class="text-xs font-bold text-slate-400 uppercase tracking-widest mb-3">Section Averages</p>
                <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                  <v-card
                    v-for="section in item.sectionAverages"
                    :key="section.sectionId ?? 'overall'"
                    rounded="lg"
                    class="px-4 py-3"
                  >
                    <p class="text-xs font-semibold text-slate-400 truncate mb-1">{{ section.sectionTitle }}</p>
                    <div class="flex items-end gap-2">
                      <span class="text-2xl font-bold text-navy">{{ section.average }}</span>
                      <span class="text-xs text-slate-400 mb-0.5">/ 100</span>
                    </div>
                    <p class="text-xs text-slate-400 mt-1">
                      {{ section.scorerCount }} scorer{{ section.scorerCount !== 1 ? 's' : '' }}
                    </p>
                  </v-card>
                </div>
              </div>
              <p v-else class="text-sm text-slate-400 py-2">No scores submitted yet.</p>
            </td>
          </tr>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>
