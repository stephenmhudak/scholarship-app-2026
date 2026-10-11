<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import api from '../../services/api'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import { useConfirm } from '../../composables/useConfirm'

const route = useRoute()
const confirm = useConfirm()
const authStore = useAuthStore()
const schoolId = route.params.id
const canManageStaff = authStore.hasPermission('manage_school_staff')

const school = ref(null)
const counselors = ref([])
const applicants = ref([])
const loading = ref(true)
const saving = ref(false)
const alert = ref(null)

function showAlert(type, message) {
  alert.value = { type, message }
  setTimeout(() => (alert.value = null), 4000)
}

// ── Applicants table ──────────────────────────────────────────────────────────
const applicantHeaders = [
  { title: 'Name', key: 'name', value: (a) => `${a.LastName ?? ''} ${a.FirstName ?? ''}` },
  { title: 'Email', key: 'Email' },
  { title: 'Status', key: 'Status' },
]

// ── Load ──────────────────────────────────────────────────────────────────────
onMounted(async () => {
  try {
    const [schoolRes, counselorRes, applicantRes] = await Promise.all([
      api.get(`/schools/${schoolId}`),
      api.get(`/schools/${schoolId}/counselors`),
      api.get(`/schools/${schoolId}/applicants`),
    ])
    school.value = { ...schoolRes.data }
    counselors.value = counselorRes.data
    applicants.value = applicantRes.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load school details.' }
  } finally {
    loading.value = false
  }
})

// ── Save school ───────────────────────────────────────────────────────────────
async function saveSchool() {
  saving.value = true
  try {
    await api.put(`/schools/${schoolId}`, {
      name: school.value.name,
      addressLine1: school.value.addressLine1,
      addressLine2: school.value.addressLine2,
      city: school.value.city,
      state: school.value.state,
      zip: school.value.zip,
    })
    showAlert('success', 'School updated successfully.')
  } catch {
    showAlert('error', 'Failed to update school.')
  } finally {
    saving.value = false
  }
}

// ── Add counselor ─────────────────────────────────────────────────────────────
const showAddCounselor = ref(false)
const addCounselorLoading = ref(false)
const addCounselorForm = ref({ firstName: '', lastName: '', email: '', password: '' })

async function submitAddCounselor() {
  if (!addCounselorForm.value.firstName || !addCounselorForm.value.lastName ||
      !addCounselorForm.value.email || !addCounselorForm.value.password) return

  addCounselorLoading.value = true
  try {
    const res = await api.post(`/schools/${schoolId}/counselors`, addCounselorForm.value)
    counselors.value.push(res.data)
    showAddCounselor.value = false
    addCounselorForm.value = { firstName: '', lastName: '', email: '', password: '' }
    showAlert('success', 'Counselor added.')
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to add counselor.')
  } finally {
    addCounselorLoading.value = false
  }
}

// ── Remove counselor ──────────────────────────────────────────────────────────
async function removeCounselor(counselor) {
  if (!await confirm({
    title: 'Remove counselor',
    text: `Remove ${counselor.FirstName} ${counselor.LastName} as a counselor?`,
    confirmText: 'Remove',
    color: 'error',
  })) return
  try {
    await api.delete(`/schools/${schoolId}/counselors/${counselor.Id}`)
    counselors.value = counselors.value.filter(c => c.Id !== counselor.Id)
    showAlert('success', 'Counselor removed.')
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to remove counselor.')
  }
}

// ── Reset counselor password ──────────────────────────────────────────────────
const showResetPassword = ref(false)
const resetTarget = ref(null)
const resetPasswordForm = ref({ newPassword: '', confirm: '' })
const resetPasswordLoading = ref(false)

function openResetPassword(counselor) {
  resetTarget.value = counselor
  resetPasswordForm.value = { newPassword: '', confirm: '' }
  showResetPassword.value = true
}

async function submitResetPassword() {
  const f = resetPasswordForm.value
  if (!f.newPassword) return
  if (f.newPassword !== f.confirm) { showAlert('error', 'Passwords do not match.'); return }

  resetPasswordLoading.value = true
  try {
    await api.post(`/schools/${schoolId}/counselors/${resetTarget.value.Id}/reset-password`, { newPassword: f.newPassword })
    showResetPassword.value = false
    showAlert('success', 'Password reset successfully.')
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to reset password.')
  } finally {
    resetPasswordLoading.value = false
  }
}
</script>

