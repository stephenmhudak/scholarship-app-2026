<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'

const router = useRouter()

const schools = ref([])
const loading = ref(true)
const alert = ref(null)

const headers = [
  { title: 'School Name', key: 'name' },
  { title: 'Location', key: 'location', value: (s) => [s.city, s.state].filter(Boolean).join(', ') },
  { title: 'Counselors', key: 'counselors_count' },
  { title: 'Applicants', key: 'applicants_count' },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' },
]

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

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <v-card class="overflow-hidden">
      <v-data-table
        :headers="headers"
        :items="schools"
        :sort-by="[{ key: 'name', order: 'asc' }]"
        :loading="loading"
        loading-text="Loading schools…"
        no-data-text="No schools found."
        class="clickable-rows"
        @click:row="(e, { item }) => router.push(`/schools/${item.id}`)"
      >
        <template #item.name="{ item }">
          <span class="font-semibold text-navy">{{ item.name }}</span>
        </template>
        <template #item.location="{ value }">
          <span class="text-slate-400">{{ value || '—' }}</span>
        </template>
        <template #item.counselors_count="{ value }">
          <span class="text-slate-400">{{ value ?? '—' }}</span>
        </template>
        <template #item.applicants_count="{ value }">
          <span class="text-slate-400">{{ value ?? '—' }}</span>
        </template>
        <template #item.actions="{ item }">
          <v-btn variant="text" color="primary" size="small" @click.stop="router.push(`/schools/${item.id}`)">
            Manage
          </v-btn>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>
