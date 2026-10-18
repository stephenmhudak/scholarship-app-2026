<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import ScorerScopeDialog from '../../components/admin/ScorerScopeDialog.vue'
import { useAuthStore } from '../../stores/auth'
import { roleHome } from '../../utils/roleHome'
import { roleLabel } from '../../utils/roleLabels'
import { tourLabel } from '../../tours'
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
const ALL_ROLES = [
  { title: 'Applicant', value: 'applicant' },
  { title: 'Scorer', value: 'scorer' },
  { title: 'App Admin', value: 'app_admin' },
  { title: 'School Admin', value: 'school_admin' },
  { title: 'Counselor', value: 'counselor' },
  { title: 'Reference Provider', value: 'referee' },
  { title: 'Super Admin', value: 'super_admin' },
]

const authStore = useAuthStore()
const isSuperAdmin = computed(() => authStore.role === 'super_admin')
// Only super admins can create super admins (the API enforces it too)
const ROLES = computed(() => ALL_ROLES.filter((r) => r.value !== 'super_admin' || isSuperAdmin.value))

const userHeaders = [
  { title: 'Name', key: 'name', value: (u) => `${u.FirstName} ${u.LastName}` },
  { title: 'Email', key: 'Email' },
  { title: 'Role', key: 'Role' },
  { title: 'School', key: 'SchoolName' },
  { title: '2FA', key: 'TwoFactorEnabled', value: (u) => !!u.TwoFactorEnabled },
  { title: 'Status', key: 'status', sortable: false },
  { title: '', key: 'actions', sortable: false, align: 'end', width: 56 },
]

const users = ref([])
const userSearch = ref('')
const roleFilter = ref(null)
const showDeactivated = ref(false)
const roleFilterItems = [{ title: 'All roles', value: null }, ...ALL_ROLES]
const deactivatedCount = computed(() => users.value.filter((u) => u.DeactivatedAt).length)
const filteredUsers = computed(() => {
  const terms = userSearch.value.trim().toLowerCase().split(/\s+/).filter(Boolean)
  return users.value.filter((u) => {
    if (!showDeactivated.value && u.DeactivatedAt) return false
    if (roleFilter.value && u.Role !== roleFilter.value) return false
    const text = `${u.FirstName} ${u.LastName} ${u.Email} ${u.SchoolName ?? ''}`.toLowerCase()
    return terms.every((t) => text.includes(t))
  })
})

// App admins can't manage super admins (the API enforces it too)
const canManage = (u) => u.Role !== 'super_admin' || isSuperAdmin.value
const isMe = (u) => u.Id === authStore.user?.id

// What the ⋯ menu offers for a user
function userActions(u) {
  if (!canManage(u)) return []
  const active = !u.DeactivatedAt
  return [
    { title: 'Edit details', icon: 'mdi-pencil', run: () => openEditUser(u) },
    u.Role === 'scorer' && { title: 'Scoring access', icon: 'mdi-filter-outline', run: () => openScope(u) },
    { title: 'Tour history', icon: 'mdi-map-marker-path', run: () => openTours(u) },
    active && u.InvitePending && { title: 'Resend invite', icon: 'mdi-email-fast-outline', run: () => resendInvite(u) },
    active && !u.InvitePending && { title: 'Reset password', icon: 'mdi-lock-reset', run: () => openResetPassword(u) },
    u.TwoFactorEnabled && { title: 'Reset 2FA', icon: 'mdi-shield-off-outline', run: () => resetTwoFactor(u) },
    active && isSuperAdmin.value && u.Role !== 'super_admin' && !isMe(u) && { title: 'Impersonate', icon: 'mdi-incognito', run: () => impersonate(u) },
    !isMe(u) && (active
      ? { title: 'Deactivate', icon: 'mdi-account-off-outline', color: 'error', run: () => deactivate(u) }
      : { title: 'Reactivate', icon: 'mdi-account-check-outline', run: () => reactivate(u) }),
  ].filter(Boolean)
}

// ── Edit user ──
const showEditUser = ref(false)
const editUserForm = ref(null)
const editUserLoading = ref(false)
function openEditUser(u) {
  editUserForm.value = { id: u.Id, firstName: u.FirstName, lastName: u.LastName, email: u.Email, role: u.Role, schoolId: u.SchoolId ?? '', originalRole: u.Role, self: isMe(u) }
  showEditUser.value = true
}
async function submitEditUser() {
  const f = editUserForm.value
  editUserLoading.value = true
  try {
    await api.put(`/admin/users/${f.id}`, {
      firstName: f.firstName, lastName: f.lastName, email: f.email, role: f.role,
      schoolId: f.role === 'school_admin' || f.role === 'counselor' ? f.schoolId || null : null,
    })
    showEditUser.value = false
    showAlert('success', f.role !== f.originalRole ? 'User saved. They\'ll need to sign in again to get their new role.' : 'User saved.')
    await loadUsers()
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to save the user.')
  } finally {
    editUserLoading.value = false
  }
}

