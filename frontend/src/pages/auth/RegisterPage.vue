<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import api from '../../services/api'

const route = useRoute()
const authStore = useAuthStore()

const form = ref({
  firstName: '',
  lastName: '',
  email: '',
  password: '',
  passwordConfirmation: '',
  schoolName: '',
  schoolAddressLine1: '',
  schoolAddressLine2: '',
  schoolCity: '',
  schoolState: '',
  schoolZip: '',
})
const loading = ref(false)
const error = ref(null)
const registeredEmail = ref(null)
const resending = ref(false)
const resendMessage = ref(null)

// /register?as=referee: an account for writing references rather than applying
const isReferee = route.query.as === 'referee' && !route.query.invite

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
      accountType: isReferee ? 'referee' : undefined,
      inviteToken: inviteToken.value ?? undefined,
      schoolName: inviteToken.value ? form.value.schoolName : undefined,
      schoolAddressLine1: inviteToken.value ? form.value.schoolAddressLine1 : undefined,
      schoolAddressLine2: inviteToken.value ? form.value.schoolAddressLine2 : undefined,
      schoolCity: inviteToken.value ? form.value.schoolCity : undefined,
      schoolState: inviteToken.value ? form.value.schoolState : undefined,
      schoolZip: inviteToken.value ? form.value.schoolZip : undefined,
    })
    registeredEmail.value = form.value.email
  } catch (err) {
    error.value = err.response?.data?.error || 'Registration failed. Please try again.'
  } finally {
    loading.value = false
  }
}

async function resend() {
  resending.value = true
  resendMessage.value = null
  try {
    await authStore.resendVerification(registeredEmail.value)
    resendMessage.value = 'If your account still needs verifying, a new link is on its way.'
  } catch {
    resendMessage.value = 'Could not resend the email. Please try again shortly.'
  } finally {
    resending.value = false
  }
}
</script>

<template>
  <div v-if="registeredEmail" class="space-y-5 text-center">
    <v-avatar color="primary" variant="tonal" size="48" rounded="xl">
      <v-icon icon="mdi-email-check-outline" size="26" />
    </v-avatar>
    <div>
      <h2 class="text-xl font-bold text-navy">Check your email</h2>
      <p class="text-sm text-slate-400 mt-1">
        We sent a verification link to <span class="font-semibold text-navy">{{ registeredEmail }}</span>.
        Click it to activate your account. The link expires in 24 hours.
      </p>
    </div>

    <v-alert v-if="resendMessage" type="info" :text="resendMessage" closable class="text-left" @click:close="resendMessage = null" />

    <v-btn variant="outlined" block :loading="resending" @click="resend">
      Resend verification email
    </v-btn>

    <p class="text-sm text-slate-400">
      Already verified?
      <RouterLink to="/login" class="text-primary font-semibold hover:underline">Sign in</RouterLink>
    </p>
  </div>

  <div v-else class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">{{ isReferee ? 'Create a referee account' : 'Create an account' }}</h2>
      <p v-if="inviteInfo" class="text-sm text-slate-400 mt-0.5">You're registering as a school admin. Enter your school information below.</p>
      <p v-else-if="isReferee" class="text-sm text-slate-400 mt-0.5">Write references for scholarship applicants and see the ones you've submitted.</p>
      <p v-else class="text-sm text-slate-400 mt-0.5">Register to apply for the scholarship</p>
    </div>

    <v-alert v-if="inviteError" type="error" :text="inviteError" />
    <v-alert v-if="error" type="error" :text="error" closable @click:close="error = null" />

    <v-form class="space-y-4" @submit.prevent="register">
      <div class="grid grid-cols-2 gap-3">
        <v-text-field v-model="form.firstName" label="First Name" placeholder="Jane" required hide-details="auto" />
        <v-text-field v-model="form.lastName" label="Last Name" placeholder="Smith" required hide-details="auto" />
      </div>

      <v-text-field
        v-model="form.email"
        type="email"
        label="Email address"
        placeholder="you@example.com"
        required
        hide-details="auto"
      />

      <v-text-field
        v-model="form.password"
        type="password"
        label="Password"
        hint="At least 8 characters"
        required
        hide-details="auto"
      />

      <v-text-field
        v-model="form.passwordConfirmation"
        type="password"
        label="Confirm Password"
        placeholder="Repeat password"
        required
        hide-details="auto"
      />

      <template v-if="inviteToken">
        <v-divider />
        <p class="text-xs font-semibold text-slate-400 uppercase tracking-widest">School Information</p>
        <v-text-field v-model="form.schoolName" label="School Name" placeholder="Lincoln High School" required hide-details="auto" />
        <v-text-field v-model="form.schoolAddressLine1" label="Address Line 1" placeholder="123 Main St" hide-details="auto" />
        <v-text-field v-model="form.schoolAddressLine2" label="Address Line 2" placeholder="Suite 100 (optional)" hide-details="auto" />
        <div class="grid grid-cols-3 gap-3">
          <v-text-field v-model="form.schoolCity" label="City" placeholder="Springfield" hide-details="auto" />
          <v-text-field v-model="form.schoolState" label="State" placeholder="IL" hide-details="auto" />
          <v-text-field v-model="form.schoolZip" label="ZIP Code" placeholder="62701" hide-details="auto" />
        </div>
      </template>

      <v-btn type="submit" color="primary" size="large" block :loading="loading" :disabled="!!inviteError">
        Create Account
      </v-btn>
    </v-form>

    <p class="text-sm text-center text-slate-400">
      Already have an account?
      <RouterLink to="/login" class="text-primary font-semibold hover:underline">Sign in</RouterLink>
    </p>
  </div>
</template>
