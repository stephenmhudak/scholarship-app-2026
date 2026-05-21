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
  <div class="space-y-1.5">
    <label class="block text-sm font-semibold text-navy">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400">{{ question.description }}</p>
    <p v-if="loadError" class="text-xs font-medium text-danger">{{ loadError }}</p>
    <select
      :value="modelValue"
      @change="onChange"
      :class="['form-select w-full', error && 'is-error']"
    >
      <option value="">Select a school…</option>
      <option v-for="school in schools" :key="school.id" :value="school.id">
        {{ school.name }}
      </option>
    </select>
    <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
  </div>
</template>
