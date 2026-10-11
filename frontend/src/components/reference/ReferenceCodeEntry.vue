<script setup>
import { ref } from 'vue'

const emit = defineEmits(['codeSubmitted'])

const code = ref('')
const error = ref(null)

function submit() {
  if (!code.value.trim()) {
    error.value = 'Please enter a reference code.'
    return
  }
  error.value = null
  emit('codeSubmitted', code.value.trim())
}
</script>

<template>
  <v-form class="space-y-5" @submit.prevent="submit">
    <div>
      <h2 class="text-xl font-bold text-navy">Enter Reference Code</h2>
      <p class="text-sm text-slate-400 mt-1">
        Enter the unique code provided by the applicant to upload your reference letter.
      </p>
    </div>
    <v-text-field
      v-model="code"
      label="Reference Code"
      placeholder="e.g. REF-2026-ABCD1234"
      :error-messages="error || []"
      hide-details="auto"
    />
    <v-btn type="submit" color="primary" size="large" block>
      Continue
    </v-btn>
  </v-form>
</template>
