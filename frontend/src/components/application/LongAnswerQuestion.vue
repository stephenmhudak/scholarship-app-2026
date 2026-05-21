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
  <div class="space-y-1.5">
    <label class="block text-sm font-semibold text-navy">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400">{{ question.description }}</p>
    <textarea
      :value="modelValue"
      placeholder="Your answer…"
      rows="5"
      @input="onInput"
      :class="['form-input resize-y', error && 'is-error']"
    />
    <div class="flex justify-between">
      <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
      <p v-if="maxLength" class="text-xs text-slate-400 ml-auto">{{ charCount }} / {{ maxLength }}</p>
    </div>
  </div>
</template>
