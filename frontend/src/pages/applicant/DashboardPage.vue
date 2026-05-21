<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import StatusBadge from '../../components/admin/StatusBadge.vue'
import api from '../../services/api'

const authStore = useAuthStore()
const router = useRouter()

const application = ref(null)
const references = ref([])
const loading = ref(true)
const error = ref(null)

onMounted(async () => {
  try {
    const appsRes = await api.get('/applications/my')
    const apps = appsRes.data
    if (apps.length > 0) {
      application.value = apps.sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))[0]
      const refRes = await api.get(`/applications/${application.value.id}/references`)
      references.value = refRes.data
    }
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
      <h1 class="page-title">Dashboard</h1>
      <p class="page-subtitle">Welcome back, {{ authStore.fullName || 'Applicant' }}!</p>
    </div>

    <div v-if="loading" class="flex items-center gap-2 text-slate-400 py-4">
      <span class="mdi mdi-loading animate-spin text-xl"></span>
      Loading your application…
    </div>

    <div v-else-if="error" class="text-sm font-medium text-danger">{{ error }}</div>

    <template v-else>
      <!-- Application Status Card -->
      <div class="section-card">
        <h2 class="text-base font-bold text-navy mb-4">Application Status</h2>
        <div v-if="application">
          <div class="flex items-center gap-3">
            <StatusBadge :status="application.status" />
            <span class="text-sm text-slate-400">
              Created {{ new Date(application.createdAt).toLocaleDateString() }}
            </span>
          </div>
          <div class="mt-5 flex gap-3">
            <RouterLink
              :to="`/application/${application.id}`"
              class="inline-flex items-center gap-1.5 text-sm font-semibold bg-primary text-white px-4 py-2.5 rounded-xl hover:bg-primary-700 transition-colors shadow-sm"
            >
              <span class="mdi mdi-file-document-edit-outline"></span>
              {{ application.status === 'draft' ? 'Continue Application' : 'View Application' }}
            </RouterLink>
            <RouterLink
              :to="`/application/${application.id}/status`"
              class="inline-flex items-center gap-1.5 text-sm font-semibold bg-white text-navy border border-[#E9EDF7] px-4 py-2.5 rounded-xl hover:bg-[#F4F7FE] transition-colors"
            >
              <span class="mdi mdi-clipboard-check-outline"></span>
              View Status
            </RouterLink>
          </div>
        </div>
        <div v-else class="space-y-4">
          <p class="text-sm text-slate-400">You haven't started an application yet.</p>
          <RouterLink
            to="/application/new"
            class="inline-flex items-center gap-1.5 text-sm font-semibold bg-primary text-white px-4 py-2.5 rounded-xl hover:bg-primary-700 transition-colors shadow-sm"
          >
            <span class="mdi mdi-plus"></span>
            Start Application
          </RouterLink>
        </div>
      </div>

      <!-- References Card -->
      <div class="section-card">
        <h2 class="text-base font-bold text-navy mb-4">Reference Letters</h2>
        <div v-if="references.length === 0" class="text-sm text-slate-400">
          No reference requests yet. Reference requests will appear here after you start your application.
        </div>
        <ul v-else class="divide-y divide-[#E9EDF7]">
          <li
            v-for="ref in references"
            :key="ref.id"
            class="flex items-center justify-between py-3"
          >
            <div>
              <p class="text-sm font-semibold text-navy">{{ ref.label || 'Reference #' + ref.id.slice(0, 8) }}</p>
              <p class="text-xs text-slate-400">Expires {{ new Date(ref.expiresAt).toLocaleDateString() }}</p>
            </div>
            <span
              :class="[
                'text-xs px-2.5 py-1 rounded-full font-semibold border',
                ref.status === 'received'
                  ? 'bg-success-light text-success-dark border-success/20'
                  : 'bg-warning-light text-[#8C6500] border-warning/20',
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
