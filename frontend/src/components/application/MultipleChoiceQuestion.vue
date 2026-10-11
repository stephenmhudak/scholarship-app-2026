<script setup>
const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: Array, default: () => [] },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

function onChange(selected) {
  emit('answer', [...selected])
  emit('update:modelValue', [...selected])
}
</script>

<template>
  <div>
    <p class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </p>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
    <v-checkbox
      v-for="option in question.options"
      :key="option.id"
      :model-value="modelValue || []"
      :value="option.id"
      :label="option.text"
      hide-details
      @update:model-value="onChange"
    />
    <p v-if="error" class="text-xs font-medium text-danger mt-1">{{ error }}</p>
  </div>
</template>
