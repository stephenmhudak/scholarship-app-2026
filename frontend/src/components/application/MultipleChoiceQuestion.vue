<script setup>
import { ref, watch } from 'vue'

const props = defineProps({
  question: {
    type: Object,
    required: true,
  },
  modelValue: {
    type: Array,
    default: () => [],
  },
})

const emit = defineEmits(['answer', 'update:modelValue'])

const selected = ref([...(props.modelValue || [])])

watch(() => props.modelValue, (val) => {
  selected.value = [...(val || [])]
})

function toggle(option) {
  const idx = selected.value.indexOf(option)
  if (idx === -1) {
    selected.value.push(option)
  } else {
    selected.value.splice(idx, 1)
  }
  emit('answer', [...selected.value])
  emit('update:modelValue', [...selected.value])
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
          type="checkbox"
          :value="option.value"
          :checked="selected.includes(option.value)"
          @change="toggle(option.value)"
          class="w-4 h-4 text-blue-600 border-gray-300 rounded focus:ring-blue-500"
        />
        <span class="text-sm text-gray-700">{{ option.label }}</span>
      </label>
    </div>
  </div>
</template>
