import { defineStore } from 'pinia'
import api from '../services/api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: JSON.parse(localStorage.getItem('auth_user') || 'null'),
    token: localStorage.getItem('auth_token') || null,
    role: localStorage.getItem('auth_role') || null,
  }),

  getters: {
    isAuthenticated: (state) => !!state.token,
    currentRole: (state) => state.role,
    fullName: (state) => state.user ? `${state.user.firstName} ${state.user.lastName}` : '',
  },

  actions: {
    async login(credentials) {
      const response = await api.post('/auth/login', credentials)
      this._applyAuthResponse(response.data)
    },

    logout() {
      this.token = null
      this.user = null
      this.role = null
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_role')
      localStorage.removeItem('auth_user')
    },

    async fetchCurrentUser() {
      const response = await api.get('/auth/me')
      this.user = response.data
      this.role = response.data.role
      localStorage.setItem('auth_role', response.data.role)
      localStorage.setItem('auth_user', JSON.stringify(response.data))
    },

    _applyAuthResponse(data) {
      this.token = data.token
      this.role = data.role
      this.user = {
        id: data.userId,
        email: data.email,
        firstName: data.firstName,
        lastName: data.lastName,
        role: data.role,
      }
      localStorage.setItem('auth_token', data.token)
      localStorage.setItem('auth_role', data.role)
      localStorage.setItem('auth_user', JSON.stringify(this.user))
    },
  },
})
