<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useScoringStore } from '../../stores/scoring'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const router = useRouter()
const scoringStore = useScoringStore()

const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    await scoringStore.fetchQueue()
  } catch {
    error.value = 'Failed to load scoring queue.'
  } finally {
    loading.value = false
  }
})

function goToDetail(applicationId) {
  router.push(`/scoring/${applicationId}`)
}
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Scoring Queue</h1>
      <p class="page-subtitle">Applications assigned to you for review.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading queue…
    </div>

    <div v-else-if="error" class="text-sm font-medium text-danger">{{ error }}</div>

    <div v-else-if="scoringStore.queue.length === 0" class="section-card text-center py-10">
      <span class="mdi mdi-inbox-outline text-4xl text-slate-400 block mb-2"></span>
      <p class="text-sm text-slate-400">No applications in your queue.</p>
    </div>

    <div v-else class="card overflow-hidden">
      <table class="dialect-table">
        <thead>
          <tr>
            <th>Applicant</th>
            <th>Status</th>
            <th>Submitted</th>
            <th>Score Status</th>
            <th class="text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="app in scoringStore.queue"
            :key="app.id"
            @click="goToDetail(app.id)"
          >
            <td>
              <p class="font-semibold text-navy">{{ app.firstName }} {{ app.lastName }}</p>
            </td>
            <td><StatusBadge :status="app.status" /></td>
            <td class="text-slate-400">
              {{ app.submittedAt ? new Date(app.submittedAt).toLocaleDateString() : '—' }}
            </td>
            <td>
              <span
                v-if="app.hasScored"
                class="inline-flex items-center gap-1 text-xs font-semibold text-success-dark bg-success-light border border-success/20 px-2 py-0.5 rounded-full"
              >
                <span class="mdi mdi-check-circle-outline text-sm"></span>
                Scored
              </span>
              <span
                v-else
                class="inline-flex items-center gap-1 text-xs font-semibold text-[#8C6500] bg-warning-light border border-warning/30 px-2 py-0.5 rounded-full"
              >
                <span class="mdi mdi-clock-outline text-sm"></span>
                Pending
              </span>
            </td>
            <td class="text-right">
              <button
                @click.stop="goToDetail(app.id)"
                class="text-sm font-semibold text-primary hover:text-primary-700 transition-colors"
              >
                {{ app.hasScored ? 'Edit Score' : 'Score Now' }}
                <span class="mdi mdi-arrow-right ml-0.5"></span>
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
