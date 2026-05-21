<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import { useTableSort } from '../../composables/useTableSort'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import SortTh from '../../components/common/SortTh.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()
const loading = ref(true)
const alert = ref(null)
const applications = ref([])
const expanded = ref(new Set())

const { sortKey, sortDir, setSort, applySort } = useTableSort('submitted', 'desc')

const sortedApplications = computed(() => applySort(applications.value, {
  applicant: (a) => `${a.lastName} ${a.firstName}`.toLowerCase(),
  status: (a) => a.status,
  scorers: (a) => a.scoredCount / (a.assignedCount || 1),
  avgScore: (a) => a.overallAverage ?? -1,
  submitted: (a) => a.submittedAt ?? '',
}))

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

function toggleExpand(id) {
  if (expanded.value.has(id)) {
    expanded.value.delete(id)
  } else {
    expanded.value.add(id)
  }
}

function scoringProgress(app) {
  if (!app.assignedCount) return { label: 'No scorers', color: 'text-slate-400' }
  if (app.scoredCount === 0) return { label: `0 / ${app.assignedCount} scored`, color: 'text-danger' }
  if (app.scoredCount < app.assignedCount) return { label: `${app.scoredCount} / ${app.assignedCount} scored`, color: 'text-[#8C6500]' }
  return { label: `${app.scoredCount} / ${app.assignedCount} scored`, color: 'text-success-dark' }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Scoring Overview</h1>
      <p class="page-subtitle">Track scoring progress and averages across all submitted applications.</p>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading overview…
    </div>

    <div v-else-if="!applications.length" class="section-card text-center py-10">
      <span class="mdi mdi-clipboard-text-outline text-4xl text-slate-400 block mb-2"></span>
      <p class="text-sm text-slate-400">No submitted applications yet.</p>
    </div>

    <div v-else class="card overflow-hidden">
      <table class="dialect-table">
        <thead>
          <tr>
            <th class="w-8"></th>
            <SortTh column="applicant" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Applicant</SortTh>
            <SortTh column="status" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Status</SortTh>
            <SortTh column="scorers" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Scorers</SortTh>
            <SortTh column="avgScore" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Avg Score</SortTh>
            <th class="text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <template v-for="app in sortedApplications" :key="app.applicationId">
            <tr
              class="cursor-pointer"
              @click="toggleExpand(app.applicationId)"
            >
              <td>
                <span
                  :class="`mdi text-slate-400 text-base transition-transform ${expanded.has(app.applicationId) ? 'mdi-chevron-down' : 'mdi-chevron-right'}`"
                ></span>
              </td>
              <td>
                <p class="font-semibold text-navy">{{ app.firstName }} {{ app.lastName }}</p>
                <p class="text-xs text-slate-400">
                  {{ app.submittedAt ? new Date(app.submittedAt).toLocaleDateString() : '—' }}
                </p>
              </td>
              <td><StatusBadge :status="app.status" /></td>
              <td>
                <span :class="`text-sm font-medium ${scoringProgress(app).color}`">
                  {{ scoringProgress(app).label }}
                </span>
              </td>
              <td>
                <span v-if="app.overallAverage != null" class="text-sm font-bold text-navy">
                  {{ app.overallAverage }}
                </span>
                <span v-else class="text-sm text-slate-400">—</span>
              </td>
              <td class="text-right" @click.stop>
                <button
                  @click="router.push(`/admin/applications/${app.applicationId}`)"
                  class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors"
                >
                  View
                  <span class="mdi mdi-arrow-right ml-0.5"></span>
                </button>
              </td>
            </tr>

            <!-- Expanded section averages -->
            <tr v-if="expanded.has(app.applicationId)" class="bg-[#F4F7FE]">
              <td colspan="6" class="px-6 pb-4 pt-2">
                <div v-if="app.sectionAverages.length" class="space-y-2">
                  <p class="text-xs font-bold text-slate-400 uppercase tracking-widest mb-3">Section Averages</p>
                  <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                    <div
                      v-for="section in app.sectionAverages"
                      :key="section.sectionId ?? 'overall'"
                      class="bg-white rounded-xl border border-[#E9EDF7] px-4 py-3 shadow-card"
                    >
                      <p class="text-xs font-semibold text-slate-400 truncate mb-1">{{ section.sectionTitle }}</p>
                      <div class="flex items-end gap-2">
                        <span class="text-2xl font-bold text-navy">{{ section.average }}</span>
                        <span class="text-xs text-slate-400 mb-0.5">/ 100</span>
                      </div>
                      <p class="text-xs text-slate-400 mt-1">
                        {{ section.scorerCount }} scorer{{ section.scorerCount !== 1 ? 's' : '' }}
                      </p>
                    </div>
                  </div>
                </div>
                <p v-else class="text-sm text-slate-400 py-2">No scores submitted yet.</p>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </div>
  </div>
</template>
