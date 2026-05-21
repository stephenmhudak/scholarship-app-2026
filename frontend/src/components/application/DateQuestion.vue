<script setup>
const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

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
    <input
      type="date"
      :value="modelValue"
      @change="onChange"
      :class="['form-input', error && 'is-error']"
    />
    <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
  </div>
</template>
