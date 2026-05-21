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
    <p class="block text-sm font-semibold text-navy">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </p>
    <p v-if="question.description" class="text-xs text-slate-400">{{ question.description }}</p>
    <div class="space-y-2.5 pt-0.5">
      <label
        v-for="option in question.options"
        :key="option.id"
        class="flex items-center gap-3 cursor-pointer group"
      >
        <input
          type="radio"
          :name="question.id"
          :value="option.id"
          :checked="modelValue === option.id"
          @change="onChange"
          class="w-4 h-4 border-[#E9EDF7] text-primary focus:ring-primary/30"
        />
        <span class="text-sm text-navy/80 group-hover:text-navy transition-colors">{{ option.text }}</span>
      </label>
    </div>
    <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
  </div>
</template>
