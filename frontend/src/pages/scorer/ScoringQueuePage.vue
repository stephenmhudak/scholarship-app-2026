<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useScoringStore } from '../../stores/scoring'
import StatusBadge from '../../components/admin/StatusBadge.vue'

const router = useRouter()
const scoringStore = useScoringStore()

const loading = ref(true)
const error = ref(null)

const headers = [
  { title: 'Applicant', key: 'applicant', value: (a) => `${a.lastName} ${a.firstName}` },
  { title: 'Status', key: 'status' },
  { title: 'Submitted', key: 'submittedAt' },
  { title: 'Score Status', key: 'hasScored' },
  { title: 'Action', key: 'actions', sortable: false, align: 'end' },
]

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
      <p class="page-subtitle">Applications with sections pending your score.</p>
    </div>

    <v-alert v-if="error" type="error" :text="error" />

    <v-card v-else-if="!loading && scoringStore.queue.length === 0">
      <v-empty-state icon="mdi-inbox-outline" text="No applications in your queue." />
    </v-card>

    <v-card v-else class="overflow-hidden">
      <v-data-table
        :headers="headers"
        :items="scoringStore.queue"
        :sort-by="[{ key: 'submittedAt', order: 'desc' }]"
        :loading="loading"
        loading-text="Loading queue…"
        class="clickable-rows"
        @click:row="(e, { item }) => goToDetail(item.id)"
      >
        <template #item.applicant="{ item }">
          <span class="font-semibold text-navy">{{ item.firstName }} {{ item.lastName }}</span>
        </template>
        <template #item.status="{ item }">
          <StatusBadge :status="item.status" />
        </template>
        <template #item.submittedAt="{ item }">
          <span class="text-slate-400">{{ item.submittedAt ? new Date(item.submittedAt).toLocaleDateString() : '—' }}</span>
        </template>
        <template #item.hasScored="{ item }">
          <v-chip color="warning" :prepend-icon="item.hasScored ? 'mdi-pencil-outline' : 'mdi-clock-outline'" class="font-semibold">
            {{ item.hasScored ? 'In Progress' : 'Pending' }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn variant="text" color="primary" size="small" append-icon="mdi-arrow-right" @click.stop="goToDetail(item.id)">
            {{ item.hasScored ? 'Continue' : 'Score Now' }}
          </v-btn>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>
