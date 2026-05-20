<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: Array, default: () => [] },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const selected = ref([...(props.modelValue || [])])

watch(() => props.modelValue, (val) => { selected.value = [...(val || [])] })

function toggle(optionId) {
  const idx = selected.value.indexOf(optionId)
  if (idx === -1) selected.value.push(optionId)
  else selected.value.splice(idx, 1)
  emit('answer', [...selected.value])
  emit('update:modelValue', [...selected.value])
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
          type="checkbox"
          :value="option.id"
          :checked="selected.includes(option.id)"
          @change="toggle(option.id)"
          class="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
        />
        <span class="text-sm text-gray-700">{{ option.text }}</span>
      </label>
    </div>
    <p v-if="error" class="text-xs text-red-600">{{ error }}</p>
  </div>
</template>
