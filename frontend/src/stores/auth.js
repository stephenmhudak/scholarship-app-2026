import { defineStore } from 'pinia'
import api from '../services/api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: null,
    token: localStorage.getItem('auth_token') || null,
    role: localStorage.getItem('auth_role') || null,
  }),

  getters: {
    isAuthenticated: (state) => !!state.token,
    currentRole: (state) => state.role,
  },

  actions: {
    async login(credentials) {
      const response = await api.post('/auth/login', credentials)
      const { token, user } = response.data

      this.token = token
      this.user = user
      this.role = user.role

      localStorage.setItem('auth_token', token)
      localStorage.setItem('auth_role', user.role)
    },

    logout() {
      this.token = null
      this.user = null
      this.role = null

      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_role')
    },

    async fetchCurrentUser() {
      const response = await api.get('/auth/me')
      this.user = response.data
      this.role = response.data.role
      localStorage.setItem('auth_role', response.data.role)
    },
  },
})
