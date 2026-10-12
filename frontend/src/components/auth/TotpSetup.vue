<script setup>
import { ref, computed, onMounted } from 'vue'
import QRCode from 'qrcode'
import { useAuthStore } from '../../stores/auth'

const emit = defineEmits(['done', 'cancel'])

const authStore = useAuthStore()

const loading = ref(true)
const qrDataUrl = ref(null)
const secret = ref('')
const code = ref('')
const verifying = ref(false)
const error = ref(null)
const recoveryCodes = ref(null)
const copied = ref(false)

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

async function copyCodes() {
  try {
    await navigator.clipboard.writeText(recoveryCodes.value.join('\n'))
    copied.value = true
  } catch {
    error.value = 'Could not copy to the clipboard. Download the codes instead.'
  }
}

function downloadCodes() {
  const text = `Scholarship Portal recovery codes\nEach code can be used once if you lose access to your authenticator app.\n\n${recoveryCodes.value.join('\n')}\n`
  const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }))
  const link = document.createElement('a')
  link.href = url
  link.download = 'scholarship-recovery-codes.txt'
  link.click()
  URL.revokeObjectURL(url)
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
      <div>
        <p class="field-label">Save your recovery codes</p>
        <p class="text-sm text-slate-400">
          If you lose your phone, each of these codes lets you sign in once. Store them somewhere safe.
          They won't be shown again.
        </p>
      </div>
      <div class="grid grid-cols-2 gap-2 p-4 rounded-lg bg-[#F4F7FE] font-mono text-sm text-navy">
        <span v-for="c in recoveryCodes" :key="c">{{ c }}</span>
      </div>
      <div class="flex flex-wrap gap-2">
        <v-btn variant="tonal" prepend-icon="mdi-content-copy" @click="copyCodes">{{ copied ? 'Copied' : 'Copy' }}</v-btn>
        <v-btn variant="tonal" prepend-icon="mdi-download" @click="downloadCodes">Download</v-btn>
      </div>
      <v-btn color="primary" block @click="emit('done')">I've saved my recovery codes</v-btn>
    </template>

    <template v-else-if="qrDataUrl">
      <div>
        <p class="field-label">1. Scan this QR code</p>
        <p class="text-sm text-slate-400">
          Use an authenticator app such as Google Authenticator, Microsoft Authenticator, or 1Password.
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