// ── Tour history: the page tours they've been through (UserTours) ──
const toursUser = ref(null)
const tourRows = ref([])
const toursLoading = ref(false)
const showTours = computed({ get: () => !!toursUser.value, set: (v) => { if (!v) toursUser.value = null } })
const tourHeaders = [
  { title: 'Page', key: 'page', value: (r) => tourLabel(r.tourId) },
  { title: 'As', key: 'role', value: (r) => roleLabel(r.tourId.split(':')[0]) },
  { title: 'Last taken', key: 'lastSeenAt' },
  { title: 'Last finished', key: 'completedAt' },
  { title: 'How it ended', key: 'outcome' },
  { title: 'Times', key: 'timesSeen', align: 'end' },
]
async function openTours(u) {
  toursUser.value = u
  tourRows.value = []
  toursLoading.value = true
  try {
    tourRows.value = (await api.get(`/admin/users/${u.Id}/tours`)).data
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to load their tour history.')
    toursUser.value = null
  } finally {
    toursLoading.value = false
  }
}
const formatDateTime = (d) => (d ? new Date(d).toLocaleString() : '—')

// ── Deactivate / reactivate ──
async function deactivate(u) {
  if (!await confirm({
    title: 'Deactivate user?',
    text: `${u.FirstName} ${u.LastName} will be signed out and won't be able to sign in. Their applications, scores and references stay. You can reactivate them later.`,
    confirmText: 'Deactivate',
    color: 'error',
  })) return
  try {
    await api.post(`/admin/users/${u.Id}/deactivate`)
    u.DeactivatedAt = new Date().toISOString()
    showAlert('success', `${u.FirstName} ${u.LastName} has been deactivated.`)
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to deactivate the user.')
  }
}
async function reactivate(u) {
  try {
    await api.post(`/admin/users/${u.Id}/reactivate`)
    u.DeactivatedAt = null
    showAlert('success', `${u.FirstName} ${u.LastName} can sign in again.`)
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to reactivate the user.')
  }
}
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

// Super admins can see the app exactly as another user does; a full reload so nothing of theirs stays on screen
const impersonating = ref(null)
async function impersonate(user) {
  if (!await confirm({
    title: 'Impersonate user',
    text: `You'll see and use the app exactly as ${user.FirstName} ${user.LastName} (${user.Email}) until you choose End impersonation. Anything you do is done as them.`,
    confirmText: 'Impersonate',
  })) return
  impersonating.value = user.Id
  try {
    await authStore.startImpersonation(user.Id)
    window.location.assign(roleHome(authStore.role, authStore.user?.schoolId))
  } catch (err) {
    showAlert('error', err.response?.data?.error || 'Failed to impersonate this user.')
    impersonating.value = null
  }
}

