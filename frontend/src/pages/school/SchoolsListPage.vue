<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import { useTableSort } from '../../composables/useTableSort'
import SortTh from '../../components/common/SortTh.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()

const schools = ref([])
const loading = ref(true)
const alert = ref(null)

const { sortKey, sortDir, setSort, applySort } = useTableSort('name')

const sortedSchools = computed(() => applySort(schools.value, {
  name: (s) => (s.name ?? '').toLowerCase(),
  location: (s) => `${s.city ?? ''} ${s.state ?? ''}`.toLowerCase().trim(),
  counselors: (s) => s.counselors_count ?? -1,
  applicants: (s) => s.applicants_count ?? -1,
}))

onMounted(async () => {
  loading.value = true
  try {
    const response = await api.get('/schools')
    schools.value = response.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load schools.' }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Schools</h1>
      <p class="page-subtitle">Manage school records and counselors.</p>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading schools…
    </div>

    <div v-else class="card overflow-hidden">
      <table class="dialect-table">
        <thead>
          <tr>
            <SortTh column="name" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">School Name</SortTh>
            <SortTh column="location" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Location</SortTh>
            <SortTh column="counselors" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Counselors</SortTh>
            <SortTh column="applicants" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Applicants</SortTh>
            <th class="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="school in sortedSchools"
            :key="school.id"
            @click="router.push(`/schools/${school.id}`)"
          >
            <td>
              <p class="font-semibold text-navy">{{ school.name }}</p>
            </td>
            <td class="text-slate-400">
              {{ [school.city, school.state].filter(Boolean).join(', ') || '—' }}
            </td>
            <td class="text-slate-400">{{ school.counselors_count ?? '—' }}</td>
            <td class="text-slate-400">{{ school.applicants_count ?? '—' }}</td>
            <td class="text-right">
              <button
                @click.stop="router.push(`/schools/${school.id}`)"
                class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors"
              >
                Manage
              </button>
            </td>
          </tr>
          <tr v-if="schools.length === 0">
            <td colspan="5" class="px-6 py-10 text-center text-sm text-slate-400">No schools found.</td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
