<script setup>
import { computed } from 'vue'
import ShortAnswerQuestion from './ShortAnswerQuestion.vue'
import LongAnswerQuestion from './LongAnswerQuestion.vue'
import MultipleChoiceQuestion from './MultipleChoiceQuestion.vue'
import SingleChoiceQuestion from './SingleChoiceQuestion.vue'
import FileUploadQuestion from './FileUploadQuestion.vue'
import SchoolSelectQuestion from './SchoolSelectQuestion.vue'
import DateQuestion from './DateQuestion.vue'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: [String, Array], default: null },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const componentMap = {
  short_answer: ShortAnswerQuestion,
  long_answer: LongAnswerQuestion,
  multiple_choice: MultipleChoiceQuestion,
  single_choice: SingleChoiceQuestion,
  file_upload: FileUploadQuestion,
  school_select: SchoolSelectQuestion,
  date: DateQuestion,
}

const questionComponent = computed(() => componentMap[props.question.type] ?? ShortAnswerQuestion)
</script>

<template>
  <div class="bg-white border border-gray-200 rounded-xl p-5 shadow-sm">
    <component
      :is="questionComponent"
      :question="question"
      :modelValue="modelValue"
      :error="error"
      @answer="(val) => emit('answer', val)"
      @update:modelValue="(val) => emit('update:modelValue', val)"
    />
  </div>
</template>
