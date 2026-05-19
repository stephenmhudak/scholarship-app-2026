<script setup>
import { ref } from 'vue'
import BaseButton from '../common/BaseButton.vue'

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
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-semibold text-gray-900">Enter Reference Code</h2>
      <p class="text-sm text-gray-500 mt-1">
        Enter the unique code provided by the applicant to upload your reference letter.
      </p>
    </div>
    <div class="space-y-2">
      <label class="block text-sm font-medium text-gray-700">Reference Code</label>
      <input
        v-model="code"
        type="text"
        placeholder="e.g. REF-2026-ABCD1234"
        class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        @keydown.enter="submit"
      />
      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
    </div>
    <BaseButton @click="submit" class="w-full">
      Continue
    </BaseButton>
  </div>
</template>
