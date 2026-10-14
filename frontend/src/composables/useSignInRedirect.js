import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { takePendingReferenceCode } from '../utils/pendingReference'

const roleHome = {
  applicant:    '/dashboard',
  scorer:       '/scoring',
  app_admin:    '/admin/applications',
  school_admin: '/schools',
  counselor:    '/counselor',
  referee:      '/references',
}

// After a password sign-in: go home, offering two-factor setup first to anyone not enrolled
export function useSignInRedirect() {
  const router = useRouter()
  const authStore = useAuthStore()

  return () => {
    // A reference link opened before signing in takes priority over the usual home page
    const code = takePendingReferenceCode()
    const home = code && authStore.hasPermission('submit_references')
      ? `/r/${code}`
      : roleHome[authStore.role] ?? '/dashboard'
    if (authStore.user?.twoFactorEnabled) router.replace(home)
    else router.replace({ path: '/two-factor', query: { next: home } })
  }
}
