<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'
import TwoFactorSetup from '../../components/auth/TwoFactorSetup.vue'
import RecoveryCodesList from '../../components/auth/RecoveryCodesList.vue'

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
// Turning 2FA off and regenerating recovery codes both re-confirm the password in one dialog
const passwordDialog = ref(false)
const passwordAction = ref('disable')
const dialogPassword = ref('')
const dialogSubmitting = ref(false)
const dialogError = ref(null)
const newRecoveryCodes = ref(null)

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

function openPasswordDialog(action) {
  passwordAction.value = action
  dialogPassword.value = ''
  dialogError.value = null
  newRecoveryCodes.value = null
  passwordDialog.value = true
}

async function submitPasswordDialog() {
  dialogSubmitting.value = true
  dialogError.value = null
  try {
    if (passwordAction.value === 'disable') {
      await authStore.disableTwoFactor(dialogPassword.value)
      passwordDialog.value = false
      alert.value = { type: 'success', message: 'Two-factor authentication has been turned off.' }
    } else {
      newRecoveryCodes.value = await authStore.regenerateRecoveryCodes(dialogPassword.value)
    }
    await loadTwoFactorStatus()
  } catch (err) {
    dialogError.value = err.response?.data?.error || 'Something went wrong. Please try again.'
  } finally {
    dialogSubmitting.value = false
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
            <p class="text-sm text-navy">
              {{ twoFactor.method === 'email'
                ? 'Signing in needs a code we email to you.'
                : 'Signing in needs a code from your authenticator app.' }}
            </p>
          </div>
          <p v-if="twoFactor.method === 'email'" class="text-sm text-slate-400">
            To switch to an authenticator app, turn two-factor off and set it up again.
          </p>
          <v-alert
            v-else-if="twoFactor.recoveryCodesRemaining <= 2"
            type="warning"
            :text="`You have ${twoFactor.recoveryCodesRemaining} recovery code${twoFactor.recoveryCodesRemaining === 1 ? '' : 's'} left. Create a new set so you don't get locked out.`"
          />
          <p v-else class="text-sm text-slate-400">{{ twoFactor.recoveryCodesRemaining }} unused recovery codes remaining.</p>
          <div class="flex flex-wrap gap-2">
            <v-btn v-if="twoFactor.method === 'totp'" variant="outlined" prepend-icon="mdi-refresh" @click="openPasswordDialog('regenerate')">New recovery codes</v-btn>
            <v-btn variant="outlined" color="error" @click="openPasswordDialog('disable')">Turn off two-factor</v-btn>
          </div>
        </div>

        <div v-else-if="settingUpTwoFactor" class="max-w-md">
          <TwoFactorSetup @done="finishTwoFactorSetup" @cancel="settingUpTwoFactor = false" />
        </div>

        <div v-else class="space-y-4">
          <p class="text-sm text-slate-400">
            Add a second step to signing in: a 6-digit code from an authenticator app on your phone, or one we email you.
            It keeps your account safe even if someone learns your password.
          </p>
          <v-btn color="primary" prepend-icon="mdi-shield-lock-outline" @click="settingUpTwoFactor = true">
            Set up two-factor
          </v-btn>
        </div>
      </v-card-text>
    </v-card>

    <v-dialog v-model="passwordDialog" max-width="480">
      <v-card :title="passwordAction === 'disable' ? 'Turn off two-factor' : 'New recovery codes'">
        <template #append>
          <v-btn icon="mdi-close" variant="text" size="small" @click="passwordDialog = false" />
        </template>
        <v-card-text class="pa-6">
          <div v-if="newRecoveryCodes" class="space-y-4">
            <RecoveryCodesList :codes="newRecoveryCodes" />
            <v-btn color="primary" block @click="passwordDialog = false">I've saved my recovery codes</v-btn>
          </div>

          <v-form v-else class="space-y-4" @submit.prevent="submitPasswordDialog">
            <p class="text-sm text-slate-400">
              <template v-if="passwordAction === 'disable'">Enter your password to confirm.{{ twoFactor?.method === 'totp' ? ' Your recovery codes will stop working.' : '' }}</template>
              <template v-else>Enter your password to create 10 new recovery codes. Your current codes will stop working.</template>
            </p>
            <v-alert v-if="dialogError" type="error" :text="dialogError" />
            <v-text-field
              v-model="dialogPassword"
              type="password"
              label="Password"
              autocomplete="current-password"
              autofocus
              required
              hide-details="auto"
            />
            <div class="flex justify-end gap-2">
              <v-btn variant="text" @click="passwordDialog = false">Cancel</v-btn>
              <v-btn type="submit" :color="passwordAction === 'disable' ? 'error' : 'primary'" :loading="dialogSubmitting">
                {{ passwordAction === 'disable' ? 'Turn off' : 'Create new codes' }}
              </v-btn>
            </div>
          </v-form>
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>
