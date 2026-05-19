<script setup>
import { ref, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import api from '../../services/api'

const authStore = useAuthStore()

const application = ref(null)
const references = ref([])
const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    const [appRes, refRes] = await Promise.all([
      api.get('/applications/current'),
      api.get('/applications/current/references'),
    ])
    application.value = appRes.data
    references.value = refRes.data
  } catch (err) {
    if (err.response?.status !== 404) {
      error.value = 'Failed to load dashboard data.'
    }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold text-gray-900">Dashboard</h1>
      <p class="text-gray-500 mt-1">Welcome back, {{ authStore.user?.name || 'Applicant' }}!</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-gray-500">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading your application...
    </div>

    <div v-else-if="error" class="text-red-600 text-sm">{{ error }}</div>

    <template v-else>
      <!-- Application Status Card -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <h2 class="text-lg font-semibold text-gray-900 mb-4">Application Status</h2>
        <div v-if="application">
          <div class="flex items-center gap-3">
            <StatusBadge :status="application.status" />
            <span class="text-sm text-gray-600">
              Last updated {{ new Date(application.updated_at).toLocaleDateString() }}
            </span>
          </div>
          <div class="mt-4 flex gap-3">
            <RouterLink
              to="/application/current"
              class="inline-flex items-center gap-1.5 text-sm bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition-colors"
            >
              <span class="mdi mdi-file-document-edit"></span>
              {{ application.status === 'draft' ? 'Continue Application' : 'View Application' }}
            </RouterLink>
            <RouterLink
              to="/application/current/status"
              class="inline-flex items-center gap-1.5 text-sm bg-white text-gray-700 border border-gray-300 px-4 py-2 rounded-lg hover:bg-gray-50 transition-colors"
            >
              <span class="mdi mdi-clipboard-check"></span>
              View Status
            </RouterLink>
          </div>
        </div>
        <div v-else class="space-y-3">
          <p class="text-gray-500 text-sm">You haven't started an application yet.</p>
          <RouterLink
            to="/application/new"
            class="inline-flex items-center gap-1.5 text-sm bg-blue-600 text-white px-4 py-2 rounded-lg hover:bg-blue-700 transition-colors"
          >
            <span class="mdi mdi-plus"></span>
            Start Application
          </RouterLink>
        </div>
      </div>

      <!-- References Card -->
      <div class="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
        <h2 class="text-lg font-semibold text-gray-900 mb-4">Reference Letters</h2>
        <div v-if="references.length === 0" class="text-sm text-gray-500">
          No reference requests yet. Reference requests will appear here after you start your application.
        </div>
        <ul v-else class="divide-y divide-gray-100">
          <li
            v-for="ref in references"
            :key="ref.id"
            class="flex items-center justify-between py-3"
          >
            <div>
              <p class="text-sm font-medium text-gray-900">{{ ref.referee_name || 'Reference #' + ref.id }}</p>
              <p class="text-xs text-gray-500">{{ ref.referee_email }}</p>
            </div>
            <span
              :class="[
                'text-xs px-2.5 py-1 rounded-full font-medium',
                ref.status === 'received' ? 'bg-green-100 text-green-700' : 'bg-yellow-100 text-yellow-700',
              ]"
            >
              {{ ref.status === 'received' ? 'Received' : 'Pending' }}
            </span>
          </li>
        </ul>
      </div>
    </template>
  </div>
</template>
