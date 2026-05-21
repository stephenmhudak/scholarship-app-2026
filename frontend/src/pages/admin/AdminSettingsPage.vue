<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseInput from '../../components/common/BaseInput.vue'
import BaseModal from '../../components/common/BaseModal.vue'

const activeTab = ref('users')

// ── Shared ──────────────────────────────────────────────────────────────────
const alert = ref(null)
function showAlert(type, message) {
  alert.value = { type, message }
  setTimeout(() => (alert.value = null), 4000)
}

// ── Users ────────────────────────────────────────────────────────────────────
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
  if (!f.password) addUserErrors.value.password = 'Required'
  if (Object.keys(addUserErrors.value).length) return

  addUserLoading.value = true
  try {
    await api.post('/admin/users', {
      firstName: f.firstName,
      lastName: f.lastName,
      email: f.email,
      password: f.password,
      role: f.role,
      schoolId: f.schoolId || null,
    })
    showAddUser.value = false
    showAlert('success', 'User created.')
    await loadUsers()
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to create user.')
  } finally {
    addUserLoading.value = false
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

// ── Schools ──────────────────────────────────────────────────────────────────
const schools = ref([])
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
  editingSchool.value = { id: school.Id, name: school.Name, address: school.Address ?? '' }
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
      address: editingSchool.value.address,
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
const invites = ref([])
const invitesLoading = ref(false)
const generatingInvite = ref(null)
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

async function generateInvite(schoolId) {
  generatingInvite.value = schoolId
  newInviteLink.value = null
  try {
    const res = await api.post(`/admin/schools/${schoolId}/invite`)
    const token = res.data.token
    const origin = window.location.origin
    newInviteLink.value = { token, url: `${origin}/register?invite=${token}`, schoolName: res.data.schoolName }
    await loadInvites()
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to generate invite.')
  } finally {
    generatingInvite.value = null
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
function switchTab(tab) {
  activeTab.value = tab
  if (tab === 'users' && !users.value.length) loadUsers()
  if (tab === 'schools' && !schools.value.length) loadSchools()
  if (tab === 'invites') { loadInvites(); if (!schools.value.length) loadSchools() }
}

onMounted(() => loadUsers())
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Settings</h1>
      <p class="page-subtitle">Manage users, schools, and registration links</p>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <!-- Tabs -->
    <div class="flex gap-1 border-b border-[#E9EDF7]">
      <button
        v-for="tab in [{ key: 'users', label: 'Users' }, { key: 'schools', label: 'Schools' }, { key: 'invites', label: 'School Admin Invites' }]"
        :key="tab.key"
        @click="switchTab(tab.key)"
        class="px-5 py-2.5 text-sm font-medium transition-colors border-b-2 -mb-px"
        :class="activeTab === tab.key
          ? 'border-primary text-primary'
          : 'border-transparent text-slate-400 hover:text-navy'"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- ── Users Tab ─────────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'users'" class="space-y-4">
      <div class="flex justify-between items-center">
        <p class="text-sm text-slate-400">{{ users.length }} user{{ users.length !== 1 ? 's' : '' }}</p>
        <BaseButton size="sm" @click="openAddUser">Add User</BaseButton>
      </div>

      <div class="card overflow-hidden">
        <div v-if="usersLoading" class="p-8 text-center text-slate-400 text-sm">Loading…</div>
        <table v-else class="dialect-table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>School</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!users.length">
              <td colspan="5" class="text-center text-slate-400">No users found.</td>
            </tr>
            <tr v-for="u in users" :key="u.Id">
              <td class="font-medium text-navy">{{ u.FirstName }} {{ u.LastName }}</td>
              <td class="text-slate-400">{{ u.Email }}</td>
              <td>
                <span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-primary/10 text-primary">
                  {{ u.Role }}
                </span>
              </td>
              <td class="text-slate-400">{{ u.SchoolName ?? '—' }}</td>
              <td class="text-right">
                <button
                  @click="openResetPassword(u)"
                  class="text-xs text-primary hover:underline"
                >
                  Reset Password
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- ── Schools Tab ───────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'schools'" class="space-y-4">
      <div v-if="schoolsLoading" class="text-center text-slate-400 text-sm py-8">Loading…</div>

      <div v-else class="space-y-3">
        <div v-if="!schools.length" class="section-card text-slate-400 text-sm text-center">No schools found.</div>

        <div v-for="school in schools" :key="school.Id" class="section-card space-y-4">
          <div v-if="editingSchool?.id === school.Id" class="space-y-3">
            <div class="grid grid-cols-2 gap-3">
              <BaseInput v-model="editingSchool.name" label="Name" :required="true" />
              <BaseInput v-model="editingSchool.address" label="Address" />
            </div>
            <div class="flex gap-2">
              <BaseButton size="sm" :loading="schoolSaving" @click="saveSchool">Save</BaseButton>
              <BaseButton size="sm" variant="ghost" @click="cancelEditSchool">Cancel</BaseButton>
            </div>
          </div>
          <div v-else class="flex items-start justify-between">
            <div>
              <p class="font-semibold text-navy">{{ school.Name }}</p>
              <p class="text-sm text-slate-400">{{ school.Address ?? 'No address set' }}</p>
            </div>
            <BaseButton size="sm" variant="secondary" @click="startEditSchool(school)">Edit</BaseButton>
          </div>
        </div>
      </div>
    </div>

    <!-- ── Invites Tab ───────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'invites'" class="space-y-6">

      <!-- New invite link result -->
      <div v-if="newInviteLink" class="section-card border border-success/20 bg-success-light space-y-2">
        <p class="text-sm font-semibold text-success-dark">Invite link generated for {{ newInviteLink.schoolName }}</p>
        <div class="flex items-center gap-2">
          <input
            readonly
            :value="newInviteLink.url"
            class="form-input flex-1 text-xs font-mono"
          />
          <BaseButton size="sm" variant="secondary" @click="copyLink">Copy</BaseButton>
        </div>
        <p class="text-xs text-slate-400">This link is valid for 7 days. Share it with the school admin to register.</p>
      </div>

      <!-- Generate invite by school -->
      <div class="section-card space-y-3">
        <p class="text-sm font-semibold text-navy">Generate Registration Link</p>
        <div v-if="schoolsLoading" class="text-slate-400 text-sm">Loading schools…</div>
        <div v-else class="space-y-2">
          <div
            v-for="school in schools"
            :key="school.Id"
            class="flex items-center justify-between py-2 border-b border-[#E9EDF7] last:border-0"
          >
            <span class="text-sm text-navy">{{ school.Name }}</span>
            <BaseButton
              size="sm"
              variant="secondary"
              :loading="generatingInvite === school.Id"
              @click="generateInvite(school.Id)"
            >
              Generate Link
            </BaseButton>
          </div>
          <p v-if="!schools.length" class="text-sm text-slate-400">No schools found.</p>
        </div>
      </div>

      <!-- Existing invites -->
      <div class="card overflow-hidden">
        <div class="px-5 py-3 border-b border-[#E9EDF7]">
          <p class="text-sm font-semibold text-navy">Invite History</p>
        </div>
        <div v-if="invitesLoading" class="p-8 text-center text-slate-400 text-sm">Loading…</div>
        <table v-else class="dialect-table">
          <thead>
            <tr>
              <th>School</th>
              <th>Created</th>
              <th>Expires</th>
              <th>Used</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!invites.length">
              <td colspan="4" class="text-center text-slate-400">No invites yet.</td>
            </tr>
            <tr v-for="invite in invites" :key="invite.Id">
              <td class="font-medium text-navy">{{ invite.SchoolName }}</td>
              <td class="text-slate-400">{{ formatDate(invite.CreatedAt) }}</td>
              <td class="text-slate-400">{{ formatDate(invite.ExpiresAt) }}</td>
              <td>
                <span
                  class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium"
                  :class="invite.UsedAt ? 'bg-success-light text-success-dark' : 'bg-warning-light text-[#8C6500]'"
                >
                  {{ invite.UsedAt ? 'Used' : 'Pending' }}
                </span>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </div>

  <!-- ── Add User Modal ──────────────────────────────────────────────────── -->
  <BaseModal :show="showAddUser" title="Add User" @close="showAddUser = false">
    <form @submit.prevent="submitAddUser" class="space-y-4">
      <div class="grid grid-cols-2 gap-3">
        <BaseInput v-model="addUserForm.firstName" label="First Name" :required="true" :error="addUserErrors.firstName" />
        <BaseInput v-model="addUserForm.lastName" label="Last Name" :required="true" :error="addUserErrors.lastName" />
      </div>
      <BaseInput v-model="addUserForm.email" type="email" label="Email" :required="true" :error="addUserErrors.email" />
      <BaseInput v-model="addUserForm.password" type="password" label="Password" :required="true" :error="addUserErrors.password" />

      <div>
        <label class="block text-sm font-medium text-navy mb-1">Role</label>
        <select v-model="addUserForm.role" class="form-select w-full">
          <option value="applicant">Applicant</option>
          <option value="scorer">Scorer</option>
          <option value="app_admin">App Admin</option>
          <option value="school_admin">School Admin</option>
          <option value="counselor">Counselor</option>
        </select>
      </div>

      <div v-if="addUserForm.role === 'school_admin' || addUserForm.role === 'counselor'">
        <label class="block text-sm font-medium text-navy mb-1">School</label>
        <select v-model="addUserForm.schoolId" class="form-select w-full">
          <option value="">— No School —</option>
          <option v-for="s in schools" :key="s.Id" :value="s.Id">{{ s.Name }}</option>
        </select>
      </div>

      <div class="flex justify-end gap-2 pt-2">
        <BaseButton type="button" variant="ghost" @click="showAddUser = false">Cancel</BaseButton>
        <BaseButton type="submit" :loading="addUserLoading">Create User</BaseButton>
      </div>
    </form>
  </BaseModal>

  <!-- ── Reset Password Modal ────────────────────────────────────────────── -->
  <BaseModal :show="showResetPassword" :title="`Reset Password — ${resetUser?.FirstName} ${resetUser?.LastName}`" @close="showResetPassword = false">
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
