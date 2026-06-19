<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import api from '../../services/api'
import { useTableSort } from '../../composables/useTableSort'
import SortTh from '../../components/common/SortTh.vue'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseInput from '../../components/common/BaseInput.vue'
import BaseModal from '../../components/common/BaseModal.vue'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const route = useRoute()
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

// ── Applicants sort ───────────────────────────────────────────────────────────
const { sortKey, sortDir, setSort, applySort } = useTableSort('LastName')
const sortedApplicants = computed(() => applySort(applicants.value, {
  LastName: (a) => `${a.LastName ?? ''} ${a.FirstName ?? ''}`.toLowerCase(),
  Email: (a) => (a.Email ?? '').toLowerCase(),
  Status: (a) => a.Status ?? '',
}))

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
  if (!confirm(`Remove ${counselor.FirstName} ${counselor.LastName} as a counselor?`)) return
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
    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading school details…
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <template v-if="!loading && school">
      <!-- School Info -->
      <div class="section-card space-y-4">
        <div>
          <h1 class="page-title">{{ school.name }}</h1>
          <p class="page-subtitle">School information</p>
        </div>

        <div class="space-y-3">
          <BaseInput v-model="school.name" label="School Name" :required="true" />

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <BaseInput v-model="school.addressLine1" label="Address Line 1" placeholder="123 Main St" />
            <BaseInput v-model="school.addressLine2" label="Address Line 2" placeholder="Suite 100 (optional)" />
          </div>

          <div class="grid grid-cols-3 gap-3">
            <BaseInput v-model="school.city" label="City" placeholder="Springfield" class="col-span-1" />
            <BaseInput v-model="school.state" label="State" placeholder="IL" />
            <BaseInput v-model="school.zip" label="ZIP Code" placeholder="62701" />
          </div>
        </div>

        <BaseButton :loading="saving" @click="saveSchool">
          <span class="mdi mdi-content-save mr-1.5"></span>
          Save Changes
        </BaseButton>
      </div>

      <!-- Counselors (school_admin only) -->
      <div v-if="canManageStaff" class="section-card space-y-4">
        <div class="flex items-center justify-between">
          <h2 class="text-base font-bold text-navy">Counselors</h2>
          <BaseButton size="sm" @click="showAddCounselor = true">
            <span class="mdi mdi-plus mr-1"></span>
            Add Counselor
          </BaseButton>
        </div>

        <ul v-if="counselors.length" class="divide-y divide-[#E9EDF7]">
          <li v-for="c in counselors" :key="c.Id" class="flex items-center justify-between py-3">
            <div>
              <p class="text-sm font-semibold text-navy">{{ c.FirstName }} {{ c.LastName }}</p>
              <p class="text-xs text-slate-400">{{ c.Email }}</p>
            </div>
            <div class="flex items-center gap-3">
              <button @click="openResetPassword(c)" class="text-xs text-primary hover:underline">Reset Password</button>
              <button @click="removeCounselor(c)" class="text-xs text-danger hover:underline">Remove</button>
            </div>
          </li>
        </ul>
        <p v-else class="text-sm text-slate-400">No counselors assigned yet.</p>
      </div>

      <!-- Applicants -->
      <div class="section-card space-y-4">
        <h2 class="text-base font-bold text-navy">Applicants</h2>
        <div v-if="applicants.length" class="card overflow-hidden">
          <table class="dialect-table">
            <thead>
              <tr>
                <SortTh column="LastName" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Name</SortTh>
                <SortTh column="Email" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Email</SortTh>
                <SortTh column="Status" :sort-key="sortKey" :sort-dir="sortDir" @sort="setSort">Status</SortTh>
              </tr>
            </thead>
            <tbody>
              <tr v-for="a in sortedApplicants" :key="a.Id">
                <td class="font-semibold text-navy">{{ a.FirstName }} {{ a.LastName }}</td>
                <td class="text-slate-400">{{ a.Email }}</td>
                <td><StatusBadge :status="a.Status || 'draft'" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="text-sm text-slate-400">No applicants from this school yet.</p>
      </div>
    </template>
  </div>

  <!-- Add Counselor Modal -->
  <BaseModal :show="showAddCounselor" title="Add Counselor" @close="showAddCounselor = false">
    <form @submit.prevent="submitAddCounselor" class="space-y-4">
      <div class="grid grid-cols-2 gap-3">
        <BaseInput v-model="addCounselorForm.firstName" label="First Name" :required="true" />
        <BaseInput v-model="addCounselorForm.lastName" label="Last Name" :required="true" />
      </div>
      <BaseInput v-model="addCounselorForm.email" type="email" label="Email" :required="true" />
      <BaseInput v-model="addCounselorForm.password" type="password" label="Password" :required="true" />
      <div class="flex justify-end gap-2 pt-2">
        <BaseButton type="button" variant="ghost" @click="showAddCounselor = false">Cancel</BaseButton>
        <BaseButton type="submit" :loading="addCounselorLoading">Add Counselor</BaseButton>
      </div>
    </form>
  </BaseModal>

  <!-- Reset Password Modal -->
  <BaseModal
    :show="showResetPassword"
    :title="`Reset Password — ${resetTarget?.FirstName} ${resetTarget?.LastName}`"
    @close="showResetPassword = false"
  >
    <form @submit.prevent="submitResetPassword" class="space-y-4">
      <BaseInput v-model="resetPasswordForm.newPassword" type="password" label="New Password" :required="true" />
      <BaseInput v-model="resetPasswordForm.confirm" type="password" label="Confirm Password" :required="true" />
      <div class="flex justify-end gap-2 pt-2">
        <BaseButton type="button" variant="ghost" @click="showResetPassword = false">Cancel</BaseButton>
        <BaseButton type="submit" :loading="resetPasswordLoading">Reset Password</BaseButton>
      </div>
    </form>
  </BaseModal>
</template>
