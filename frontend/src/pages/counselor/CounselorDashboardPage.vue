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
      <h1 class="text-2xl font-bold text-gray-900">Counselor Dashboard</h1>
      <p class="text-gray-500 mt-1">Track your students' scholarship application progress.</p>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading students...
    </div>

    <div v-else class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Student</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Application Status</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Last Activity</th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
          </tr>
        </thead>
        <tbody class="bg-white divide-y divide-gray-100">
          <tr v-for="student in students" :key="student.id">
            <td class="px-6 py-4">
              <div>
                <p class="text-sm font-medium text-gray-900">{{ student.name }}</p>
                <p class="text-xs text-gray-500">{{ student.email }}</p>
              </div>
            </td>
            <td class="px-6 py-4">
              <StatusBadge :status="student.application_status || 'draft'" />
            </td>
            <td class="px-6 py-4 text-sm text-gray-500">
              {{ student.last_activity ? new Date(student.last_activity).toLocaleDateString() : 'No activity' }}
            </td>
            <td class="px-6 py-4 text-right">
              <button
                @click="sendNudge(student.id)"
                :disabled="nudging[student.id]"
                class="inline-flex items-center gap-1 text-sm text-blue-600 hover:text-blue-700 font-medium disabled:opacity-50"
              >
                <span v-if="nudging[student.id]" class="mdi mdi-loading animate-spin"></span>
                <span v-else class="mdi mdi-bell-ring"></span>
                Send Reminder
              </button>
            </td>
          </tr>
          <tr v-if="students.length === 0">
            <td colspan="4" class="px-6 py-8 text-center text-sm text-gray-500">
              No students found in your school.
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
