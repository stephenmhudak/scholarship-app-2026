<script setup>
import { ref } from 'vue'
import api from '../../services/api'

const props = defineProps({
  question: { type: Object, required: true },
  modelValue: { type: String, default: null },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const uploading = ref(false)
const uploadError = ref(null)
const uploadedName = ref(null)

async function onChange(e) {
  const file = e.target.files?.[0]
  if (!file) return
  uploading.value = true
  uploadError.value = null
  try {
    const formData = new FormData()
    formData.append('file', file)
    const response = await api.post('/files/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    uploadedName.value = file.name
    emit('answer', response.data.fileId)
    emit('update:modelValue', response.data.fileId)
  } catch {
    uploadError.value = 'Upload failed. Please try again.'
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <div class="space-y-2">
    <label class="block text-sm font-semibold text-navy">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400">{{ question.description }}</p>

    <div :class="['border-2 border-dashed rounded-xl p-5 hover:border-primary/40 transition-colors',
      error ? 'border-danger/40 bg-danger/5' : 'border-[#E9EDF7] bg-[#F4F7FE]']">
      <label class="flex flex-col items-center gap-2 cursor-pointer">
        <span class="mdi mdi-cloud-upload-outline text-3xl text-slate-400"></span>
        <span class="text-sm font-medium text-slate-400">Click to select a file</span>
        <input type="file" :disabled="uploading" @change="onChange" class="sr-only" />
      </label>
    </div>

    <div v-if="uploading" class="flex items-center gap-2 text-sm font-medium text-primary">
      <span class="mdi mdi-loading animate-spin"></span> Uploading…
    </div>
    <div v-else-if="uploadedName" class="flex items-center gap-2 text-sm font-medium text-success-dark">
      <span class="mdi mdi-check-circle-outline"></span> {{ uploadedName }} uploaded
    </div>

    <p v-if="uploadError || error" class="text-xs font-medium text-danger">{{ uploadError || error }}</p>
  </div>
</template>
