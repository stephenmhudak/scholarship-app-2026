<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const students = ref([])
const loading = ref(true)
const alert = ref(null)
const nudging = ref({})

const headers = [
  { title: 'Student', key: 'name' },
  { title: 'Application Status', key: 'applicationStatus' },
  { title: 'Last Activity', key: 'lastActivity' },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' },
]

// No students is a normal, empty table; the error is only for a request that actually failed
onMounted(async () => {
  try {
    const response = await api.get('/counselor/applicants')
    students.value = response.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load student data.' }
  } finally {
    loading.value = false
  }
})

async function sendNudge(studentId) {
  nudging.value[studentId] = true
  try {
    const { data } = await api.post(`/counselor/nudge/${studentId}`)
    alert.value = { type: 'success', message: data.message || 'Reminder sent.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Failed to send reminder.' }
  } finally {
    nudging.value[studentId] = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Students</h1>
      <p class="page-subtitle">Track your students' scholarship application progress.</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <v-card class="overflow-hidden">
      <v-data-table
        :headers="headers"
        :items="students"
        :sort-by="[{ key: 'name', order: 'asc' }]"
        :loading="loading"
        loading-text="Loading students…"
        no-data-text="No students found in your school."
      >
        <template #item.name="{ item }">
          <p class="font-semibold text-navy">{{ item.name }}</p>
          <p class="text-xs text-slate-400">{{ item.email }}</p>
        </template>
        <template #item.applicationStatus="{ item }">
          <StatusBadge v-if="item.applicationStatus" :status="item.applicationStatus" />
          <v-chip v-else>Not started</v-chip>
        </template>
        <template #item.lastActivity="{ item }">
          <span class="text-slate-400">
            {{ item.lastActivity ? new Date(item.lastActivity).toLocaleDateString() : 'No activity' }}
          </span>
        </template>
        <template #item.actions="{ item }">
          <v-btn
            variant="text"
            color="primary"
            size="small"
            prepend-icon="mdi-bell-ring-outline"
            :loading="nudging[item.id]"
            @click="sendNudge(item.id)"
          >
            Send Reminder
          </v-btn>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>
