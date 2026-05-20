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
  <div class="space-y-2">
    <p class="block text-sm font-medium text-gray-700">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-red-500 ml-0.5">*</span>
    </p>
    <p v-if="question.description" class="text-xs text-gray-500">{{ question.description }}</p>
    <div class="space-y-2">
      <label
        v-for="option in question.options"
        :key="option.id"
        class="flex items-center gap-3 cursor-pointer"
      >
        <input
          type="radio"
          :name="question.id"
          :value="option.id"
          :checked="modelValue === option.id"
          @change="onChange"
          class="w-4 h-4 text-blue-600 border-gray-300 focus:ring-blue-500"
        />
        <span class="text-sm text-gray-700">{{ option.text }}</span>
      </label>
    </div>
    <p v-if="error" class="text-xs text-red-600">{{ error }}</p>
  </div>
</template>
