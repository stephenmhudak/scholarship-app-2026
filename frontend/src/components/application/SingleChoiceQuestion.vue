<script setup>
const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

function onChange(value) {
  emit('answer', value)
  emit('update:modelValue', value)
}
</script>

<template>
  <div>
    <p class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </p>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
    <v-radio-group
      :model-value="modelValue"
      :error-messages="error || []"
      @update:model-value="onChange"
    >
      <v-radio
        v-for="option in question.options"
        :key="option.id"
        :value="option.id"
        :label="option.text"
      />
    </v-radio-group>
  </div>
</template>
