<script setup>
import { ref, onMounted } from 'vue'
import api from '../../services/api'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const schools = ref([])
const loadError = ref(null)

onMounted(async () => {
  try {
    const res = await api.get('/schools')
    schools.value = res.data
  } catch {
    loadError.value = 'Could not load schools.'
  }
})

function onChange(e) {
  emit('answer', e.target.value)
  emit('update:modelValue', e.target.value)
}
</script>

<template>
  <div class="space-y-1">
    <label class="block text-sm font-medium text-gray-700">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-red-500 ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-gray-500">{{ question.description }}</p>

    <p v-if="loadError" class="text-xs text-red-600">{{ loadError }}</p>

    <select
      :value="modelValue"
      @change="onChange"
      :class="['block w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500',
        error ? 'border-red-400' : 'border-gray-300']"
    >
      <option value="">Select a school…</option>
      <option v-for="school in schools" :key="school.id" :value="school.id">
        {{ school.name }}
      </option>
    </select>

    <p v-if="error" class="text-xs text-red-600">{{ error }}</p>
  </div>
</template>
