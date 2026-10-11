<script setup>
import { ref } from 'vue'
import { uploadFile, ACCEPTED_REFERENCE_TYPES, MAX_FILE_SIZE } from '../../utils/fileUpload'

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

function onFileChange(value) {
  const selected = (Array.isArray(value) ? value[0] : value) || null
  error.value = null
  if (selected && selected.size > MAX_FILE_SIZE) {
    error.value = 'File must be 5 MB or less.'
    file.value = null
    return
  }
  file.value = selected
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

    <v-file-input
      :model-value="file"
      label="Reference letter"
      :accept="ACCEPTED_REFERENCE_TYPES.join(',')"
      :hint="`${ACCEPTED_REFERENCE_TYPES.join(', ')} up to 5MB`"
      persistent-hint
      prepend-inner-icon="mdi-file-upload-outline"
      :error-messages="error || []"
      @update:model-value="onFileChange"
    />

    <v-btn
      color="primary"
      size="large"
      block
      prepend-icon="mdi-upload"
      :loading="uploading"
      :disabled="!file || uploading"
      @click="upload"
    >
      Upload Reference Letter
    </v-btn>
  </div>
</template>
