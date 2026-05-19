import { defineStore } from 'pinia'
import api from '../services/api'
import { uploadFile as uploadFileUtil } from '../utils/fileUpload'

export const useApplicationStore = defineStore('application', {
  state: () => ({
    questions: [],
    answers: {},
    currentApplication: null,
    status: null,
  }),

  actions: {
    async fetchQuestions(appId) {
      const response = await api.get(`/applications/${appId}/questions`)
      this.questions = response.data
    },

    saveAnswer(questionId, value) {
      this.answers[questionId] = value
    },

    async saveDraft(appId) {
      const response = await api.put(`/applications/${appId}/draft`, {
        answers: this.answers,
      })
      this.currentApplication = response.data
      this.status = response.data.status
      return response.data
    },

    async submitApplication(appId) {
      const response = await api.post(`/applications/${appId}/submit`, {
        answers: this.answers,
      })
      this.currentApplication = response.data
      this.status = response.data.status
      return response.data
    },

    async uploadFile(questionId, file) {
      const fileId = await uploadFileUtil(`/applications/upload`, file)
      this.answers[questionId] = fileId
      return fileId
    },
  },
})