// Scorers can be limited to applications with certain answers
const scopeUser = ref(null)
const showScope = ref(false)
function openScope(user) {
  scopeUser.value = user
  showScope.value = true
}
function onScopeSaved(scope) {
  scopeUser.value.ScoringScope = scope
  showAlert('success', scope === 'all' ? 'This scorer can see all applications.' : 'This scorer now only sees matching applications.')
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
      <h1 class="page-title">Users &amp; Schools</h1>
      <p class="page-subtitle">Manage users, schools, and registration links</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <!-- Tabs -->
    <v-tabs v-model="activeTab" color="primary" class="border-b" data-tour="admin-tabs">
      <v-tab value="users" class="text-none">Users</v-tab>
      <v-tab value="schools" class="text-none">Schools</v-tab>
      <v-tab value="invites" class="text-none">School Admin Invites</v-tab>
    </v-tabs>

    <!-- ── Users Tab ─────────────────────────────────────────────────────── -->
    <div v-if="activeTab === 'users'" class="space-y-4">
      <div class="flex flex-wrap items-center gap-3" data-tour="user-filters">
        <v-text-field
          v-model="userSearch"
          prepend-inner-icon="mdi-magnify"
          placeholder="Search name, email or school"
          density="compact"
          hide-details
          clearable
          class="flex-1 min-w-[220px]"
        />
        <v-select v-model="roleFilter" :items="roleFilterItems" density="compact" hide-details class="max-w-[200px]" aria-label="Role" />
        <v-checkbox v-if="deactivatedCount" v-model="showDeactivated" :label="`Show deactivated (${deactivatedCount})`" density="compact" hide-details class="flex-none" />
        <v-btn color="primary" prepend-icon="mdi-plus" data-tour="add-user" @click="openAddUser">Add User</v-btn>
      </div>
      <p class="text-sm text-slate-400">
        {{ filteredUsers.length === users.length ? '' : `${filteredUsers.length} of ` }}{{ users.length }} user{{ users.length !== 1 ? 's' : '' }}
      </p>

      <v-card class="overflow-hidden">
        <v-data-table
          :headers="userHeaders"
          :items="filteredUsers"
          :loading="usersLoading"
          :row-props="({ item }) => ({ class: item.DeactivatedAt ? 'opacity-60' : '' })"
          no-data-text="No users match."
        >
          <template #item.name="{ value, item }">
            <span class="font-medium text-navy">{{ value }}</span>
            <span v-if="isMe(item)" class="text-xs text-slate-400"> (you)</span>
          </template>
          <template #item.Email="{ value }">
            <span class="text-slate-400">{{ value }}</span>
          </template>
          <template #item.Role="{ value }">
            <v-chip color="primary">{{ roleLabel(value) }}</v-chip>
          </template>
          <template #item.SchoolName="{ value }">
            <span class="text-slate-400">{{ value ?? '—' }}</span>
          </template>
          <template #item.TwoFactorEnabled="{ value }">
            <v-chip v-if="value" color="success">On</v-chip>
            <span v-else class="text-slate-400">—</span>
          </template>
          <template #item.status="{ item }">
            <div class="flex flex-wrap gap-1">
              <v-chip v-if="item.DeactivatedAt" color="error">Deactivated</v-chip>
              <v-chip v-else-if="item.InvitePending" color="warning">Invite pending</v-chip>
              <v-chip v-if="item.Role === 'scorer' && item.ScoringScope === 'filtered'" color="primary">Limited scoring</v-chip>
            </div>
          </template>
          <template #item.actions="{ item }">
            <v-menu v-if="userActions(item).length" location="bottom end">
              <template #activator="{ props: menuProps }">
                <v-btn
                  v-bind="menuProps"
                  icon="mdi-dots-horizontal"
                  data-tour="user-actions"
                  variant="text"
                  size="small"
                  :loading="impersonating === item.Id"
                  :aria-label="`Actions for ${item.FirstName} ${item.LastName}`"
                />
              </template>
              <v-list density="compact" min-width="200">
                <v-list-item
                  v-for="a in userActions(item)"
                  :key="a.title"
                  :title="a.title"
                  :prepend-icon="a.icon"
                  :base-color="a.color"
                  @click="a.run()"
                />
              </v-list>
            </v-menu>
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

  <ScorerScopeDialog v-model="showScope" :user="scopeUser" @saved="onScopeSaved" />

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

  <!-- ── Tour History Dialog ─────────────────────────────────────────────── -->
  <v-dialog v-model="showTours" max-width="860">
    <v-card v-if="toursUser" :title="`Tour history — ${toursUser.FirstName} ${toursUser.LastName}`">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="showTours = false" />
      </template>
      <v-card-text class="pa-6 space-y-3">
        <p class="text-sm text-slate-400">
          The page tours they've taken. A tour shows again on its own when its page changes.
        </p>
        <v-card class="overflow-hidden">
          <v-data-table
            :headers="tourHeaders"
            :items="tourRows"
            :loading="toursLoading"
            :sort-by="[{ key: 'lastSeenAt', order: 'desc' }]"
            no-data-text="They haven't taken any tours yet."
          >
            <template #item.lastSeenAt="{ value }">{{ formatDateTime(value) }}</template>
            <template #item.completedAt="{ value }">{{ formatDateTime(value) }}</template>
            <template #item.outcome="{ value }">
              <v-chip :color="value === 'completed' ? 'success' : undefined">{{ value === 'completed' ? 'Finished' : 'Closed early' }}</v-chip>
            </template>
          </v-data-table>
        </v-card>
      </v-card-text>
    </v-card>
  </v-dialog>

  <!-- ── Edit User Dialog ────────────────────────────────────────────────── -->
  <v-dialog v-model="showEditUser">
    <v-card v-if="editUserForm" title="Edit User">
      <template #append>
        <v-btn icon="mdi-close" variant="text" size="small" @click="showEditUser = false" />
      </template>
      <v-card-text>
        <v-form class="space-y-4" @submit.prevent="submitEditUser">
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <v-text-field v-model="editUserForm.firstName" label="First Name" hide-details="auto" />
            <v-text-field v-model="editUserForm.lastName" label="Last Name" hide-details="auto" />
          </div>
          <v-text-field v-model="editUserForm.email" type="email" label="Email" hint="They sign in with this address." persistent-hint />
          <v-select
            v-model="editUserForm.role"
            :items="ROLES"
            label="Role"
            :disabled="editUserForm.self"
            :hint="editUserForm.self ? 'You can\'t change your own role.' : editUserForm.role !== editUserForm.originalRole ? 'They\'ll be signed out and get the new role when they sign in again.' : ''"
            persistent-hint
          />
          <v-select
            v-if="editUserForm.role === 'school_admin' || editUserForm.role === 'counselor'"
            v-model="editUserForm.schoolId"
            :items="schoolItems"
            label="School"
            hide-details
          />
          <div class="flex justify-end gap-2 pt-2">
            <v-btn variant="text" @click="showEditUser = false">Cancel</v-btn>
            <v-btn type="submit" color="primary" :loading="editUserLoading">Save</v-btn>
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
