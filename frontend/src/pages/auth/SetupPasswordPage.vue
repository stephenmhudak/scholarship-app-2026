<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'
import { useAuthStore } from '../../stores/auth'
import { useSignInRedirect } from '../../composables/useSignInRedirect'
import TwoFactorChallengeForm from '../../components/auth/TwoFactorChallengeForm.vue'

// From the invitation email (/setup-password?token=…): someone added by a school admin or admin creates their
// password. Their account can't sign in until they do.
const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const redirectAfterSignIn = useSignInRedirect()

const token = String(route.query.token ?? '')
const checking = ref(true)
const invite = ref(null)
const linkError = ref(null)
const form = ref({ newPassword: '', passwordConfirmation: '' })
const loading = ref(false)
const error = ref(null)
const pendingChallenge = ref(null)

onMounted(async () => {
  // Keep the one-time token out of the address bar and history
  router.replace({ query: {} })
  try {
    const { data } = await api.post('/auth/setup-password/check', { token })
    invite.value = data
  } catch (err) {
    linkError.value = err.response?.data?.error || 'This link could not be checked. Please try again.'
  } finally {
    checking.value = false
  }
})

async function submit() {
  if (form.value.newPassword !== form.value.passwordConfirmation) {
    error.value = 'Passwords do not match.'
    return
  }
  loading.value = true
  error.value = null
  try {
    const challenge = await authStore.setupPassword({ token, newPassword: form.value.newPassword })
    if (challenge) pendingChallenge.value = challenge
    else redirectAfterSignIn()
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not create your password. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div v-if="checking" class="flex items-center gap-3 text-slate-400">
    <v-progress-circular indeterminate color="primary" size="22" width="2" />
    Checking your link…
  </div>

  <div v-else-if="linkError" class="space-y-5">
    <h2 class="text-xl font-bold text-navy">Link not valid</h2>
    <v-alert type="error" :text="linkError" />
    <v-btn variant="outlined" block to="/login">Go to sign in</v-btn>
  </div>

  <div v-else-if="pendingChallenge" class="space-y-5">
    <v-alert type="success" text="Your password has been created. Finish signing in with your second factor." />
    <TwoFactorChallengeForm :challenge="pendingChallenge" @success="redirectAfterSignIn" @cancel="$router.push('/login')" />
  </div>

  <div v-else class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Create your password</h2>
      <p class="text-sm text-slate-400 mt-0.5">
        Welcome, {{ invite.firstName }}. Choose a password for <span class="font-semibold text-navy">{{ invite.email }}</span>.
      </p>
    </div>

    <v-alert v-if="error" type="error" :text="error" closable @click:close="error = null" />

    <v-form class="space-y-4" @submit.prevent="submit">
      <!-- Lets password managers save the new password against the right account -->
      <input type="email" :value="invite.email" autocomplete="username" class="hidden" readonly>
      <v-text-field
        v-model="form.newPassword"
        type="password"
        label="Password"
        hint="At least 8 characters"
        autocomplete="new-password"
        required
        hide-details="auto"
      />
      <v-text-field
        v-model="form.passwordConfirmation"
        type="password"
        label="Confirm password"
        autocomplete="new-password"
        required
        hide-details="auto"
      />
      <v-btn type="submit" color="primary" size="large" block :loading="loading">Create password and sign in</v-btn>
    </v-form>
  </div>
</template>
