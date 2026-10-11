<script setup>
import { computed } from 'vue'

// VDateInput works with Date objects; answers and cycle dates are 'YYYY-MM-DD' strings.
// Parse and format in local time — new Date('YYYY-MM-DD') is UTC and can shift the day.
const props = defineProps({
  modelValue: { type: String, default: '' },
})
const emit = defineEmits(['update:modelValue'])

const pad = (n) => String(n).padStart(2, '0')

const date = computed({
  get() {
    const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(props.modelValue || '')
    return match ? new Date(+match[1], +match[2] - 1, +match[3]) : null
  },
  set(value) {
    emit('update:modelValue', value
      ? `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`
      : '')
  },
})
</script>

<template>
  <v-date-input v-model="date" />
</template>
