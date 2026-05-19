<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'

const router = useRouter()

const form = ref({
  name: '',
  email: '',
  password: '',
  password_confirmation: '',
  role: 'applicant',
})
const loading = ref(false)
const error = ref(null)
const success = ref(null)

const roles = [
  { value: 'applicant', label: 'Applicant' },
  { value: 'scorer', label: 'Scorer' },
  { value: 'school_admin', label: 'School Administrator' },
  { value: 'counselor', label: 'Counselor' },
]

async function register() {
  if (form.value.password !== form.value.password_confirmation) {
    error.value = 'Passwords do not match.'
    return
  }

  loading.value = true
  error.value = null

  try {
    await api.post('/auth/register', form.value)
    success.value = 'Account created! You can now sign in.'
    setTimeout(() => router.push('/login'), 2000)
  } catch (err) {
    error.value = err.response?.data?.message || 'Registration failed. Please try again.'
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
      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Full Name</label>
        <input
          v-model="form.name"
          type="text"
          required
          placeholder="Jane Smith"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
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
          v-model="form.password_confirmation"
          type="password"
          required
          placeholder="Repeat password"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      <div class="space-y-1">
        <label class="block text-sm font-medium text-gray-700">Role</label>
        <select
          v-model="form.role"
          class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          <option v-for="r in roles" :key="r.value" :value="r.value">{{ r.label }}</option>
        </select>
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
