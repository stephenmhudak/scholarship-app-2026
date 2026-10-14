<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'
import ReferenceResponse from '../../components/scoring/ReferenceResponse.vue'

// A reference the signed-in referee submitted: who it was for and what they wrote. Scores are never shown here.
const route = useRoute()
const reference = ref(null)
const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    const res = await api.get(`/my-references/${route.params.id}`)
    reference.value = res.data
  } catch (err) {
    error.value = err.response?.status === 404 ? 'Reference not found.' : 'Failed to load the reference.'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6 max-w-3xl">
    <div>
      <v-btn variant="text" color="primary" size="small" prepend-icon="mdi-arrow-left" to="/references" class="-ml-3 mb-1">
        My References
      </v-btn>
      <h1 class="page-title">{{ reference?.formTitle ?? 'Reference' }}</h1>
      <p v-if="reference" class="page-subtitle">For {{ reference.applicantName }} · {{ reference.cycleName }}</p>
    </div>

    <div v-if="loading" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading reference…
    </div>

    <v-alert v-else-if="error" type="error" :text="error" />

    <template v-else-if="reference">
      <v-card title="Submitted for">
        <v-card-text class="pa-6 grid grid-cols-1 sm:grid-cols-3 gap-4">
          <div>
            <p class="eyebrow">Applicant</p>
            <p class="text-sm font-semibold text-navy">{{ reference.applicantName }}</p>
          </div>
          <div>
            <p class="eyebrow">School</p>
            <p class="text-sm font-semibold text-navy">{{ reference.schoolName || '—' }}</p>
          </div>
          <div>
            <p class="eyebrow">Scholarship</p>
            <p class="text-sm font-semibold text-navy">{{ reference.cycleName }}</p>
          </div>
        </v-card-text>
      </v-card>

      <v-card title="Your reference">
        <v-card-text class="pa-6">
          <ReferenceResponse :reference="reference" api-base="/my-references" />
        </v-card-text>
      </v-card>
    </template>
  </div>
</template>
