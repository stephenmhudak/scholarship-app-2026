import { defineStore } from 'pinia'
import api from '../services/api'

export const useApplicationStore = defineStore('application', {
  state: () => ({
    questions: [],
    answers: {},
    currentApplication: null,
    cycleId: null,
  }),

  actions: {
    async loadApplicationForm(appId) {
      const appRes = await api.get(`/applications/${appId}`)
      this.currentApplication = appRes.data
      this.cycleId = appRes.data.cycleId

      const qRes = await api.get(`/cycles/${appRes.data.cycleId}/questions`)
      this.questions = qRes.data

      // Pre-fill answers from existing draft
      const answers = {}
      for (const answer of appRes.data.answers || []) {
        if (answer.selectedOptions && answer.selectedOptions.length) {
          answers[answer.questionId] = answer.selectedOptions
        } else {
          answers[answer.questionId] = answer.textValue ?? ''
        }
      }
      this.answers = answers
    },

    saveAnswer(questionId, value) {
      this.answers[questionId] = value
    },

    _buildAnswersPayload() {
      return Object.entries(this.answers).map(([questionId, value]) => ({
        questionId,
        textValue: Array.isArray(value) ? null : (value || null),
        selectedOptions: Array.isArray(value) ? value : null,
      }))
    },

    async saveDraft(appId) {
      const id = this.currentApplication?.id ?? appId
      await api.put(`/applications/${id}/draft`, {
        answers: this._buildAnswersPayload(),
      })
    },

    async submitApplication(appId) {
      const id = this.currentApplication?.id ?? appId
      await api.post(`/applications/${id}/submit`, {
        answers: this._buildAnswersPayload(),
      })
    },

    async uploadFile(questionId, file) {
      const formData = new FormData()
      formData.append('file', file)
      const response = await api.post('/files/upload', formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      const fileId = response.data.fileId
      this.answers[questionId] = fileId
      return fileId
    },
  },
})
