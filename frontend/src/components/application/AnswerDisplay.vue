<script setup>
import { ref, computed, watch } from 'vue'
import api from '../../services/api'
import RichTextAnswer from './RichTextAnswer.vue'
import { isRichText } from '../../utils/richText'
import { useSchools } from '../../utils/conditionTargets'

// A saved answer as people read it: choice answers and schools by name (they're stored as ids), dates written out,
// uploaded files as a download button with their file name, rich text as formatted text.
const props = defineProps({
  question: { type: Object, required: true },
  // { textValue, selectedOptions } from the API, or null when unanswered
  answer: { type: Object, default: null },
})

const { schoolName } = useSchools()
const fileName = ref(null)
const downloading = ref(false)

const isFile = computed(() => props.question.type === 'file_upload' && !!props.answer?.textValue)

const text = computed(() => {
  const a = props.answer
  if (!a) return ''
  const optionText = (id) => props.question.options?.find((o) => o.id === id)?.text ?? ''
  if (a.selectedOptions?.length) return a.selectedOptions.map(optionText).filter(Boolean).join(', ')
  if (!a.textValue) return ''
  switch (props.question.type) {
    case 'single_choice': return optionText(a.textValue)
    case 'school_select': return schoolName(a.textValue)
    case 'date': {
      const [y, m, d] = a.textValue.split('-').map(Number)
      return y && m && d ? new Date(y, m - 1, d).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' }) : a.textValue
    }
    default: return a.textValue
  }
})

// The file's name, for its download button
watch(() => (isFile.value ? props.answer.textValue : null), async (fileId) => {
  fileName.value = null
  if (!fileId) return
  try {
    fileName.value = (await api.get(`/files/${fileId}/info`)).data.fileName
  } catch {
    fileName.value = 'Uploaded file'
  }
}, { immediate: true })

async function download() {
  downloading.value = true
  try {
    const res = await api.get(`/files/${props.answer.textValue}`, { responseType: 'blob' })
    const href = URL.createObjectURL(res.data)
    const link = document.createElement('a')
    link.href = href
    link.download = fileName.value || 'file'
    link.click()
    URL.revokeObjectURL(href)
  } finally {
    downloading.value = false
  }
}
</script>

<template>
  <RichTextAnswer v-if="isRichText(question) && answer?.textValue" :html="answer.textValue" />
  <v-btn
    v-else-if="isFile"
    variant="outlined"
    size="small"
    prepend-icon="mdi-download"
    :loading="downloading || !fileName"
    @click="download"
  >
    {{ fileName }}
  </v-btn>
  <p v-else class="text-sm text-navy whitespace-pre-wrap">{{ text || '—' }}</p>
</template>
