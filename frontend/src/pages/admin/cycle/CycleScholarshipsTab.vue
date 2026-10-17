<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../../services/api'
import ScholarshipsCard from '../../../components/admin/ScholarshipsCard.vue'

// The period's scholarships; eligibility rules look at the application questions (but not scholarships' own sections)
const cycleId = useRoute().params.cycleId
const questions = ref(null)
const sections = ref([])
const error = ref(null)

async function load() {
  try {
    const [q, s] = await Promise.all([api.get(`/cycles/${cycleId}/questions`), api.get(`/cycles/${cycleId}/sections`)])
    questions.value = q.data
    sections.value = s.data
  } catch {
    error.value = 'Failed to load the application questions.'
  }
}

onMounted(load)
</script>

<template>
  <div class="max-w-3xl">
    <v-alert v-if="error" type="error" :text="error" />
    <div v-else-if="!questions" class="flex items-center gap-3 text-slate-400 py-4">
      <v-progress-circular indeterminate color="primary" size="22" width="2" />
      Loading…
    </div>
    <ScholarshipsCard v-else :cycleId="cycleId" :questions="questions" :sections="sections" @changed="load" />
  </div>
</template>
