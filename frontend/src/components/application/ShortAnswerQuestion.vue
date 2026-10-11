<script setup>
const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

function onInput(value) {
  emit('answer', value)
  emit('update:modelValue', value)
}
</script>

<template>
  <div>
    <label class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
    <v-text-field
      :model-value="modelValue"
      placeholder="Your answer…"
      :error-messages="error || []"
      hide-details="auto"
      @update:model-value="onInput"
    />
  </div>
</template>
