<script setup>
import { ref } from 'vue'
import { uploadFile } from '../../utils/fileUpload'

const props = defineProps({
  question: {
    type: Object,
    required: true,
  },
  modelValue: {
    type: String,
    default: null,
  },
})

const emit = defineEmits(['answer', 'update:modelValue'])

const uploading = ref(false)
const error = ref(null)
const uploadedName = ref(null)

async function onChange(e) {
  const file = e.target.files?.[0]
  if (!file) return

  uploading.value = true
  error.value = null

  try {
    const fileId = await uploadFile('/applications/upload', file)
    uploadedName.value = file.name
    emit('answer', fileId)
    emit('update:modelValue', fileId)
  } catch (err) {
    error.value = 'Upload failed. Please try again.'
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <div class="space-y-2">
    <label class="block text-sm font-medium text-gray-700">
      {{ question.label }}
      <span v-if="question.required" class="text-red-500 ml-0.5">*</span>
    </label>
    <p v-if="question.hint" class="text-xs text-gray-500">{{ question.hint }}</p>

    <div class="border-2 border-dashed border-gray-300 rounded-lg p-4 hover:border-blue-400 transition-colors">
      <label class="flex flex-col items-center gap-2 cursor-pointer">
        <span class="mdi mdi-cloud-upload text-3xl text-gray-400"></span>
        <span class="text-sm text-gray-600">
          Click to select a file
        </span>
        <span v-if="question.accept" class="text-xs text-gray-400">
          Accepted: {{ question.accept }}
        </span>
        <input
          type="file"
          :accept="question.accept"
          :disabled="uploading"
          @change="onChange"
          class="sr-only"
        />
      </label>
    </div>

    <div v-if="uploading" class="flex items-center gap-2 text-sm text-blue-600">
      <span class="mdi mdi-loading animate-spin"></span>
      Uploading...
    </div>

    <div v-else-if="uploadedName" class="flex items-center gap-2 text-sm text-green-600">
      <span class="mdi mdi-check-circle"></span>
      {{ uploadedName }} uploaded successfully
    </div>

    <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
  </div>
</template>