<template>
  <div class="space-y-6 max-w-4xl">
    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading school details…
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <template v-if="!loading && school">
      <!-- School Info -->
      <v-card class="pa-6 space-y-4">
        <div>
          <h1 class="page-title">{{ school.name }}</h1>
          <p class="page-subtitle">School information</p>
        </div>

        <div class="space-y-3">
          <v-text-field v-model="school.name" label="School Name" required hide-details="auto" />

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <v-text-field v-model="school.addressLine1" label="Address Line 1" placeholder="123 Main St" hide-details="auto" />
            <v-text-field v-model="school.addressLine2" label="Address Line 2" placeholder="Suite 100 (optional)" hide-details="auto" />
          </div>

          <div class="grid grid-cols-3 gap-3">
            <v-text-field v-model="school.city" label="City" placeholder="Springfield" hide-details="auto" />
            <v-text-field v-model="school.state" label="State" placeholder="IL" hide-details="auto" />
            <v-text-field v-model="school.zip" label="ZIP Code" placeholder="62701" hide-details="auto" />
          </div>
        </div>

        <v-btn color="primary" prepend-icon="mdi-content-save" :loading="saving" @click="saveSchool">
          Save Changes
        </v-btn>
      </v-card>

      <!-- Counselors (school_admin only) -->
      <v-card v-if="canManageStaff" class="pa-6 space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-base font-bold text-navy">Counselors</h2>
          <v-btn color="primary" size="small" prepend-icon="mdi-plus" @click="showAddCounselor = true">
            Add Counselor
          </v-btn>
        </div>

        <v-list v-if="counselors.length" class="py-0">
          <template v-for="(c, i) in counselors" :key="c.Id">
            <v-divider v-if="i > 0" />
            <v-list-item class="px-0" :title="`${c.FirstName} ${c.LastName}`" :subtitle="c.Email">
              <template #append>
                <v-btn variant="text" color="primary" size="small" @click="openResetPassword(c)">Reset Password</v-btn>
                <v-btn variant="text" color="error" size="small" @click="removeCounselor(c)">Remove</v-btn>
              </template>
            </v-list-item>
          </template>
        </v-list>
        <p v-else class="text-sm text-slate-400">No counselors assigned yet.</p>
      </v-card>

      <!-- Applicants -->
      <v-card class="pa-6 space-y-4">
        <h2 class="text-base font-bold text-navy">Applicants</h2>
        <v-card v-if="applicants.length" class="overflow-hidden">
          <v-data-table
            :headers="applicantHeaders"
            :items="applicants"
            :sort-by="[{ key: 'name', order: 'asc' }]"
          >
            <template #item.name="{ item }">
              <span class="font-semibold text-navy">{{ item.FirstName }} {{ item.LastName }}</span>
            </template>
            <template #item.Email="{ value }">
              <span class="text-slate-400">{{ value }}</span>
            </template>
            <template #item.Status="{ value }">
              <StatusBadge :status="value || 'draft'" />
            </template>
          </v-data-table>
        </v-card>
        <p v-else class="text-sm text-slate-400">No applicants from this school yet.</p>
      </v-card>
    </template>
  </div>

  <!-- Add Counselor Dialog -->
  <v-dialog v-model="showAddCounselor">
    <v-card title="Add Counselor">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="showAddCounselor = false" />
      </template>
      <v-card-text>
        <v-form class="space-y-4" @submit.prevent="submitAddCounselor">
          <div class="grid grid-cols-2 gap-3">
            <v-text-field v-model="addCounselorForm.firstName" label="First Name" required hide-details="auto" />
            <v-text-field v-model="addCounselorForm.lastName" label="Last Name" required hide-details="auto" />
          </div>
          <v-text-field v-model="addCounselorForm.email" type="email" label="Email" required hide-details="auto" />
          <v-text-field v-model="addCounselorForm.password" type="password" label="Password" required hide-details="auto" />
          <div class="flex justify-end gap-2 pt-2">
            <v-btn variant="text" @click="showAddCounselor = false">Cancel</v-btn>
            <v-btn type="submit" color="primary" :loading="addCounselorLoading">Add Counselor</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-dialog>

  <!-- Reset Password Dialog -->
  <v-dialog v-model="showResetPassword">
    <v-card :title="`Reset Password — ${resetTarget?.FirstName} ${resetTarget?.LastName}`">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="showResetPassword = false" />
      </template>
      <v-card-text>
        <v-form class="space-y-4" @submit.prevent="submitResetPassword">
          <v-text-field v-model="resetPasswordForm.newPassword" type="password" label="New Password" required hide-details="auto" />
          <v-text-field v-model="resetPasswordForm.confirm" type="password" label="Confirm Password" required hide-details="auto" />
          <div class="flex justify-end gap-2 pt-2">
            <v-btn variant="text" @click="showResetPassword = false">Cancel</v-btn>
            <v-btn type="submit" color="primary" :loading="resetPasswordLoading">Reset Password</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>
