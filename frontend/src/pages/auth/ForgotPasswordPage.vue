<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const step = ref('request')
const email = ref(route.query.email || '')
const form = ref({
  code: '',
  newPassword: '',
  passwordConfirmation: '',
})
const loading = ref(false)
const error = ref(null)
const resending = ref(false)
const resendMessage = ref(null)

const roleHome = {
  applicant:    '/dashboard',
  scorer:       '/scoring',
  app_admin:    '/admin/applications',
  school_admin: '/schools',
  counselor:    '/counselor',
}

async function requestCode() {
  loading.value = true
  error.value = null
  try {
    await authStore.forgotPassword(email.value)
    step.value = 'reset'
  } catch {
    error.value = 'Could not send the reset code. Please try again shortly.'
  } finally {
    loading.value = false
  }
}

async function resend() {
  resending.value = true
  resendMessage.value = null
  try {
    await authStore.forgotPassword(email.value)
    resendMessage.value = 'If an account exists for that email, a new code is on its way. Codes can be requested once a minute.'
  } catch {
    resendMessage.value = 'Could not resend the code. Please try again shortly.'
  } finally {
    resending.value = false
  }
}

async function resetPassword() {
  if (form.value.newPassword !== form.value.passwordConfirmation) {
    error.value = 'Passwords do not match.'
    return
  }

  loading.value = true
  error.value = null
  try {
    await authStore.resetPassword({
      email: email.value,
      code: form.value.code,
      newPassword: form.value.newPassword,
    })
    router.replace(roleHome[authStore.role] ?? '/dashboard')
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not reset your password. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Reset your password</h2>
      <p v-if="step === 'request'" class="text-sm text-slate-400 mt-0.5">
        Enter your account email and we'll send you a one-time code.
      </p>
      <p v-else class="text-sm text-slate-400 mt-0.5">
        If an account exists for <span class="font-semibold text-navy">{{ email }}</span>, we sent it a 6-digit code.
        It expires in 15 minutes.
      </p>
    </div>

    <v-alert v-if="error" type="error" :text="error" closable @click:close="error = null" />

    <v-form v-if="step === 'request'" class="space-y-4" @submit.prevent="requestCode">
      <v-text-field
        v-model="email"
        type="email"
        label="Email address"
        placeholder="you@example.com"
        autocomplete="email"
        required
        hide-details="auto"
      />

      <v-btn type="submit" color="primary" size="large" block :loading="loading">
        Send reset code
      </v-btn>
    </v-form>

    <template v-else>
      <v-form class="space-y-4" @submit.prevent="resetPassword">
        <v-text-field
          v-model="form.code"
          label="Reset code"
          placeholder="123456"
          inputmode="numeric"
          autocomplete="one-time-code"
          maxlength="6"
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

        <v-btn type="submit" color="primary" size="large" block :loading="loading">
          Reset password
        </v-btn>
      </v-form>

      <v-alert v-if="resendMessage" type="info" :text="resendMessage" closable @click:close="resendMessage = null" />

      <div class="flex justify-between">
        <v-btn variant="text" @click="step = 'request'">Use a different email</v-btn>
        <v-btn variant="text" color="primary" :loading="resending" @click="resend">Resend code</v-btn>
      </div>
    </template>

    <p class="text-sm text-center text-slate-400">
      Remembered it?
      <RouterLink to="/login" class="text-primary font-semibold hover:underline">Sign in</RouterLink>
    </p>
  </div>
</template>
