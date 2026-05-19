import { defineStore } from 'pinia'
import api from '../services/api'

export const useAdminStore = defineStore('admin', {
  state: () => ({
    applications: [],
    filters: {
      status: '',
      search: '',
    },
    pagination: {
      page: 1,
      perPage: 25,
      total: 0,
    },
  }),

  actions: {
    async fetchApplications(params = {}) {
      const response = await api.get('/admin/applications', { params })
      this.applications = response.data.data
      this.pagination = {
        page: response.data.current_page,
        perPage: response.data.per_page,
        total: response.data.total,
      }
      return response.data
    },

    async updateStatus(id, status) {
      const response = await api.patch(`/admin/applications/${id}/status`, { status })
      const index = this.applications.findIndex((a) => a.id === id)
      if (index !== -1) {
        this.applications[index] = { ...this.applications[index], status }
      }
      return response.data
    },

    async assignScorer(id, scorerId) {
      const response = await api.patch(`/admin/applications/${id}/assign`, { scorer_id: scorerId })
      const index = this.applications.findIndex((a) => a.id === id)
      if (index !== -1) {
        this.applications[index] = { ...this.applications[index], scorer_id: scorerId }
      }
      return response.data
    },

    async exportData(params = {}) {
      const response = await api.get('/admin/applications/export', {
        params,
        responseType: 'blob',
      })
      const url = window.URL.createObjectURL(new Blob([response.data]))
      const link = document.createElement('a')
      link.href = url
      link.setAttribute('download', 'applications-export.csv')
      document.body.appendChild(link)
      link.click()
      link.remove()
      window.URL.revokeObjectURL(url)
    },
  },
})
