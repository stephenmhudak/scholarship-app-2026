<script setup>
import { computed } from 'vue'
import RichTextEditor from '../common/RichTextEditor.vue'
import { htmlToText } from '../../utils/richText'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: '' },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const maxLength = computed(() => props.question.validationRules?.maxLength ?? null)
const charCount = computed(() => htmlToText(props.modelValue).length)

function update(html) {
  emit('answer', html)
  emit('update:modelValue', html)
}
</script>

<template>
  <div>
    <label class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
    <RichTextEditor :model-value="modelValue" :error="error" @update:model-value="update" />
    <div class="flex justify-between mt-1">
      <p v-if="error" class="text-xs font-medium text-danger">{{ error }}</p>
      <p v-if="maxLength" class="text-xs text-slate-400 ml-auto">{{ charCount }} / {{ maxLength }}</p>
    </div>
  </div>
</template>
