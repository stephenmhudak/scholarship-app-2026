<script setup>
import { ref, computed } from 'vue'
import { useAuthStore } from '../../stores/auth'

// challenge: { challengeToken, method: 'totp' | 'email', maskedEmail? } from the sign-in response
const props = defineProps({
  challenge: { type: Object, required: true },
})
const emit = defineEmits(['success', 'cancel'])

const authStore = useAuthStore()

const isEmail = computed(() => props.challenge.method === 'email')
const code = ref('')
const useRecoveryCode = ref(false)
const loading = ref(false)
const error = ref(null)
const resending = ref(false)
const resendMessage = ref(null)

async function verify() {
  loading.value = true
  error.value = null
  try {
    await authStore.completeTwoFactorLogin(props.challenge.challengeToken, code.value)
    emit('success')
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not verify that code. Please try again.'
    code.value = ''
  } finally {
    loading.value = false
  }
}

async function resend() {
  resending.value = true
  resendMessage.value = null
  error.value = null
  try {
    await authStore.resendTwoFactorEmail(props.challenge.challengeToken)
    resendMessage.value = 'A new code is on its way. Earlier codes no longer work.'
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not resend the code. Please try again shortly.'
  } finally {
    resending.value = false
  }
}

function toggleRecoveryCode() {
  useRecoveryCode.value = !useRecoveryCode.value
  code.value = ''
  error.value = null
}
</script>

<template>
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-bold text-navy">Two-factor authentication</h2>
      <p v-if="isEmail" class="text-sm text-slate-400 mt-0.5">
        We emailed a 6-digit code to <span class="font-semibold text-navy">{{ challenge.maskedEmail }}</span>.
        It expires in 10 minutes.
      </p>
      <p v-else-if="useRecoveryCode" class="text-sm text-slate-400 mt-0.5">
        Enter one of the recovery codes you saved when you set up two-factor. Each code works once.
      </p>
      <p v-else class="text-sm text-slate-400 mt-0.5">
        Enter the 6-digit code from your authenticator app.
      </p>
    </div>

    <v-alert v-if="error" type="error" :text="error" closable @click:close="error = null" />
    <v-alert v-if="resendMessage" type="info" :text="resendMessage" closable @click:close="resendMessage = null" />

    <v-form class="space-y-4" @submit.prevent="verify">
      <v-text-field
        v-if="useRecoveryCode"
        :key="'recovery'"
        v-model="code"
        label="Recovery code"
        placeholder="xxxxx-xxxxx"
        autocomplete="off"
        autofocus
        required
        hide-details="auto"
      />
      <v-text-field
        v-else
        :key="'code'"
        v-model="code"
        :label="isEmail ? 'Email code' : 'Authentication code'"
        placeholder="123456"
        inputmode="numeric"
        autocomplete="one-time-code"
        maxlength="6"
        autofocus
        required
        hide-details="auto"
      />

      <v-btn type="submit" color="primary" size="large" block :loading="loading">
        Verify
      </v-btn>
    </v-form>

    <div class="flex justify-between">
      <v-btn variant="text" @click="emit('cancel')">Back</v-btn>
      <v-btn v-if="isEmail" variant="text" color="primary" :loading="resending" @click="resend">Resend code</v-btn>
      <v-btn v-else variant="text" color="primary" @click="toggleRecoveryCode">
        {{ useRecoveryCode ? 'Use authenticator app' : 'Use a recovery code' }}
      </v-btn>
    </div>
  </div>
</template>
