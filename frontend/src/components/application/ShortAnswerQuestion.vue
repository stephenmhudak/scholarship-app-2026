<script setup>
defineProps({
  question: {
    type: Object,
    required: true,
  },
  modelValue: {
    type: String,
    default: '',
  },
})

const emit = defineEmits(['answer', 'update:modelValue'])

function onInput(e) {
  emit('answer', e.target.value)
  emit('update:modelValue', e.target.value)
}
</script>

<template>
  <div class="space-y-1">
    <label class="block text-sm font-medium text-gray-700">
      {{ question.label }}
      <span v-if="question.required" class="text-red-500 ml-0.5">*</span>
    </label>
    <input
      type="text"
      :value="modelValue"
      :placeholder="question.placeholder || 'Your answer...'"
      :required="question.required"
      @input="onInput"
      class="block w-full border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent"
    />
    <p v-if="question.hint" class="text-xs text-gray-500">{{ question.hint }}</p>
  </div>
</template>
