<script setup>
import { ref, computed, onMounted } from 'vue'
import QRCode from 'qrcode'
import { useAuthStore } from '../../stores/auth'
import RecoveryCodesList from './RecoveryCodesList.vue'

const emit = defineEmits(['done', 'cancel'])

const authStore = useAuthStore()

const loading = ref(true)
const qrDataUrl = ref(null)
const secret = ref('')
const code = ref('')
const verifying = ref(false)
const error = ref(null)
const recoveryCodes = ref(null)

const groupedSecret = computed(() => secret.value.match(/.{1,4}/g)?.join(' ') ?? '')

onMounted(async () => {
  try {
    const setup = await authStore.beginTwoFactorSetup()
    secret.value = setup.secret
    qrDataUrl.value = await QRCode.toDataURL(setup.otpauthUri, { width: 200, margin: 1 })
  } catch (err) {
    error.value = err.response?.data?.error || 'Could not start two-factor setup. Please try again.'
  } finally {
    loading.value = false
  }
})

async function verify() {
  verifying.value = true
  error.value = null
  try {
    recoveryCodes.value = await authStore.enableTwoFactor(code.value)
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

    <div v-if="loading" class="flex items-center gap-3 text-sm text-slate-400">
      <v-progress-circular indeterminate color="primary" size="20" width="2" />
      Preparing setup…
    </div>

    <template v-else-if="recoveryCodes">
      <v-alert type="success" text="Two-factor authentication is on." />
      <RecoveryCodesList :codes="recoveryCodes" />
      <v-btn color="primary" block @click="emit('done')">I've saved my recovery codes</v-btn>
    </template>

    <template v-else-if="qrDataUrl">
      <div>
        <p class="field-label">1. Scan this QR code</p>
        <p class="text-sm text-slate-400">
          Use an authenticator app such as Google Authenticator, Microsoft Authenticator, or 1Password.
          If you've set this up before, delete the older Scholarship Portal entries from the app first.
        </p>
      </div>
      <div class="flex justify-center">
        <img :src="qrDataUrl" alt="Two-factor setup QR code" width="200" height="200" class="rounded-lg border border-[#E9EDF7]">
      </div>
      <div>
        <p class="eyebrow">Can't scan? Enter this key</p>
        <p class="font-mono text-sm text-navy break-all">{{ groupedSecret }}</p>
      </div>

      <v-form class="space-y-4" @submit.prevent="verify">
        <div>
          <p class="field-label">2. Enter the 6-digit code from the app</p>
          <v-text-field
            v-model="code"
            label="Authentication code"
            placeholder="123456"
            inputmode="numeric"
            autocomplete="one-time-code"
            maxlength="6"
            required
            hide-details="auto"
          />
        </div>
        <div class="flex gap-2">
          <v-btn type="submit" color="primary" :loading="verifying">Turn on two-factor</v-btn>
          <v-btn variant="text" @click="emit('cancel')">Cancel</v-btn>
        </div>
      </v-form>
    </template>

    <v-btn v-else variant="text" @click="emit('cancel')">Back</v-btn>
  </div>
</template>
