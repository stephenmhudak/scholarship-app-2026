<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import api from '../../services/api'
import { useConfirm } from '../../composables/useConfirm'

const confirm = useConfirm()

const activeTab = ref('users')

// ── Shared ──────────────────────────────────────────────────────────────────
const alert = ref(null)
function showAlert(type, message) {
  alert.value = { type, message }
  setTimeout(() => (alert.value = null), 4000)
}

// ── Users ────────────────────────────────────────────────────────────────────
const ROLES = [
  { title: 'Applicant', value: 'applicant' },
  { title: 'Scorer', value: 'scorer' },
  { title: 'App Admin', value: 'app_admin' },
  { title: 'School Admin', value: 'school_admin' },
  { title: 'Counselor', value: 'counselor' },
  { title: 'Referee', value: 'referee' },
]

const userHeaders = [
  { title: 'Name', key: 'name', value: (u) => `${u.FirstName} ${u.LastName}` },
  { title: 'Email', key: 'Email' },
  { title: 'Role', key: 'Role' },
  { title: 'School', key: 'SchoolName' },
  { title: '2FA', key: 'TwoFactorEnabled', value: (u) => !!u.TwoFactorEnabled },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

const users = ref([])
const usersLoading = ref(false)
const showAddUser = ref(false)
const addUserLoading = ref(false)
const addUserForm = ref({ firstName: '', lastName: '', email: '', password: '', role: 'applicant', schoolId: '' })
const addUserErrors = ref({})

const showResetPassword = ref(false)
const resetUser = ref(null)
const resetPasswordForm = ref({ newPassword: '', confirm: '' })
const resetPasswordLoading = ref(false)

async function loadUsers() {
  usersLoading.value = true
  try {
    const res = await api.get('/admin/users')
    users.value = res.data
  } catch {
    showAlert('error', 'Failed to load users.')
  } finally {
    usersLoading.value = false
  }
}

function openAddUser() {
  addUserForm.value = { firstName: '', lastName: '', email: '', password: '', role: 'applicant', schoolId: '' }
  addUserErrors.value = {}
  showAddUser.value = true
}

async function submitAddUser() {
  addUserErrors.value = {}
  const f = addUserForm.value
  if (!f.firstName) addUserErrors.value.firstName = 'Required'
  if (!f.lastName) addUserErrors.value.lastName = 'Required'
  if (!f.email) addUserErrors.value.email = 'Required'
  // Counselors get an email to create their own password instead
  if (f.role !== 'counselor' && !f.password) addUserErrors.value.password = 'Required'
  if (Object.keys(addUserErrors.value).length) return

  addUserLoading.value = true
  try {
    const { data } = await api.post('/admin/users', {
      firstName: f.firstName,
      lastName: f.lastName,
      email: f.email,
      password: f.role === 'counselor' ? null : f.password,
      role: f.role,
      schoolId: f.schoolId || null,
    })
    showAddUser.value = false
    if (f.role !== 'counselor') showAlert('success', 'User created.')
    else if (data.emailSent) showAlert('success', `Counselor created. We emailed ${f.email} a link to create their password.`)
    else showAlert('warning', 'Counselor created, but the invitation email couldn\'t be sent. Use Resend Invite to try again.')
    await loadUsers()
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to create user.')
  } finally {
    addUserLoading.value = false
  }
}

async function resendInvite(user) {
  try {
    await api.post(`/admin/users/${user.Id}/resend-invite`)
    showAlert('success', `Invitation sent to ${user.Email}.`)
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to resend the invitation.')
  }
}

function openResetPassword(user) {
  resetUser.value = user
  resetPasswordForm.value = { newPassword: '', confirm: '' }
  showResetPassword.value = true
}

async function submitResetPassword() {
  const f = resetPasswordForm.value
  if (!f.newPassword) return
  if (f.newPassword !== f.confirm) {
    showAlert('error', 'Passwords do not match.')
    return
  }
  resetPasswordLoading.value = true
  try {
    await api.post(`/admin/users/${resetUser.value.Id}/reset-password`, { newPassword: f.newPassword })
    showResetPassword.value = false
    showAlert('success', 'Password reset successfully.')
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to reset password.')
  } finally {
    resetPasswordLoading.value = false
  }
}

async function resetTwoFactor(user) {
  if (!await confirm({
    title: 'Reset two-factor?',
    text: `${user.FirstName} ${user.LastName} will be able to sign in with just their password, and can set up two-factor again from their Account page. Only do this after confirming their identity.`,
    confirmText: 'Reset 2FA',
    color: 'error',
  })) return
  try {
    await api.post(`/admin/users/${user.Id}/reset-2fa`)
    user.TwoFactorEnabled = false
    showAlert('success', 'Two-factor authentication reset.')
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to reset two-factor.')
  }
}

// ── Schools ──────────────────────────────────────────────────────────────────
const schools = ref([])
const schoolItems = computed(() => [
  { title: '— No School —', value: '' },
  ...schools.value.map((s) => ({ title: s.name, value: s.id })),
])
const schoolsLoading = ref(false)
const editingSchool = ref(null)
const schoolSaving = ref(false)

async function loadSchools() {
  schoolsLoading.value = true
  try {
    const res = await api.get('/admin/schools')
    schools.value = res.data
  } catch {
    showAlert('error', 'Failed to load schools.')
  } finally {
    schoolsLoading.value = false
  }
}

function startEditSchool(school) {
  editingSchool.value = {
    id: school.id,
    name: school.name,
    addressLine1: school.addressLine1 ?? '',
    addressLine2: school.addressLine2 ?? '',
    city: school.city ?? '',
    state: school.state ?? '',
    zip: school.zip ?? '',
  }
}

function cancelEditSchool() {
  editingSchool.value = null
}

async function saveSchool() {
  if (!editingSchool.value) return
  schoolSaving.value = true
  try {
    await api.put(`/admin/schools/${editingSchool.value.id}`, {
      name: editingSchool.value.name,
      addressLine1: editingSchool.value.addressLine1,
      addressLine2: editingSchool.value.addressLine2,
      city: editingSchool.value.city,
      state: editingSchool.value.state,
      zip: editingSchool.value.zip,
    })
    showAlert('success', 'School updated.')
    editingSchool.value = null
    await loadSchools()
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to save school.')
  } finally {
    schoolSaving.value = false
  }
}

// ── Invites ───────────────────────────────────────────────────────────────────
const inviteHeaders = [
  { title: 'School', key: 'SchoolName' },
  { title: 'Created', key: 'CreatedAt' },
  { title: 'Expires', key: 'ExpiresAt' },
  { title: 'Used', key: 'UsedAt' },
]

const invites = ref([])
const invitesLoading = ref(false)
const generatingInvite = ref(false)
const newInviteLink = ref(null)

async function loadInvites() {
  invitesLoading.value = true
  try {
    const res = await api.get('/admin/invites')
    invites.value = res.data
  } catch {
    showAlert('error', 'Failed to load invites.')
  } finally {
    invitesLoading.value = false
  }
}

async function generateInvite() {
  generatingInvite.value = true
  newInviteLink.value = null
  try {
    const res = await api.post('/admin/invites')
    const token = res.data.token
    newInviteLink.value = { token, url: `${window.location.origin}/register?invite=${token}` }
    await loadInvites()
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to generate invite.')
  } finally {
    generatingInvite.value = false
  }
}

function copyLink() {
  if (newInviteLink.value) {
    navigator.clipboard.writeText(newInviteLink.value.url)
    showAlert('success', 'Link copied to clipboard.')
  }
}

function formatDate(d) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString()
}

