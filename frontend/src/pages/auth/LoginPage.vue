<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()
const authStore = useAuthStore()

const form = ref({
  email: '',
  password: '',
})
const loading = ref(false)
const error = ref(null)

async function login() {
  loading.value = true
  error.value = null

  try {
    await authStore.login(form.value)
    router.push('/dashboard')
  } catch (err) {
    error.value = err.response?.data?.message || 'Invalid email or password. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h2 class="text-2xl font-bold text-gray-900">Sign in</h2>
      <p class="text-sm text-gray-500 mt-1">Access your scholarship account</p>
    </div>

    <BaseAlert v-if="error" type="error" :message="error" />

    <form @submit.prevent="login" class="space-y-4">
      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Email address</label>
        <input
          v-model="form.email"
          type="email"
          required
          autocomplete="email"
          placeholder="you@example.com"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Password</label>
        <input
          v-model="form.password"
          type="password"
          required
          autocomplete="current-password"
          placeholder="••••••••"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <BaseButton type="submit" :loading="loading" class="w-full">
        Sign in
      </BaseButton>
    </form>

    <p class="text-sm text-center text-gray-500">
      Don't have an account?
      <RouterLink to="/register" class="text-blue-600 hover:underline font-medium">Register</RouterLink>
    </p>
  </div>
</template>
