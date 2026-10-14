import { defineStore } from 'pinia'
import api from '../services/api'
import { userFromAuth, writeAuth, saveReturnSession, clearReturnSession, restoreReturnSession } from '../utils/authStorage'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: JSON.parse(localStorage.getItem('auth_user') || 'null'),
    token: localStorage.getItem('auth_token') || null,
    role: localStorage.getItem('auth_role') || null,
    permissions: JSON.parse(localStorage.getItem('auth_permissions') || '[]'),
    // Set while a super admin is impersonating someone: the super admin's name. Everything else above is the
    // impersonated user's, so the app looks and behaves as it would for them.
    impersonatorName: localStorage.getItem('impersonator_name') || null,
  }),

  getters: {
    isAuthenticated: (state) => !!state.token,
    currentRole: (state) => state.role,
    fullName: (state) => state.user ? `${state.user.firstName} ${state.user.lastName}` : '',
    hasPermission: (state) => (perm) => state.permissions.includes(perm),
    isImpersonating: (state) => !!state.impersonatorName,
  },

  actions: {
    // Resolves to { challengeToken, method, maskedEmail } when a second factor is needed, otherwise null (signed in)
    async login(credentials) {
      const response = await api.post('/auth/login', credentials)
      return this._applySignInResponse(response.data)
    },

    async resendTwoFactorEmail(challengeToken) {
      await api.post('/auth/login/2fa/resend', { challengeToken })
    },

    async completeTwoFactorLogin(challengeToken, code) {
      const response = await api.post('/auth/login/2fa', { challengeToken, code })
      this._applyAuthResponse(response.data)
    },

    async verifyEmail(token) {
      const response = await api.post('/auth/verify-email', { token })
      return this._applySignInResponse(response.data)
    },

    async resendVerification(email) {
      await api.post('/auth/resend-verification', { email })
    },

    async forgotPassword(email) {
      await api.post('/auth/forgot-password', { email })
    },

    // From an invitation link: creates the first password and signs in (or returns a 2FA challenge)
    async setupPassword({ token, newPassword }) {
      const response = await api.post('/auth/setup-password', { token, newPassword })
      return this._applySignInResponse(response.data)
    },

    async resetPassword({ email, code, newPassword }) {
      const response = await api.post('/auth/reset-password', { email, code, newPassword })
      return this._applySignInResponse(response.data)
    },

    async changePassword({ currentPassword, newPassword }) {
      await api.post('/auth/change-password', { currentPassword, newPassword })
    },

    async fetchTwoFactorStatus() {
      const response = await api.get('/auth/2fa')
      return response.data
    },

    async beginTwoFactorSetup() {
      const response = await api.post('/auth/2fa/setup')
      return response.data
    },

    async enableTwoFactor(code) {
      const response = await api.post('/auth/2fa/enable', { code })
      this._setTwoFactorEnabled(true)
      return response.data.recoveryCodes
    },

    async beginEmailTwoFactorSetup() {
      await api.post('/auth/2fa/email/setup')
    },

    async enableEmailTwoFactor(code) {
      await api.post('/auth/2fa/email/enable', { code })
      this._setTwoFactorEnabled(true)
    },

    async regenerateRecoveryCodes(password) {
      const response = await api.post('/auth/2fa/recovery-codes', { password })
      return response.data.recoveryCodes
    },

    async disableTwoFactor(password) {
      await api.post('/auth/2fa/disable', { password })
      this._setTwoFactorEnabled(false)
    },

    // Super admins: become this user until endImpersonation. The caller reloads the app so no page or store keeps
    // the super admin's data.
    async startImpersonation(userId) {
      const response = await api.post(`/admin/users/${userId}/impersonate`)
      saveReturnSession(response.data.returnSession)
      this._applyAuthResponse(response.data)
    },

    // Back to the super admin. Asks the API first (fresh session, logged); if that fails, falls back to the session
    // kept when the impersonation started, so they return to their account rather than being signed out.
    // Resolves to false only when neither works.
    async endImpersonation() {
      try {
        const response = await api.post('/auth/end-impersonation')
        clearReturnSession()
        this._applyAuthResponse(response.data)
        return true
      } catch {
        return restoreReturnSession()
      }
    },

    logout() {
      this.token = null
      this.user = null
      this.role = null
      this.permissions = []
      this.impersonatorName = null
      localStorage.removeItem('impersonator_name')
      clearReturnSession()
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
        twoFactorEnabled: this.user?.twoFactorEnabled ?? false,
      }
      localStorage.setItem('auth_role', d.role)
      localStorage.setItem('auth_user', JSON.stringify(this.user))
      localStorage.setItem('auth_permissions', JSON.stringify(this.permissions))
    },

    _applySignInResponse(data) {
      if (data.twoFactorRequired) return { challengeToken: data.challengeToken, method: data.method, maskedEmail: data.maskedEmail }
      this._applyAuthResponse(data)
      return null
    },

    _setTwoFactorEnabled(enabled) {
      if (!this.user) return
      this.user = { ...this.user, twoFactorEnabled: enabled }
      localStorage.setItem('auth_user', JSON.stringify(this.user))
    },

    _applyAuthResponse(data) {
      this.token = data.token
      this.role = data.role
      this.permissions = data.permissions ?? []
      this.user = userFromAuth(data)
      this.impersonatorName = data.impersonatorName ?? null
      writeAuth(data)
    },
  },
})
