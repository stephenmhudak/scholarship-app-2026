<script setup>
import { ref } from 'vue'
import { uploadFile, ACCEPTED_REFERENCE_TYPES } from '../../utils/fileUpload'
import BaseButton from '../common/BaseButton.vue'

const props = defineProps({
  referenceCode: {
    type: String,
    required: true,
  },
})

const emit = defineEmits(['uploaded'])

const file = ref(null)
const uploading = ref(false)
const error = ref(null)

function onFileChange(e) {
  file.value = e.target.files?.[0] || null
  error.value = null
}

async function upload() {
  if (!file.value) {
    error.value = 'Please select a file to upload.'
    return
  }

  uploading.value = true
  error.value = null

  try {
    const fileId = await uploadFile(`/references/${props.referenceCode}/upload`, file.value)
    emit('uploaded', fileId)
  } catch (err) {
    error.value = 'Upload failed. Please check your file and try again.'
  } finally {
    uploading.value = false
  }
}
</script>

<template>
  <div class="space-y-5">
    <div>
      <h2 class="text-xl font-semibold text-gray-900">Upload Reference Letter</h2>
      <p class="text-sm text-gray-500 mt-1">
        Upload your reference letter for code <strong>{{ referenceCode }}</strong>.
        Accepted formats: {{ ACCEPTED_REFERENCE_TYPES.join(', ') }}
      </p>
    </div>

    <div class="border-2 border-dashed border-gray-300 rounded-lg p-6 text-center hover:border-blue-400 transition-colors">
      <span class="mdi mdi-file-upload text-4xl text-gray-400 block mb-2"></span>
      <label class="cursor-pointer">
        <span class="text-sm text-blue-600 hover:text-blue-700 font-medium">
          {{ file ? file.name : 'Click to select a file' }}
        </span>
        <input
          type="file"
          :accept="ACCEPTED_REFERENCE_TYPES.join(',')"
          @change="onFileChange"
          class="sr-only"
        />
      </label>
      <p class="text-xs text-gray-400 mt-1">{{ ACCEPTED_REFERENCE_TYPES.join(', ') }} up to 10MB</p>
    </div>

    <p v-if="error" class="text-sm text-red-600">{{ error }}</p>

    <BaseButton @click="upload" :loading="uploading" :disabled="!file || uploading" class="w-full">
      <span class="mdi mdi-upload mr-1"></span>
      Upload Reference Letter
    </BaseButton>
  </div>
</template>
