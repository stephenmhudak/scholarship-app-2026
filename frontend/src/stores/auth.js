import { defineStore } from 'pinia'
import api from '../services/api'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: JSON.parse(localStorage.getItem('auth_user') || 'null'),
    token: localStorage.getItem('auth_token') || null,
    role: localStorage.getItem('auth_role') || null,
    permissions: JSON.parse(localStorage.getItem('auth_permissions') || '[]'),
  }),

  getters: {
    isAuthenticated: (state) => !!state.token,
    currentRole: (state) => state.role,
    fullName: (state) => state.user ? `${state.user.firstName} ${state.user.lastName}` : '',
    hasPermission: (state) => (perm) => state.permissions.includes(perm),
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
      this.permissions = []
      localStorage.removeItem('auth_token')
      localStorage.removeItem('auth_role')
      localStorage.removeItem('auth_user')
      localStorage.removeItem('auth_permissions')
    },

    async fetchCurrentUser() {
      const response = await api.get('/auth/me')
      const d = response.data
      this.role = d.role
      this.permissions = d.permissions ?? []
      this.user = {
        id: d.id,
        email: d.email,
        firstName: d.firstName,
        lastName: d.lastName,
        role: d.role,
        schoolId: d.schoolId ?? null,
      }
      localStorage.setItem('auth_role', d.role)
      localStorage.setItem('auth_user', JSON.stringify(this.user))
      localStorage.setItem('auth_permissions', JSON.stringify(this.permissions))
    },

    _applyAuthResponse(data) {
      this.token = data.token
      this.role = data.role
      this.permissions = data.permissions ?? []
      this.user = {
        id: data.userId,
        email: data.email,
        firstName: data.firstName,
        lastName: data.lastName,
        role: data.role,
        schoolId: data.schoolId ?? null,
      }
      localStorage.setItem('auth_token', data.token)
      localStorage.setItem('auth_role', data.role)
      localStorage.setItem('auth_user', JSON.stringify(this.user))
      localStorage.setItem('auth_permissions', JSON.stringify(this.permissions))
    },
  },
})
