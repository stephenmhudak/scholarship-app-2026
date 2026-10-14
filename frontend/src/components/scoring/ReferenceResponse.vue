<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue'
import api from '../../services/api'
import RichTextAnswer from '../application/RichTextAnswer.vue'

// One submitted reference: the referee's answers, files, name and drawn signature. Used by scorers, and by the
// referee to look back at what they wrote (never with scores).
const props = defineProps({
  reference: { type: Object, required: true },
  // Where the signature and files are fetched from: '/scoring/references' or '/my-references'
  apiBase: { type: String, default: '/scoring/references' },
  // Scorers see which account it came from
  showAccount: { type: Boolean, default: false },
})

const signatureUrl = ref(null)
const signatureError = ref(false)
const downloading = ref(null)

// The signature and files need the scorer's token, so they're fetched as blobs rather than linked directly
onMounted(async () => {
  if (!props.reference.hasSignature) return
  try {
    const res = await api.get(`${props.apiBase}/${props.reference.id}/signature`, { responseType: 'blob' })
    signatureUrl.value = URL.createObjectURL(res.data)
  } catch {
    signatureError.value = true
  }
})

onBeforeUnmount(() => {
  if (signatureUrl.value) URL.revokeObjectURL(signatureUrl.value)
})

async function download(answer) {
  downloading.value = answer.fileId
  try {
    const res = await api.get(`${props.apiBase}/files/${answer.fileId}`, { responseType: 'blob' })
    const url = URL.createObjectURL(res.data)
    const link = document.createElement('a')
    link.href = url
    link.download = answer.fileName || 'reference-file'
    link.click()
    URL.revokeObjectURL(url)
  } finally {
    downloading.value = null
  }
}

function hasAnswer(answer) {
  return answer.textValue || answer.selectedOptions?.length || answer.fileId
}
</script>

<template>
  <div class="space-y-4">
    <div v-for="answer in reference.answers" :key="answer.questionId">
      <p class="eyebrow mb-1.5">{{ answer.questionText }}</p>
      <template v-if="hasAnswer(answer)">
        <RichTextAnswer v-if="answer.richText" :html="answer.textValue" />
        <v-btn
          v-else-if="answer.fileId"
          variant="outlined"
          size="small"
          prepend-icon="mdi-download"
          :loading="downloading === answer.fileId"
          @click="download(answer)"
        >
          {{ answer.fileName }}
        </v-btn>
        <p v-else class="text-sm text-navy whitespace-pre-wrap">
          {{ answer.selectedOptions?.length ? answer.selectedOptions.join(', ') : answer.textValue }}
        </p>
      </template>
      <p v-else class="text-sm text-slate-400">—</p>
    </div>

    <v-divider />

    <div class="space-y-2">
      <p class="eyebrow">Signed</p>
      <img
        v-if="signatureUrl"
        :src="signatureUrl"
        :alt="`Signature of ${reference.signerName}`"
        class="max-h-28 rounded border border-[#E9EDF7] bg-white"
      >
      <p v-else-if="signatureError" class="text-sm text-slate-400">The signature couldn't be loaded.</p>
      <p class="text-sm text-navy">
        <span class="font-semibold">{{ reference.signerName }}</span>
        <span class="text-slate-400"> · {{ new Date(reference.submittedAt).toLocaleDateString() }}</span>
      </p>
      <p v-if="showAccount && reference.submittedByEmail" class="text-xs text-slate-400">
        Submitted from the account {{ reference.submittedByEmail }}
      </p>
    </div>
  </div>
</template>
