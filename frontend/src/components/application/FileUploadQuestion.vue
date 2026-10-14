<script setup>
import { ref, watch } from 'vue'
import api from '../../services/api'
import { MAX_FILE_SIZE } from '../../utils/fileUpload'

const props = defineProps({
  question: { type: Object, required: true },
  applicationId: { type: String, default: null },
  // Where to send the file instead of /files/upload (reference links); the response is { fileId } either way
  uploadUrl: { type: String, default: null },
  modelValue: { type: String, default: null },
  error: { type: String, default: null },
})
const emit = defineEmits(['answer', 'update:modelValue'])

const file = ref(null)
const uploading = ref(false)
const uploadError = ref(null)
const uploadedName = ref(null)
// Id the current uploadedName belongs to, so a fresh upload doesn't trigger a lookup.
const uploadedId = ref(null)

// When resuming a draft the answer holds a fileId from an earlier session; look up its name.
watch(() => props.modelValue, async (fileId) => {
  if (!fileId) {
    uploadedName.value = null
    uploadedId.value = null
    return
  }
  if (fileId === uploadedId.value) return
  uploadedId.value = fileId
  // Reference uploads have no info endpoint; their answers are only ever set by an upload here
  if (props.uploadUrl) return
  try {
    const { data } = await api.get(`/files/${fileId}/info`)
    if (uploadedId.value === fileId) uploadedName.value = data.fileName
  } catch {
    if (uploadedId.value === fileId) uploadedName.value = 'A file'
  }
}, { immediate: true })

async function onChange(value) {
  const selected = Array.isArray(value) ? value[0] : value
  if (!selected) return
  if (selected.size > MAX_FILE_SIZE) {
    uploadError.value = 'File must be 5 MB or less.'
    file.value = null
    return
  }
  uploading.value = true
  uploadError.value = null
  try {
    const formData = new FormData()
    formData.append('file', selected)
    if (!props.uploadUrl) formData.append('applicationId', props.applicationId)
    formData.append('questionId', props.question.id)
    const response = await api.post(props.uploadUrl ?? '/files/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    uploadedName.value = selected.name
    uploadedId.value = response.data.fileId
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
  <div>
    <label class="field-label">
      {{ question.text }}
      <span v-if="question.isRequired" class="text-danger ml-0.5">*</span>
    </label>
    <p v-if="question.description" class="text-xs text-slate-400 mb-2">{{ question.description }}</p>
    <v-file-input
      v-model="file"
      :placeholder="uploadedName ? 'Click to replace the uploaded file' : 'Click to select a file'"
      hint="Max 5 MB"
      persistent-hint
      :loading="uploading"
      :disabled="uploading"
      :error-messages="uploadError || error || []"
      @update:model-value="onChange"
    />
    <div v-if="uploadedName && !uploading" class="flex items-center gap-2 text-sm font-medium text-success-dark mt-1">
      <v-icon icon="mdi-check-circle-outline" size="18" /> {{ uploadedName }} uploaded
    </div>
  </div>
</template>
