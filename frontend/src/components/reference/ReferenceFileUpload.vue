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
      <h2 class="text-xl font-bold text-navy">Upload Reference Letter</h2>
      <p class="text-sm text-slate-400 mt-1">
        Upload your reference letter for code <strong class="text-navy">{{ referenceCode }}</strong>.
        Accepted formats: {{ ACCEPTED_REFERENCE_TYPES.join(', ') }}
      </p>
    </div>

    <div class="border-2 border-dashed border-[#E9EDF7] bg-[#F4F7FE] rounded-xl p-6 text-center hover:border-primary/40 transition-colors">
      <span class="mdi mdi-file-upload-outline text-4xl text-slate-400 block mb-2"></span>
      <label class="cursor-pointer">
        <span class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors">
          {{ file ? file.name : 'Click to select a file' }}
        </span>
        <input
          type="file"
          :accept="ACCEPTED_REFERENCE_TYPES.join(',')"
          @change="onFileChange"
          class="sr-only"
        />
      </label>
      <p class="text-xs text-slate-400 mt-1">{{ ACCEPTED_REFERENCE_TYPES.join(', ') }} up to 10MB</p>
    </div>

    <p v-if="error" class="text-sm font-medium text-danger">{{ error }}</p>

    <BaseButton @click="upload" :loading="uploading" :disabled="!file || uploading" class="w-full">
      <span class="mdi mdi-upload mr-1.5"></span>
      Upload Reference Letter
    </BaseButton>
  </div>
</template>
