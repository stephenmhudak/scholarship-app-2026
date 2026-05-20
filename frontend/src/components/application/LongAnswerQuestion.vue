<script setup>
import { computed } from 'vue'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const maxLength = computed(() => props.question.validationRules?.maxLength ?? null)
const charCount = computed(() => (props.modelValue || '').length)

function onInput(e) {
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
    <textarea
      :value="modelValue"
      placeholder="Your answer…"
      rows="5"
      @input="onInput"
      :class="['block w-full border rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent resize-y',
        error ? 'border-red-400' : 'border-gray-300']"
    />
    <div class="flex justify-between">
      <p v-if="error" class="text-xs text-red-600">{{ error }}</p>
      <p v-if="maxLength" class="text-xs text-gray-400 ml-auto">{{ charCount }} / {{ maxLength }}</p>
    </div>
  </div>
</template>
