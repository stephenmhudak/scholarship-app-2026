<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useSignInRedirect } from '../../composables/useSignInRedirect'
import TwoFactorChallengeForm from '../../components/auth/TwoFactorChallengeForm.vue'

const route = useRoute()
const authStore = useAuthStore()
const redirectAfterSignIn = useSignInRedirect()

const status = ref('verifying')
const error = ref(null)
const pendingChallenge = ref(null)

onMounted(async () => {
  const token = route.query.token
  if (!token) {
    status.value = 'error'
    error.value = 'This verification link is missing its token.'
    return
  }

  try {
    const challenge = await authStore.verifyEmail(token)
    if (challenge) {
      pendingChallenge.value = challenge
      status.value = 'challenge'
      return
    }
    status.value = 'success'
    setTimeout(redirectAfterSignIn, 1500)
  } catch (err) {
    status.value = 'error'
    error.value = err.response?.data?.error || 'This verification link is invalid or has expired.'
  }
})
</script>

<template>
  <TwoFactorChallengeForm
    v-if="status === 'challenge'"
    :challenge="pendingChallenge"
    @success="redirectAfterSignIn"
    @cancel="$router.push('/login')"
  />

  <div v-else class="space-y-5 text-center">
    <template v-if="status === 'verifying'">
      <v-progress-circular indeterminate color="primary" />
      <p class="text-sm text-slate-400">Verifying your email…</p>
    </template>

    <template v-else-if="status === 'success'">
      <v-avatar color="success" variant="tonal" size="48" rounded="xl">
        <v-icon icon="mdi-check-circle-outline" size="26" />
      </v-avatar>
      <div>
        <h2 class="text-xl font-bold text-navy">Email verified</h2>
        <p class="text-sm text-slate-400 mt-1">Your account is active. Signing you in…</p>
      </div>
    </template>

    <template v-else>
      <h2 class="text-xl font-bold text-navy">Verification failed</h2>
      <v-alert type="error" :text="error" class="text-left" />
      <p class="text-sm text-slate-400">
        Sign in to request a new verification link.
      </p>
      <v-btn variant="text" color="primary" to="/login">Go to sign in</v-btn>
    </template>
  </div>
</template>
