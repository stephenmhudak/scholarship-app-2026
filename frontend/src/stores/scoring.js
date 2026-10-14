import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../services/api'

export const useScoringStore = defineStore('scoring', () => {
  const queue = ref([])
  const currentApplication = ref(null)
  const questions = ref([])
  const sections = ref([])
  const existingScores = ref([])
  // Submitted references, each scored on its own
  const references = ref([])

  const questionMap = ref({})

  async function fetchQueue() {
    const res = await api.get('/scoring/queue')
    queue.value = res.data
  }

  async function fetchApplication(id) {
    const res = await api.get(`/scoring/${id}`)
    currentApplication.value = res.data

    if (res.data.cycleId) {
      const [qRes, sRes] = await Promise.all([
        api.get(`/cycles/${res.data.cycleId}/questions`),
        api.get(`/cycles/${res.data.cycleId}/sections`),
      ])
      questions.value = qRes.data
      sections.value = sRes.data
      questionMap.value = Object.fromEntries(qRes.data.map((q) => [q.id, q]))
    }

    const [scoresRes, referencesRes] = await Promise.all([
      api.get(`/scoring/${id}/my-scores`),
      api.get(`/scoring/${id}/references`),
    ])
    existingScores.value = scoresRes.data
    references.value = referencesRes.data
  }

  async function submitScore(applicationId, payload) {
    await api.post(`/scoring/${applicationId}/score`, payload)
    // Refresh existing scores after submit
    const scoresRes = await api.get(`/scoring/${applicationId}/my-scores`)
    existingScores.value = scoresRes.data
    // Mark as scored in queue
    const queueItem = queue.value.find((q) => q.id === applicationId)
    if (queueItem) queueItem.hasScored = true
  }

  return { queue, currentApplication, questions, sections, existingScores, references, questionMap, fetchQueue, fetchApplication, submitScore }
})
