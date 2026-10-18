import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '../services/api'
import { useAuthStore } from './auth'
import { tourFor } from '../tours'
import router from '../router'

// The signed-in user's page tours: which they've been through (UserTours), the current page's tour, and replay
// requests from the header. PageTour does the showing.
export const useToursStore = defineStore('tours', () => {
  const authStore = useAuthStore()

  // tourId -> { version, outcome, lastSeenAt, completedAt, timesSeen }; null until loaded for `loadedFor`
  const records = ref(null)
  let loadedFor = null
  // Bumped to ask PageTour to (re)start the current tour
  const replayRequest = ref(0)

  const currentTour = computed(() => {
    const name = router.currentRoute.value.name
    return authStore.isAuthenticated && name ? tourFor(name, authStore.role) : null
  })

  async function load() {
    const key = `${authStore.user?.id}:${authStore.role}`
    if (records.value && loadedFor === key) return records.value
    const { data } = await api.get('/tours')
    records.value = Object.fromEntries(data.map((r) => [r.tourId, r]))
    loadedFor = key
    return records.value
  }

  // Whether a tour should start on its own: never seen, or changed since
  function isDue(tour) {
    const record = records.value?.[tour.id]
    return !record || record.version !== tour.version
  }

  async function record(tour, outcome) {
    // While impersonating, the tours are the real user's, so nothing is recorded
    if (authStore.isImpersonating) return
    const now = new Date().toISOString()
    const previous = records.value?.[tour.id]
    if (records.value) {
      records.value[tour.id] = {
        tourId: tour.id,
        version: tour.version,
        outcome,
        lastSeenAt: now,
        completedAt: outcome === 'completed' ? now : previous?.completedAt ?? null,
        timesSeen: (previous?.timesSeen ?? 0) + 1,
      }
    }
    try {
      await api.put(`/tours/${encodeURIComponent(tour.id)}`, { version: tour.version, outcome })
    } catch {
      // Not worth interrupting anyone over; it'll just show again next time
    }
  }

  function replay() {
    replayRequest.value++
  }

  function reset() {
    records.value = null
    loadedFor = null
  }

  return { records, currentTour, replayRequest, load, isDue, record, replay, reset }
})
