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
  success: 'bg-success-light border-l-4 border-success text-success-dark',
  error:   'bg-danger-light border-l-4 border-danger text-danger-dark',
  warning: 'bg-warning-light border-l-4 border-warning text-[#8C6500]',
  info:    'bg-primary-50 border-l-4 border-primary text-primary-800',
}

const icons = {
  success: 'mdi-check-circle-outline',
  error:   'mdi-alert-circle-outline',
  warning: 'mdi-alert-outline',
  info:    'mdi-information-outline',
}
</script>

<template>
  <div
    v-if="!dismissed"
    :class="['flex items-start gap-3 px-4 py-3.5 rounded-xl text-sm', styles[type]]"
    role="alert"
  >
    <span :class="`mdi ${icons[type]} text-lg shrink-0 mt-0.5`"></span>
    <p class="flex-1 font-medium">{{ message }}</p>
    <button
      v-if="dismissible"
      @click="dismissed = true"
      class="shrink-0 opacity-50 hover:opacity-100 transition-opacity ml-1"
    >
      <span class="mdi mdi-close text-sm"></span>
    </button>
  </div>
</template>
