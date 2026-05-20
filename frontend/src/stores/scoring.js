import { defineStore } from 'pinia'
import api from '../services/api'

export const useScoringStore = defineStore('scoring', {
  state: () => ({
    queue: [],
    currentApplication: null,
    questions: [],
  }),

  getters: {
    questionMap: (state) => Object.fromEntries(state.questions.map((q) => [q.id, q.text])),
  },

  actions: {
    async fetchQueue() {
      const response = await api.get('/scoring/queue')
      this.queue = response.data
    },

    async fetchApplication(id) {
      const response = await api.get(`/scoring/${id}`)
      this.currentApplication = response.data

      if (response.data.cycleId) {
        const qRes = await api.get(`/cycles/${response.data.cycleId}/questions`)
        this.questions = qRes.data
      }
    },

    async submitScore(applicationId, payload) {
      await api.post(`/scoring/${applicationId}/score`, payload)
    },
  },
})
