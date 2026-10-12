<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'

const emit = defineEmits(['done', 'cancel'])

const authStore = useAuthStore()

const sending = ref(true)
const sent = ref(false)
const code = ref('')
const verifying = ref(false)
const enabled = ref(false)
const error = ref(null)
const info = ref(null)

onMounted(() => sendCode())

async function sendCode(isResend = false) {
  sending.value = true
  error.value = null
  info.value = null
  try {
    await authStore.beginEmailTwoFactorSetup()
    sent.value = true
    if (isResend) info.value = 'A new code is on its way. Earlier codes no longer work.'
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not send the code. Please try again shortly.'
  } finally {
    sending.value = false
  }
}

async function verify() {
  verifying.value = true
  error.value = null
  try {
    await authStore.enableEmailTwoFactor(code.value)
    enabled.value = true
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not verify that code. Please try again.'
  } finally {
    verifying.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <v-alert v-if="error" type="error" :text="error" closable @click:close="error = null" />
    <v-alert v-if="info" type="info" :text="info" closable @click:close="info = null" />

    <template v-if="enabled">
      <v-alert type="success" text="Two-factor authentication is on. We'll email you a code each time you sign in." />
      <v-btn color="primary" block @click="emit('done')">Continue</v-btn>
    </template>

    <div v-else-if="sending && !sent" class="flex items-center gap-3 text-sm text-slate-400">
      <v-progress-circular indeterminate color="primary" size="20" width="2" />
      Sending a code to {{ authStore.user?.email }}…
    </div>

    <v-form v-else-if="sent" class="space-y-4" @submit.prevent="verify">
      <div>
        <p class="field-label">Enter the code we emailed you</p>
        <p class="text-sm text-slate-400">
          We sent a 6-digit code to <span class="font-semibold text-navy">{{ authStore.user?.email }}</span>.
          It expires in 10 minutes.
        </p>
      </div>
      <v-text-field
        v-model="code"
        label="Email code"
        placeholder="123456"
        inputmode="numeric"
        autocomplete="one-time-code"
        maxlength="6"
        autofocus
        required
        hide-details="auto"
      />
      <div class="flex flex-wrap gap-2">
        <v-btn type="submit" color="primary" :loading="verifying">Turn on two-factor</v-btn>
        <v-btn variant="text" :loading="sending" @click="sendCode(true)">Resend code</v-btn>
        <v-btn variant="text" @click="emit('cancel')">Cancel</v-btn>
      </div>
    </v-form>

    <div v-else class="flex gap-2">
      <v-btn color="primary" :loading="sending" @click="sendCode()">Try again</v-btn>
      <v-btn variant="text" @click="emit('cancel')">Back</v-btn>
    </div>
  </div>
</template>
