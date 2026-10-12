<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'
import TotpSetup from '../../components/auth/TotpSetup.vue'

const authStore = useAuthStore()

const emptyForm = () => ({
  currentPassword: '',
  newPassword: '',
  passwordConfirmation: '',
})
const form = ref(emptyForm())
const formRef = ref(null)
const saving = ref(false)
const alert = ref(null)

const twoFactor = ref(null)
const settingUpTwoFactor = ref(false)
const disableDialog = ref(false)
const disablePassword = ref('')
const disabling = ref(false)
const disableError = ref(null)

onMounted(loadTwoFactorStatus)

async function loadTwoFactorStatus() {
  try {
    twoFactor.value = await authStore.fetchTwoFactorStatus()
  } catch {
    alert.value = { type: 'error', message: 'Could not load your two-factor settings.' }
  }
}

async function finishTwoFactorSetup() {
  settingUpTwoFactor.value = false
  await loadTwoFactorStatus()
}

function openDisableDialog() {
  disablePassword.value = ''
  disableError.value = null
  disableDialog.value = true
}

async function disableTwoFactor() {
  disabling.value = true
  disableError.value = null
  try {
    await authStore.disableTwoFactor(disablePassword.value)
    disableDialog.value = false
    alert.value = { type: 'success', message: 'Two-factor authentication has been turned off.' }
    await loadTwoFactorStatus()
  } catch (err) {
    disableError.value = err.response?.data?.error || 'Could not turn off two-factor. Please try again.'
  } finally {
    disabling.value = false
  }
}

async function changePassword() {
  if (form.value.newPassword !== form.value.passwordConfirmation) {
    alert.value = { type: 'error', message: 'New passwords do not match.' }
    return
  }

  saving.value = true
  alert.value = null
  try {
    await authStore.changePassword({
      currentPassword: form.value.currentPassword,
      newPassword: form.value.newPassword,
    })
    form.value = emptyForm()
    formRef.value?.resetValidation()
    alert.value = { type: 'success', message: 'Your password has been changed.' }
  } catch (err) {
    alert.value = { type: 'error', message: err.response?.data?.error || 'Could not change your password. Please try again.' }
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Account</h1>
      <p class="page-subtitle">Manage your sign-in details</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <v-card title="Profile">
      <v-card-text class="pa-6 grid sm:grid-cols-2 gap-4">
        <div>
          <p class="eyebrow">Name</p>
          <p class="text-navy">{{ authStore.fullName }}</p>
        </div>
        <div>
          <p class="eyebrow">Email</p>
          <p class="text-navy">{{ authStore.user?.email }}</p>
        </div>
      </v-card-text>
    </v-card>

    <v-card title="Change Password">
      <v-card-text class="pa-6">
        <v-form ref="formRef" class="space-y-4 max-w-md" @submit.prevent="changePassword">
          <v-text-field
            v-model="form.currentPassword"
            type="password"
            label="Current password"
            autocomplete="current-password"
            required
            hide-details="auto"
          />
          <v-text-field
            v-model="form.newPassword"
            type="password"
            label="New password"
            hint="At least 8 characters"
            autocomplete="new-password"
            required
            hide-details="auto"
          />
          <v-text-field
            v-model="form.passwordConfirmation"
            type="password"
            label="Confirm new password"
            autocomplete="new-password"
            required
            hide-details="auto"
          />
          <v-btn type="submit" color="primary" :loading="saving">Change password</v-btn>
        </v-form>
      </v-card-text>
    </v-card>

    <v-card title="Two-Factor Authentication">
      <v-card-text class="pa-6">
        <div v-if="!twoFactor" class="flex items-center gap-3 text-sm text-slate-400">
          <v-progress-circular indeterminate color="primary" size="20" width="2" />
          Loading…
        </div>

        <div v-else-if="twoFactor.enabled" class="space-y-4">
          <div class="flex items-center gap-3">
            <v-chip color="success" prepend-icon="mdi-shield-check-outline">On</v-chip>
            <p class="text-sm text-navy">Signing in needs a code from your authenticator app.</p>
          </div>
          <v-alert
            v-if="twoFactor.recoveryCodesRemaining <= 2"
            type="warning"
            :text="`You have ${twoFactor.recoveryCodesRemaining} recovery code${twoFactor.recoveryCodesRemaining === 1 ? '' : 's'} left. Turn two-factor off and on again to get a new set.`"
          />
          <p v-else class="text-sm text-slate-400">{{ twoFactor.recoveryCodesRemaining }} unused recovery codes remaining.</p>
          <v-btn variant="outlined" color="error" @click="openDisableDialog">Turn off two-factor</v-btn>
        </div>

        <div v-else-if="settingUpTwoFactor" class="max-w-md">
          <TotpSetup @done="finishTwoFactorSetup" @cancel="settingUpTwoFactor = false" />
        </div>

        <div v-else class="space-y-4">
          <p class="text-sm text-slate-400">
            Add a second step to signing in: a 6-digit code from an authenticator app on your phone.
            It keeps your account safe even if someone learns your password.
          </p>
          <v-btn color="primary" prepend-icon="mdi-shield-lock-outline" @click="settingUpTwoFactor = true">
            Set up two-factor
          </v-btn>
        </div>
      </v-card-text>
    </v-card>

    <v-dialog v-model="disableDialog" max-width="440">
      <v-card title="Turn off two-factor">
        <template #append>
          <v-btn icon="mdi-close" variant="text" size="small" @click="disableDialog = false" />
        </template>
        <v-card-text class="pa-6">
          <v-form class="space-y-4" @submit.prevent="disableTwoFactor">
            <p class="text-sm text-slate-400">Enter your password to confirm. Your recovery codes will stop working.</p>
            <v-alert v-if="disableError" type="error" :text="disableError" />
            <v-text-field
              v-model="disablePassword"
              type="password"
              label="Password"
              autocomplete="current-password"
              autofocus
              required
              hide-details="auto"
            />
            <div class="flex justify-end gap-2">
              <v-btn variant="text" @click="disableDialog = false">Cancel</v-btn>
              <v-btn type="submit" color="error" :loading="disabling">Turn off</v-btn>
            </div>
          </v-form>
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>
