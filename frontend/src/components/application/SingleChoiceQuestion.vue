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

function onChange(e) {
  emit('answer', e.target.value)
  emit('update:modelValue', e.target.value)
}
</script>

<template>
  <div class="space-y-2">
    <p class="block text-sm font-medium text-gray-700">
      {{ question.label }}
      <span v-if="question.required" class="text-red-500 ml-0.5">*</span>
    </p>
    <p v-if="question.hint" class="text-xs text-gray-500">{{ question.hint }}</p>
    <div class="space-y-2">
      <label
        v-for="option in question.options"
        :key="option.value"
        class="flex items-center gap-3 cursor-pointer"
      >
        <input
          type="radio"
          :name="question.id"
          :value="option.value"
          :checked="modelValue === option.value"
          @change="onChange"
          class="w-4 h-4 text-blue-600 border-gray-300 focus:ring-blue-500"
        />
        <span class="text-sm text-gray-700">{{ option.label }}</span>
      </label>
    </div>
  </div>
</template>
