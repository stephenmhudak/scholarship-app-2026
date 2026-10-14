// How a signed-in session is kept in localStorage (read back by stores/auth.js on load), and the super admin's own
// session kept aside while they impersonate someone.

const RETURN_KEY = 'impersonator_session'

/** The user object stored for a sign-in response (POST /auth/login etc.). */
export function userFromAuth(data) {
  return {
    id: data.userId,
    email: data.email,
    firstName: data.firstName,
    lastName: data.lastName,
    role: data.role,
    schoolId: data.schoolId ?? null,
    twoFactorEnabled: !!data.twoFactorEnabled,
  }
}

/** Saves a sign-in response as the current session. */
export function writeAuth(data) {
  localStorage.setItem('auth_token', data.token)
  localStorage.setItem('auth_role', data.role)
  localStorage.setItem('auth_user', JSON.stringify(userFromAuth(data)))
  localStorage.setItem('auth_permissions', JSON.stringify(data.permissions ?? []))
  if (data.impersonatorName) localStorage.setItem('impersonator_name', data.impersonatorName)
  else localStorage.removeItem('impersonator_name')
}

/** Keeps the super admin's own session while they impersonate (from the impersonate response's returnSession). */
export function saveReturnSession(session) {
  if (session) localStorage.setItem(RETURN_KEY, JSON.stringify(session))
}

export function clearReturnSession() {
  localStorage.removeItem(RETURN_KEY)
}

/**
 * Switches back to the super admin's kept session, if there is one. Returns true when it did. Used when ending an
 * impersonation fails, or the impersonated session is rejected (expired or reset), so they're returned to their
 * own account instead of being signed out.
 */
export function restoreReturnSession() {
  let session = null
  try { session = JSON.parse(localStorage.getItem(RETURN_KEY) || 'null') } catch { /* unreadable: treat as none */ }
  clearReturnSession()
  if (!session?.token) return false
  writeAuth(session)
  return true
}
