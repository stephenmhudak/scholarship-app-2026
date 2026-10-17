<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../../services/api'
import ReferenceFormsCard from '../../../components/admin/ReferenceFormsCard.vue'

// The period's reference forms; their conditions look at the application questions
const cycleId = useRoute().params.cycleId
const questions = ref(null)
const error = ref(null)

onMounted(async () => {
  try {
    questions.value = (await api.get(`/cycles/${cycleId}/questions`)).data
  } catch {
    error.value = 'Failed to load the application questions.'
  }
})
</script>

<template>
  <div class="max-w-3xl">
    <v-alert v-if="error" type="error" :text="error" />
    <div v-else-if="!questions" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading…
    </div>
    <ReferenceFormsCard v-else :cycleId="cycleId" :questions="questions" />
  </div>
</template>
