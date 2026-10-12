<script setup>
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { useSignInRedirect } from '../../composables/useSignInRedirect'
import TwoFactorChallengeForm from '../../components/auth/TwoFactorChallengeForm.vue'

const authStore = useAuthStore()
const redirectAfterSignIn = useSignInRedirect()

const form = ref({
  email: '',
  password: '',
})
const loading = ref(false)
const error = ref(null)
const unverifiedEmail = ref(null)
const resending = ref(false)
const resendMessage = ref(null)
const pendingChallenge = ref(null)

async function login() {
  loading.value = true
  error.value = null
  unverifiedEmail.value = null
  resendMessage.value = null

  try {
    const challenge = await authStore.login(form.value)
    if (challenge) pendingChallenge.value = challenge
    else redirectAfterSignIn()
  } catch (err) {
    if (err.response?.data?.code === 'email_not_verified') unverifiedEmail.value = form.value.email
    else error.value = err.response?.data?.error || 'Invalid email or password. Please try again.'
  } finally {
    loading.value = false
  }
}

async function resend() {
  resending.value = true
  try {
    await authStore.resendVerification(unverifiedEmail.value)
    resendMessage.value = 'A new verification link is on its way. Check your inbox.'
  } catch {
    resendMessage.value = 'Could not resend the email. Please try again shortly.'
  } finally {
    resending.value = false
  }
}
</script>

<template>
  <TwoFactorChallengeForm
    v-if="pendingChallenge"
    :challenge="pendingChallenge"
    @success="redirectAfterSignIn"
    @cancel="pendingChallenge = null; form.password = ''"
  />

  <div v-else class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Sign in</h2>
      <p class="text-sm text-slate-400 mt-0.5">Access your scholarship account</p>
    </div>

    <v-alert v-if="error" type="error" :text="error" closable @click:close="error = null" />

    <div v-if="unverifiedEmail" class="space-y-3">
      <v-alert
        type="warning"
        text="Please verify your email address before signing in. Check your inbox for the link we sent."
      />
      <v-alert v-if="resendMessage" type="info" :text="resendMessage" closable @click:close="resendMessage = null" />
      <v-btn variant="outlined" block :loading="resending" @click="resend">
        Resend verification email
      </v-btn>
    </div>

    <v-form class="space-y-4" @submit.prevent="login">
      <v-text-field
        v-model="form.email"
        type="email"
        label="Email address"
        placeholder="you@example.com"
        autocomplete="email"
        required
        hide-details="auto"
      />

      <v-text-field
        v-model="form.password"
        type="password"
        label="Password"
        autocomplete="current-password"
        required
        hide-details="auto"
      />

      <div class="text-right">
        <RouterLink
          :to="{ path: '/forgot-password', query: form.email ? { email: form.email } : {} }"
          class="text-sm text-primary font-semibold hover:underline"
        >
          Forgot password?
        </RouterLink>
      </div>

      <v-btn type="submit" color="primary" size="large" block :loading="loading">
        Sign in
      </v-btn>
    </v-form>

    <p class="text-sm text-center text-slate-400">
      Don't have an account?
      <RouterLink to="/register" class="text-primary font-semibold hover:underline">Register</RouterLink>
    </p>
  </div>
</template>
