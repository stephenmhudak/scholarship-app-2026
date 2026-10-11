<script setup>
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '../../services/api'

const route = useRoute()
const router = useRouter()

onMounted(async () => {
  try {
    // Returns the existing application if one was already started for this cycle.
    const response = await api.post('/applications', { cycleId: route.query.cycleId || null })
    router.replace(`/application/${response.data.id}`)
  } catch (err) {
    const msg = err.response?.data?.error || 'Could not start application.'
    router.replace({ path: '/dashboard', query: { error: msg } })
  }
})
</script>

<template>
  <div class="flex items-center gap-3 text-slate-400 p-8">
    <v-progress-circular indeterminate color="primary" size="22" width="2" />
    Starting your application…
  </div>
</template>