// ── Tabs ──────────────────────────────────────────────────────────────────────
watch(activeTab, (tab) => {
  if (tab === 'invites') loadInvites()
})

onMounted(() => { loadUsers(); loadSchools() })
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Settings</h1>
      <p class="page-subtitle">Manage users, schools, and registration links</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Tabs -->
    <v-tabs v-model="activeTab" color="primary" class="border-b">
      <v-tab value="users" class="text-none">Users</v-tab>
      <v-tab value="schools" class="text-none">Schools</v-tab>
      <v-tab value="invites" class="text-none">School Admin Invites</v-tab>
    </v-tabs>

    <!-- ── Users Tab ─────────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'users'" class="space-y-4">
      <div class="flex justify-between items-center">
        <p class="text-sm text-slate-400">{{ users.length }} user{{ users.length !== 1 ? 's' : '' }}</p>
        <v-btn color="primary" size="small" prepend-icon="mdi-plus" @click="openAddUser">Add User</v-btn>
      </div>

      <v-card class="overflow-hidden">
        <v-data-table
          :headers="userHeaders"
          :items="users"
          :loading="usersLoading"
          no-data-text="No users found."
        >
          <template #item.name="{ value }">
            <span class="font-medium text-navy">{{ value }}</span>
          </template>
          <template #item.Email="{ value }">
            <span class="text-slate-400">{{ value }}</span>
          </template>
          <template #item.Role="{ value }">
            <v-chip color="primary">{{ value }}</v-chip>
          </template>
          <template #item.SchoolName="{ value }">
            <span class="text-slate-400">{{ value ?? '—' }}</span>
          </template>
          <template #item.TwoFactorEnabled="{ value }">
            <v-chip v-if="value" color="success">On</v-chip>
            <span v-else class="text-slate-400">—</span>
          </template>
          <template #item.actions="{ item }">
            <template v-if="item.InvitePending">
              <v-chip color="warning" class="mr-2">Invite pending</v-chip>
              <v-btn variant="text" color="primary" size="small" @click="resendInvite(item)">Resend Invite</v-btn>
            </template>
            <v-btn v-else variant="text" color="primary" size="small" @click="openResetPassword(item)">Reset Password</v-btn>
            <v-btn v-if="item.TwoFactorEnabled" variant="text" color="error" size="small" @click="resetTwoFactor(item)">Reset 2FA</v-btn>
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ── Schools Tab ───────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'schools'" class="space-y-4">
      <div v-if="schoolsLoading" class="flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <div v-else class="space-y-3">
        <v-card v-if="!schools.length" class="pa-6 text-slate-400 text-sm text-center">No schools found.</v-card>

        <v-card v-for="school in schools" :key="school.id" class="pa-6">
          <div v-if="editingSchool?.id === school.id" class="space-y-3">
            <v-text-field v-model="editingSchool.name" label="School Name" required hide-details="auto" />
            <div class="grid grid-cols-2 gap-3">
              <v-text-field v-model="editingSchool.addressLine1" label="Address Line 1" hide-details="auto" />
              <v-text-field v-model="editingSchool.addressLine2" label="Address Line 2" hide-details="auto" />
            </div>
            <div class="grid grid-cols-3 gap-3">
              <v-text-field v-model="editingSchool.city" label="City" hide-details="auto" />
              <v-text-field v-model="editingSchool.state" label="State" hide-details="auto" />
              <v-text-field v-model="editingSchool.zip" label="ZIP" hide-details="auto" />
            </div>
            <div class="flex gap-2">
              <v-btn color="primary" size="small" :loading="schoolSaving" @click="saveSchool">Save</v-btn>
              <v-btn variant="text" size="small" @click="cancelEditSchool">Cancel</v-btn>
            </div>
          </div>
          <div v-else class="flex items-start justify-between">
            <div>
              <p class="font-semibold text-navy">{{ school.name }}</p>
              <p class="text-sm text-slate-400">
                {{ [school.addressLine1, school.city, school.state, school.zip].filter(Boolean).join(', ') || 'No address set' }}
              </p>
            </div>
            <v-btn variant="outlined" size="small" @click="startEditSchool(school)">Edit</v-btn>
          </div>
        </v-card>
      </div>
    </div>

    <!-- ── Invites Tab ───────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'invites'" class="space-y-6">

      <!-- Generate invite -->
      <v-card title="Generate Registration Link">
        <v-card-text class="pa-6 flex items-center justify-between gap-4">
          <p class="text-sm text-slate-400">The school admin will enter their school information when they register. Valid for 7 days.</p>
          <v-btn color="primary" size="small" :loading="generatingInvite" @click="generateInvite">Generate Link</v-btn>
        </v-card-text>
      </v-card>

      <!-- New invite link result -->
      <v-alert v-if="newInviteLink" type="success" title="Link ready — share with the school admin">
        <div class="flex items-center gap-2 mt-2">
          <v-text-field
            :model-value="newInviteLink.url"
            readonly
            density="compact"
            bg-color="surface"
            hide-details
            class="flex-1 font-mono"
          />
          <v-btn variant="outlined" prepend-icon="mdi-content-copy" @click="copyLink">Copy</v-btn>
        </div>
      </v-alert>

      <!-- Existing invites -->
      <v-card class="overflow-hidden" title="Invite History">
        <v-data-table
          :headers="inviteHeaders"
          :items="invites"
          :loading="invitesLoading"
          no-data-text="No invites yet."
        >
          <template #item.SchoolName="{ value }">
            <span class="font-medium text-navy">{{ value }}</span>
          </template>
          <template #item.CreatedAt="{ value }">
            <span class="text-slate-400">{{ formatDate(value) }}</span>
          </template>
          <template #item.ExpiresAt="{ value }">
            <span class="text-slate-400">{{ formatDate(value) }}</span>
          </template>
          <template #item.UsedAt="{ value }">
            <v-chip :color="value ? 'success' : 'warning'">{{ value ? 'Used' : 'Pending' }}</v-chip>
          </template>
        </v-data-table>
      </v-card>
    </div>
  </div>

  <!-- ── Add User Dialog ─────────────────────────────────────────────────── -->
  <v-dialog v-model="showAddUser">
    <v-card title="Add User">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="showAddUser = false" />
      </template>
      <v-card-text>
        <v-form class="space-y-4" @submit.prevent="submitAddUser">
          <div class="grid grid-cols-2 gap-3">
            <v-text-field v-model="addUserForm.firstName" label="First Name" :error-messages="addUserErrors.firstName || []" hide-details="auto" />
            <v-text-field v-model="addUserForm.lastName" label="Last Name" :error-messages="addUserErrors.lastName || []" hide-details="auto" />
          </div>
          <v-text-field v-model="addUserForm.email" type="email" label="Email" :error-messages="addUserErrors.email || []" hide-details="auto" />
          <v-text-field v-if="addUserForm.role !== 'counselor'" v-model="addUserForm.password" type="password" label="Password" :error-messages="addUserErrors.password || []" hide-details="auto" />

          <v-select v-model="addUserForm.role" :items="ROLES" label="Role" hide-details />
          <p v-if="addUserForm.role === 'counselor'" class="text-sm text-slate-400">
            We'll email them a link to create their own password. They can't sign in until they do.
          </p>

          <v-select
            v-if="addUserForm.role === 'school_admin' || addUserForm.role === 'counselor'"
            v-model="addUserForm.schoolId"
            :items="schoolItems"
            label="School"
            hide-details
          />

          <div class="flex justify-end gap-2 pt-2">
            <v-btn variant="text" @click="showAddUser = false">Cancel</v-btn>
            <v-btn type="submit" color="primary" :loading="addUserLoading">Create User</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-dialog>

  <!-- ── Reset Password Dialog ───────────────────────────────────────────── -->
  <v-dialog v-model="showResetPassword">
    <v-card :title="`Reset Password — ${resetUser?.FirstName} ${resetUser?.LastName}`">
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
