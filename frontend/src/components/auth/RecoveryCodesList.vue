<script setup>
import { ref } from 'vue'

const props = defineProps({
  codes: { type: Array, required: true },
})

const copied = ref(false)
const copyError = ref(false)

async function copyCodes() {
  try {
    await navigator.clipboard.writeText(props.codes.join('\n'))
    copied.value = true
  } catch {
    copyError.value = true
  }
}

function downloadCodes() {
  const text = `Scholarship Portal recovery codes\nEach code can be used once if you lose access to your authenticator app.\n\n${props.codes.join('\n')}\n`
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
    <div>
      <p class="field-label">Save your recovery codes</p>
      <p class="text-sm text-slate-400">
        If you lose your phone, each of these codes lets you sign in once. Store them somewhere safe.
        They won't be shown again.
      </p>
    </div>
    <v-alert v-if="copyError" type="error" text="Could not copy to the clipboard. Download the codes instead." closable @click:close="copyError = false" />
    <div class="grid grid-cols-2 gap-2 p-4 rounded-lg bg-[#F4F7FE] font-mono text-sm text-navy">
      <span v-for="c in codes" :key="c">{{ c }}</span>
    </div>
    <div class="flex flex-wrap gap-2">
      <v-btn variant="tonal" prepend-icon="mdi-content-copy" @click="copyCodes">{{ copied ? 'Copied' : 'Copy' }}</v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-download" @click="downloadCodes">Download</v-btn>
    </div>
  </div>
</template>
