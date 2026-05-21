<script setup>
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../../services/api'

const router = useRouter()

onMounted(async () => {
  try {
    const response = await api.post('/applications')
    router.replace(`/application/${response.data.id}`)
  } catch (err) {
    const msg = err.response?.data?.error || 'Could not start application.'
    router.replace({ path: '/dashboard', query: { error: msg } })
  }
})
</script>

<template>
  <div class="flex items-center gap-2 text-slate-400 p-8">
    <span class="mdi mdi-loading animate-spin text-xl"></span>
    Starting your application…
  </div>
</template>
