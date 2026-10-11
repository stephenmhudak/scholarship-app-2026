<script setup>
import { ref, computed } from 'vue'
import ReferenceCodeEntry from '../../components/reference/ReferenceCodeEntry.vue'
import ReferenceFileUpload from '../../components/reference/ReferenceFileUpload.vue'

// Steps: 'code' | 'upload' | 'done'
const step = ref('code')
const referenceCode = ref('')
const stepNumber = computed(() => ({ code: 1, upload: 2, done: 2 })[step.value])

function onCodeSubmitted(code) {
  referenceCode.value = code
  step.value = 'upload'
}

function onUploaded() {
  step.value = 'done'
}
</script>

<template>
  <div class="space-y-6">
    <!-- Progress indicator -->
    <v-stepper :model-value="stepNumber" flat class="bg-transparent">
      <v-stepper-header class="shadow-none">
        <v-stepper-item :value="1" title="Code" :complete="step !== 'code'" color="primary" />
        <v-divider />
        <v-stepper-item :value="2" title="Upload" :complete="step === 'done'" color="primary" />
      </v-stepper-header>
    </v-stepper>

    <!-- Step 1: Enter code -->
    <ReferenceCodeEntry
      v-if="step === 'code'"
      @codeSubmitted="onCodeSubmitted"
    />

    <!-- Step 2: Upload file -->
    <ReferenceFileUpload
      v-else-if="step === 'upload'"
      :referenceCode="referenceCode"
      @uploaded="onUploaded"
    />

    <!-- Step 3: Success -->
    <v-empty-state
      v-else
      icon="mdi-check-circle"
      color="success"
      title="Reference Uploaded!"
    >
      <template #text>
        <p class="text-sm text-slate-400">
          Your reference letter has been successfully submitted for code
          <strong class="text-navy">{{ referenceCode }}</strong>.
          The applicant will be notified.
        </p>
        <p class="text-sm text-slate-400 mt-2">You may now close this window.</p>
      </template>
    </v-empty-state>
  </div>
</template>
