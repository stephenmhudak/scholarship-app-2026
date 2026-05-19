<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const route = useRoute()
const schoolId = route.params.id

const school = ref(null)
const counselors = ref([])
const applicants = ref([])
const loading = ref(true)
const saving = ref(false)
const inviting = ref(false)
const alert = ref(null)
const inviteEmail = ref('')

onMounted(async () => {
  try {
    const [schoolRes, counselorRes, applicantRes] = await Promise.all([
      api.get(`/schools/${schoolId}`),
      api.get(`/schools/${schoolId}/counselors`),
      api.get(`/schools/${schoolId}/applicants`),
    ])
    school.value = schoolRes.data
    counselors.value = counselorRes.data
    applicants.value = applicantRes.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load school details.' }
  } finally {
    loading.value = false
  }
})

async function saveSchool() {
  saving.value = true
  try {
    await api.put(`/schools/${schoolId}`, school.value)
    alert.value = { type: 'success', message: 'School updated successfully.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to update school.' }
  } finally {
    saving.value = false
  }
}

async function inviteCounselor() {
  if (!inviteEmail.value.trim()) return
  inviting.value = true
  try {
    const response = await api.post(`/schools/${schoolId}/counselors/invite`, { email: inviteEmail.value })
    counselors.value.push(response.data)
    inviteEmail.value = ''
    alert.value = { type: 'success', message: 'Counselor invitation sent.' }
  } catch {
    alert.value = { type: 'error', message: 'Failed to invite counselor.' }
  } finally {
    inviting.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl">
    <div class="flex items-center gap-3">
      <RouterLink to="/schools" class="text-sm text-blue-600 hover:underline">
        <span class="mdi mdi-arrow-left mr-1"></span>
        Back to Schools
      </RouterLink>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading school details...
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-else-if="school">
      <!-- School Info -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm space-y-4">
        <h1 class="text-xl font-bold text-gray-900">{{ school.name }}</h1>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div class="space-y-1">
            <label class="block text-sm font-medium text-gray-700">School Name</label>
            <input
              v-model="school.name"
              type="text"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div class="space-y-1">
            <label class="block text-sm font-medium text-gray-700">Address</label>
            <input
              v-model="school.address"
              type="text"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div class="space-y-1">
            <label class="block text-sm font-medium text-gray-700">City</label>
            <input
              v-model="school.city"
              type="text"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div class="space-y-1">
            <label class="block text-sm font-medium text-gray-700">State</label>
            <input
              v-model="school.state"
              type="text"
              class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>
        <BaseButton :loading="saving" @click="saveSchool">
          <span class="mdi mdi-content-save mr-1"></span>
          Save Changes
        </BaseButton>
      </div>

      <!-- Counselors -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm space-y-4">
        <h2 class="text-lg font-semibold text-gray-900">Counselors</h2>
        <div class="flex gap-2">
          <input
            v-model="inviteEmail"
            type="email"
            placeholder="counselor@school.edu"
            class="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
          <BaseButton size="sm" :loading="inviting" @click="inviteCounselor">
            Invite Counselor
          </BaseButton>
        </div>
        <ul v-if="counselors.length" class="divide-y divide-gray-100">
          <li v-for="c in counselors" :key="c.id" class="flex items-center justify-between py-2.5">
            <div>
              <p class="text-sm font-medium text-gray-900">{{ c.name }}</p>
              <p class="text-xs text-gray-500">{{ c.email }}</p>
            </div>
            <span class="text-xs px-2 py-0.5 bg-green-100 text-green-700 rounded-full">Active</span>
          </li>
        </ul>
        <p v-else class="text-sm text-gray-500">No counselors assigned yet.</p>
      </div>

      <!-- Applicants -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm space-y-4">
        <h2 class="text-lg font-semibold text-gray-900">Applicants</h2>
        <table v-if="applicants.length" class="min-w-full divide-y divide-gray-200">
          <thead class="bg-gray-50">
            <tr>
              <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
              <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Email</th>
              <th class="px-4 py-2 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-gray-100">
            <tr v-for="a in applicants" :key="a.id">
              <td class="px-4 py-3 text-sm font-medium text-gray-900">{{ a.name }}</td>
              <td class="px-4 py-3 text-sm text-gray-500">{{ a.email }}</td>
              <td class="px-4 py-3"><StatusBadge :status="a.application_status || 'draft'" /></td>
            </tr>
          </tbody>
        </table>
        <p v-else class="text-sm text-gray-500">No applicants from this school yet.</p>
      </div>
    </template>
  </div>
</template>
