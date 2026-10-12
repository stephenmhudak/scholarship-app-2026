<script setup>
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'

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
  </div>
</template>
