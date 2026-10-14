<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'
import { normalizeReferenceCode, REFERENCE_CODE_RE } from '../../utils/pendingReference'

// A referee's home: enter a reference code without the link, and the references they've submitted
const router = useRouter()

const code = ref('')
const codeError = ref(null)
const references = ref([])
const loading = ref(true)
const alert = ref(null)

const headers = [
  { title: 'Applicant', key: 'applicantName' },
  { title: 'School', key: 'schoolName' },
  { title: 'Reference', key: 'formTitle' },
  { title: 'Scholarship', key: 'cycleName' },
  { title: 'Submitted', key: 'submittedAt', value: (r) => new Date(r.submittedAt).getTime() },
]

onMounted(async () => {
  try {
    const res = await api.get('/my-references')
    references.value = res.data
  } catch {
    alert.value = { type: 'error', message: 'Failed to load your references.' }
  } finally {
    loading.value = false
  }
})

function openCode() {
  const normalized = normalizeReferenceCode(code.value)
  if (!REFERENCE_CODE_RE.test(normalized)) {
    codeError.value = 'Reference codes are 10 letters and numbers.'
    return
  }
  router.push(`/r/${normalized}`)
}
</script>

<template>
  <div class="space-y-6 max-w-5xl">
    <div>
      <h1 class="page-title">My References</h1>
      <p class="page-subtitle">Write a reference with the code you were given, and look back at the ones you've submitted.</p>
    </div>

    <v-alert v-if="alert" :type="alert.type" :text="alert.message" closable @click:close="alert = null" />

    <v-card title="Enter a reference code">
      <v-card-text class="pa-6">
        <v-form class="flex items-start gap-3" @submit.prevent="openCode">
          <v-text-field
            v-model="code"
            label="Reference code"
            placeholder="e.g. K7QX2M9P4A"
            maxlength="10"
            class="flex-1 max-w-sm font-mono"
            :error-messages="codeError || []"
            hide-details="auto"
            @update:model-value="codeError = null"
          />
          <v-btn type="submit" color="primary" size="large">Continue</v-btn>
        </v-form>
      </v-card-text>
    </v-card>

    <v-card class="overflow-hidden" title="Submitted references">
      <v-data-table
        :headers="headers"
        :items="references"
        :loading="loading"
        class="clickable-rows"
        no-data-text="You haven't submitted any references yet."
        @click:row="(e, { item }) => router.push(`/references/${item.id}`)"
      >
        <template #item.applicantName="{ value }">
          <span class="font-semibold text-navy">{{ value }}</span>
        </template>
        <template #item.schoolName="{ value }">{{ value || '—' }}</template>
        <template #item.formTitle="{ item }">
          {{ item.formTitle }}<span v-if="item.label" class="text-slate-400"> · {{ item.label }}</span>
        </template>
        <template #item.submittedAt="{ item }">{{ new Date(item.submittedAt).toLocaleDateString() }}</template>
      </v-data-table>
    </v-card>
  </div>
</template>
