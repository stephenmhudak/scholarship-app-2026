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
const loading = ref(true)
const loadError = ref(null)

onMounted(async () => {
  try {
    const res = await api.get('/schools')
    schools.value = res.data
  } catch {
    loadError.value = 'Could not load schools.'
  } finally {
    loading.value = false
  }
})

function onChange(value) {
  emit('answer', value ?? '')
  emit('update:modelValue', value ?? '')
}
</script>

<template>
  <div>
    <label class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
    <v-autocomplete
      :model-value="modelValue || null"
      :items="schools"
      item-title="name"
      item-value="id"
      placeholder="Select a school…"
      :loading="loading"
      clearable
      :error-messages="error || loadError || []"
      hide-details="auto"
      @update:model-value="onChange"
    />
  </div>
</template>
