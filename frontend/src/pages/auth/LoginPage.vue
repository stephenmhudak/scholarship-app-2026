<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseInput from '../../components/common/BaseInput.vue'

const router = useRouter()
const authStore = useAuthStore()

const form = ref({
  email: '',
  password: '',
})
const loading = ref(false)
const error = ref(null)
const unverifiedEmail = ref(null)
const resending = ref(false)
const resendMessage = ref(null)

const roleHome = {
  applicant:    '/dashboard',
  scorer:       '/scoring',
  app_admin:    '/admin/applications',
  school_admin: '/schools',
  counselor:    '/counselor',
}

async function login() {
  loading.value = true
  error.value = null
  unverifiedEmail.value = null
  resendMessage.value = null

  try {
    await authStore.login(form.value)
    router.push(roleHome[authStore.role] ?? '/dashboard')
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
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Sign in</h2>
      <p class="text-sm text-slate-400 mt-0.5">Access your scholarship account</p>
    </div>

    <BaseAlert v-if="error" type="error" :message="error" />

    <div v-if="unverifiedEmail" class="space-y-3">
      <BaseAlert
        type="warning"
        :dismissible="false"
        message="Please verify your email address before signing in. Check your inbox for the link we sent."
      />
      <BaseAlert v-if="resendMessage" type="info" :message="resendMessage" />
      <BaseButton variant="secondary" size="sm" :loading="resending" class="w-full" @click="resend">
        Resend verification email
      </BaseButton>
    </div>

    <form @submit.prevent="login" class="space-y-4">
      <BaseInput
        v-model="form.email"
        type="email"
        label="Email address"
        placeholder="you@example.com"
        autocomplete="email"
        :required="true"
      />

      <BaseInput
        v-model="form.password"
        type="password"
        label="Password"
        placeholder="••••••••"
        autocomplete="current-password"
        :required="true"
      />

      <BaseButton type="submit" :loading="loading" class="w-full mt-1">
        Sign in
      </BaseButton>
    </form>

    <p class="text-sm text-center text-slate-400">
      Don't have an account?
      <RouterLink to="/register" class="text-primary font-semibold hover:underline">Register</RouterLink>
    </p>
  </div>
</template>
