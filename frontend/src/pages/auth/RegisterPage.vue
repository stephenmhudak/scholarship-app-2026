<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()
const authStore = useAuthStore()

const form = ref({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  passwordConfirmation: '',
})
const loading = ref(false)
const error = ref(null)
const success = ref(null)

async function register() {
  if (form.value.password !== form.value.passwordConfirmation) {
    error.value = 'Passwords do not match.'
    return
  }

  loading.value = true
  error.value = null

  try {
    await api.post('/auth/register', {
      firstName: form.value.firstName,
      lastName: form.value.lastName,
      email: form.value.email,
      password: form.value.password,
    })
    success.value = 'Account created! Signing you in…'
    await authStore.login({ email: form.value.email, password: form.value.password })
    router.push('/dashboard')
  } catch (err) {
    error.value = err.response?.data?.error || 'Registration failed. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h2 class="text-2xl font-bold text-gray-900">Create an account</h2>
      <p class="text-sm text-gray-500 mt-1">Register to apply for the scholarship</p>
    </div>

    <BaseAlert v-if="error" type="error" :message="error" />
    <BaseAlert v-if="success" type="success" :message="success" />

    <form @submit.prevent="register" class="space-y-4">
      <div class="grid grid-cols-2 gap-3">
        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">First Name</label>
          <input
            v-model="form.firstName"
            type="text"
            required
            placeholder="Jane"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
        <div class="space-y-1">
          <label class="block text-sm font-medium text-gray-700">Last Name</label>
          <input
            v-model="form.lastName"
            type="text"
            required
            placeholder="Smith"
            class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Email address</label>
        <input
          v-model="form.email"
          type="email"
          required
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
          placeholder="At least 8 characters"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Confirm Password</label>
        <input
          v-model="form.passwordConfirmation"
          type="password"
          required
          placeholder="Repeat password"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <BaseButton type="submit" :loading="loading" class="w-full">
        Create Account
      </BaseButton>
    </form>

    <p class="text-sm text-center text-gray-500">
      Already have an account?
      <RouterLink to="/login" class="text-blue-600 hover:underline font-medium">Sign in</RouterLink>
    </p>
  </div>
</template>
