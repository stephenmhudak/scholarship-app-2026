import { defineStore } from 'pinia'
import api from '../services/api'

export const useScoringStore = defineStore('scoring', {
  state: () => ({
    queue: [],
    currentApplication: null,
    scores: [],
  }),

  actions: {
    async fetchQueue() {
      const response = await api.get('/scoring/queue')
      this.queue = response.data
    },

    async fetchApplication(id) {
      const response = await api.get(`/scoring/applications/${id}`)
      this.currentApplication = response.data.application
      this.scores = response.data.scores || []
    },

    async submitScore(applicationId, payload) {
      const response = await api.post(`/scoring/applications/${applicationId}/score`, payload)
      this.scores = [...this.scores, response.data]
      return response.data
    },
  },
})
