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
    <RouterLink to="/schools" class="inline-flex items-center text-sm font-semibold text-primary hover:text-primary-700 transition-colors">
      <span class="mdi mdi-arrow-left mr-1"></span>
      Back to Schools
    </RouterLink>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading school details…
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-else-if="school">
      <!-- School Info -->
      <div class="section-card space-y-4">
        <h1 class="text-xl font-bold text-navy">{{ school.name }}</h1>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <label class="block text-sm font-semibold text-navy">School Name</label>
            <input v-model="school.name" type="text" class="form-input" />
          </div>
          <div class="space-y-1.5">
            <label class="block text-sm font-semibold text-navy">Address</label>
            <input v-model="school.address" type="text" class="form-input" />
          </div>
          <div class="space-y-1.5">
            <label class="block text-sm font-semibold text-navy">City</label>
            <input v-model="school.city" type="text" class="form-input" />
          </div>
          <div class="space-y-1.5">
            <label class="block text-sm font-semibold text-navy">State</label>
            <input v-model="school.state" type="text" class="form-input" />
          </div>
        </div>
        <BaseButton :loading="saving" @click="saveSchool">
          <span class="mdi mdi-content-save mr-1.5"></span>
          Save Changes
        </BaseButton>
      </div>

      <!-- Counselors -->
      <div class="section-card space-y-4">
        <h2 class="text-base font-bold text-navy">Counselors</h2>
        <div class="flex gap-2">
          <input
            v-model="inviteEmail"
            type="email"
            placeholder="counselor@school.edu"
            class="form-input flex-1"
          />
          <BaseButton size="sm" :loading="inviting" @click="inviteCounselor">
            Invite Counselor
          </BaseButton>
        </div>
        <ul v-if="counselors.length" class="divide-y divide-[#E9EDF7]">
          <li v-for="c in counselors" :key="c.id" class="flex items-center justify-between py-2.5">
            <div>
              <p class="text-sm font-semibold text-navy">{{ c.name }}</p>
              <p class="text-xs text-slate-400">{{ c.email }}</p>
            </div>
            <span class="text-xs px-2.5 py-0.5 bg-success-light text-success-dark border border-success/20 rounded-full font-semibold">Active</span>
          </li>
        </ul>
        <p v-else class="text-sm text-slate-400">No counselors assigned yet.</p>
      </div>

      <!-- Applicants -->
      <div class="section-card space-y-4">
        <h2 class="text-base font-bold text-navy">Applicants</h2>
        <table v-if="applicants.length" class="dialect-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="a in applicants" :key="a.id">
              <td class="font-semibold text-navy">{{ a.name }}</td>
              <td class="text-slate-400">{{ a.email }}</td>
              <td><StatusBadge :status="a.application_status || 'draft'" /></td>
            </tr>
          </tbody>
        </table>
        <p v-else class="text-sm text-slate-400">No applicants from this school yet.</p>
      </div>
    </template>
  </div>
</template>
