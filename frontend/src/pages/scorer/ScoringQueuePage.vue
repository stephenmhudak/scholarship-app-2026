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
      <h1 class="text-2xl font-bold text-gray-900">Scoring Queue</h1>
      <p class="text-gray-500 mt-1">Applications assigned to you for review.</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading queue...
    </div>

    <div v-else-if="error" class="text-red-600">{{ error }}</div>

    <div v-else-if="scoringStore.queue.length === 0" class="bg-white rounded-xl border border-gray-200 p-8 text-center shadow-sm">
      <span class="mdi mdi-inbox-full text-4xl text-gray-300 block mb-2"></span>
      <p class="text-gray-500">No applications in your queue.</p>
    </div>

    <div v-else class="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
      <table class="min-w-full divide-y divide-gray-200">
        <thead class="bg-gray-50">
          <tr>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Applicant</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Submitted</th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Action</th>
          </tr>
        </thead>
        <tbody class="bg-white divide-y divide-gray-100">
          <tr
            v-for="app in scoringStore.queue"
            :key="app.id"
            class="hover:bg-gray-50 cursor-pointer transition-colors"
            @click="goToDetail(app.id)"
          >
            <td class="px-6 py-4">
              <div>
                <p class="text-sm font-medium text-gray-900">{{ app.applicant_name }}</p>
                <p class="text-xs text-gray-500">{{ app.applicant_email }}</p>
              </div>
            </td>
            <td class="px-6 py-4">
              <StatusBadge :status="app.status" />
            </td>
            <td class="px-6 py-4 text-sm text-gray-500">
              {{ app.submitted_at ? new Date(app.submitted_at).toLocaleDateString() : '—' }}
            </td>
            <td class="px-6 py-4 text-right">
              <button
                @click.stop="goToDetail(app.id)"
                class="text-sm text-blue-600 hover:text-blue-700 font-medium"
              >
                Review
                <span class="mdi mdi-arrow-right ml-0.5"></span>
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
