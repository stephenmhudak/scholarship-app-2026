<script setup>
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const status = ref('verifying')
const error = ref(null)

const roleHome = {
  applicant:    '/dashboard',
  scorer:       '/scoring',
  app_admin:    '/admin/applications',
  school_admin: '/schools',
  counselor:    '/counselor',
}

onMounted(async () => {
  const token = route.query.token
  if (!token) {
    status.value = 'error'
    error.value = 'This verification link is missing its token.'
    return
  }

  try {
    await authStore.verifyEmail(token)
    status.value = 'success'
    setTimeout(() => router.replace(roleHome[authStore.role] ?? '/dashboard'), 1500)
  } catch (err) {
    status.value = 'error'
    error.value = err.response?.data?.error || 'This verification link is invalid or has expired.'
  }
})
</script>

<template>
  <div class="space-y-5 text-center">
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
