<script setup>
import { ref, computed, provide, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '../../services/api'

// One application period: its details, application questions, references, scholarships and award letters, one tab
// each (child routes). Tabs read the period through inject('cycle').
const route = useRoute()
const cycleId = route.params.cycleId

const cycle = ref(null)
const error = ref(null)

async function load() {
  try {
    const res = await api.get('/admin/cycles')
    cycle.value = res.data.find((c) => c.id === cycleId) ?? null
    if (!cycle.value) error.value = 'Application period not found.'
  } catch {
    error.value = 'Failed to load the application period.'
  }
}

provide('cycle', { cycle, reload: load })
onMounted(load)

const base = `/admin/cycles/${cycleId}`
const tabs = [
  { title: 'Details', to: base, icon: 'mdi-calendar-edit-outline', exact: true },
  { title: 'Application Questions', to: `${base}/questions`, icon: 'mdi-format-list-bulleted' },
  { title: 'References', to: `${base}/references`, icon: 'mdi-account-check-outline' },
  { title: 'Scholarships', to: `${base}/scholarships`, icon: 'mdi-trophy-outline' },
  { title: 'Award Letters', to: `${base}/letters`, icon: 'mdi-email-newsletter' },
]

const dates = computed(() => cycle.value
  ? `${new Date(cycle.value.openDate).toLocaleDateString()} – ${new Date(cycle.value.closeDate).toLocaleDateString()}`
  : '')
</script>

<template>
  <div class="space-y-6">
    <div>
      <v-btn variant="text" color="primary" size="small" prepend-icon="mdi-arrow-left" to="/admin/scholarship-settings" class="-ml-3 mb-1">
        Application Periods
      </v-btn>
      <div class="flex flex-wrap items-center gap-2">
        <h1 class="page-title">{{ cycle?.name ?? 'Application Period' }}</h1>
        <v-chip v-if="cycle?.isActive" color="success" class="font-semibold">Accepting applications</v-chip>
      </div>
      <p class="page-subtitle">{{ dates }}</p>
    </div>

    <v-alert v-if="error" type="error" :text="error" />

    <template v-else>
      <v-tabs color="primary" show-arrows class="border-b border-[#E9EDF7]" data-tour="period-tabs">
        <v-tab v-for="t in tabs" :key="t.to" :to="t.to" :exact="t.exact" :prepend-icon="t.icon">{{ t.title }}</v-tab>
      </v-tabs>

      <RouterView />
    </template>
  </div>
</template>
