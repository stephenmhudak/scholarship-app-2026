<script setup>
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import api from '../../services/api'
import BaseButton from '../../components/common/BaseButton.vue'
import BaseAlert from '../../components/common/BaseAlert.vue'
import BaseInput from '../../components/common/BaseInput.vue'

const router = useRouter()
const route = useRoute()
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

const inviteToken = ref(null)
const inviteInfo = ref(null)
const inviteError = ref(null)

onMounted(async () => {
  const token = route.query.invite
  if (!token) return

  inviteToken.value = token
  try {
    const res = await api.get(`/auth/invite/${token}`)
    inviteInfo.value = res.data
  } catch (err) {
    inviteError.value = err.response?.data?.error || 'This invite link is invalid or has expired.'
  }
})

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
      inviteToken: inviteToken.value ?? undefined,
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
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Create an account</h2>
      <p v-if="inviteInfo" class="text-sm text-slate-400 mt-0.5">
        You're registering as a school admin for <strong class="text-navy">{{ inviteInfo.schoolName }}</strong>
      </p>
      <p v-else class="text-sm text-slate-400 mt-0.5">Register to apply for the scholarship</p>
    </div>

    <BaseAlert v-if="inviteError" type="error" :message="inviteError" />
    <BaseAlert v-if="error" type="error" :message="error" />
    <BaseAlert v-if="success" type="success" :message="success" />

    <form @submit.prevent="register" class="space-y-4">
      <div class="grid grid-cols-2 gap-3">
        <BaseInput
          v-model="form.firstName"
          label="First Name"
          placeholder="Jane"
          :required="true"
        />
        <BaseInput
          v-model="form.lastName"
          label="Last Name"
          placeholder="Smith"
          :required="true"
        />
      </div>

      <BaseInput
        v-model="form.email"
        type="email"
        label="Email address"
        placeholder="you@example.com"
        :required="true"
      />

      <BaseInput
        v-model="form.password"
        type="password"
        label="Password"
        placeholder="At least 8 characters"
        :required="true"
      />

      <BaseInput
        v-model="form.passwordConfirmation"
        type="password"
        label="Confirm Password"
        placeholder="Repeat password"
        :required="true"
      />

      <BaseButton type="submit" :loading="loading" :disabled="!!inviteError" class="w-full mt-1">
        Create Account
      </BaseButton>
    </form>

    <p class="text-sm text-center text-slate-400">
      Already have an account?
      <RouterLink to="/login" class="text-primary font-semibold hover:underline">Sign in</RouterLink>
    </p>
  </div>
</template>
