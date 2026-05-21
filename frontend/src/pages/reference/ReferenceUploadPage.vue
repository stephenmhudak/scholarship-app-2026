<script setup>
import { ref } from 'vue'
import ReferenceCodeEntry from '../../components/reference/ReferenceCodeEntry.vue'
import ReferenceFileUpload from '../../components/reference/ReferenceFileUpload.vue'

// Steps: 'code' | 'upload' | 'done'
const step = ref('code')
const referenceCode = ref('')

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
    <div class="flex items-center gap-3 mb-2">
      <div
        :class="[
          'w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold',
          step !== 'code' ? 'bg-primary text-white' : 'bg-primary-100 text-primary-700',
        ]"
      >1</div>
      <div class="flex-1 h-0.5 bg-[#E9EDF7]"></div>
      <div
        :class="[
          'w-7 h-7 rounded-full flex items-center justify-center text-xs font-bold',
          step === 'done' ? 'bg-primary text-white' : step === 'upload' ? 'bg-primary-100 text-primary-700' : 'bg-[#F4F7FE] text-slate-400',
        ]"
      >2</div>
    </div>

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
    <div v-else class="text-center space-y-4 py-4">
      <span class="mdi mdi-check-circle text-5xl text-success block"></span>
      <h2 class="text-xl font-bold text-navy">Reference Uploaded!</h2>
      <p class="text-sm text-slate-400">
        Your reference letter has been successfully submitted for code
        <strong class="text-navy">{{ referenceCode }}</strong>.
        The applicant will be notified.
      </p>
      <p class="text-sm text-slate-400">You may now close this window.</p>
    </div>
  </div>
</template>
