<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const students = ref([])
const loading = ref(true)
const alert = ref(null)
const nudging = ref({})

onMounted(async () => {
  try {
    const response = await api.get('/counselor/students')
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
    await api.post(`/counselor/students/${studentId}/nudge`)
    alert.value = { type: 'success', message: 'Reminder sent successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to send reminder.' }
  } finally {
    nudging.value[studentId] = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Counselor Dashboard</h1>
      <p class="page-subtitle">Track your students' scholarship application progress.</p>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading students…
    </div>

    <div v-else class="card overflow-hidden">
      <table class="dialect-table">
        <thead>
          <tr>
            <th>Student</th>
            <th>Application Status</th>
            <th>Last Activity</th>
            <th class="text-right">Actions</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="student in students" :key="student.id">
            <td>
              <p class="font-semibold text-navy">{{ student.name }}</p>
              <p class="text-xs text-slate-400">{{ student.email }}</p>
            </td>
            <td>
              <StatusBadge :status="student.application_status || 'draft'" />
            </td>
            <td class="text-slate-400">
              {{ student.last_activity ? new Date(student.last_activity).toLocaleDateString() : 'No activity' }}
            </td>
            <td class="text-right">
              <button
                @click="sendNudge(student.id)"
                :disabled="nudging[student.id]"
                class="inline-flex items-center gap-1.5 text-sm font-semibold text-primary hover:text-primary-700 transition-colors disabled:opacity-50"
              >
                <span v-if="nudging[student.id]" class="mdi mdi-loading animate-spin"></span>
                <span v-else class="mdi mdi-bell-ring-outline"></span>
                Send Reminder
              </button>
            </td>
          </tr>
          <tr v-if="students.length === 0">
            <td colspan="4" class="px-6 py-10 text-center text-sm text-slate-400">
              No students found in your school.
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
