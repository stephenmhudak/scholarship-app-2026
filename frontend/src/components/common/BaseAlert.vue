<script setup>
import { ref } from 'vue'

const props = defineProps({
  type: {
    type: String,
    default: 'info',
    validator: (v) => ['success', 'error', 'warning', 'info'].includes(v),
  },
  message: {
    type: String,
    required: true,
  },
  dismissible: {
    type: Boolean,
    default: true,
  },
})

const dismissed = ref(false)

const styles = {
  success: 'bg-green-50 border-green-400 text-green-800',
  error: 'bg-red-50 border-red-400 text-red-800',
  warning: 'bg-yellow-50 border-yellow-400 text-yellow-800',
  info: 'bg-blue-50 border-blue-400 text-blue-800',
}

const icons = {
  success: 'mdi-check-circle',
  error: 'mdi-alert-circle',
  warning: 'mdi-alert',
  info: 'mdi-information',
}
</script>

<template>
  <div
    v-if="!dismissed"
    :class="['flex items-start gap-3 px-4 py-3 rounded-lg border text-sm', styles[type]]"
    role="alert"
  >
    <span :class="`mdi ${icons[type]} text-lg shrink-0 mt-0.5`"></span>
    <p class="flex-1">{{ message }}</p>
    <button
      v-if="dismissible"
      @click="dismissed = true"
      class="shrink-0 opacity-60 hover:opacity-100 transition-opacity"
    >
      <span class="mdi mdi-close"></span>
    </button>
  </div>
</template>
