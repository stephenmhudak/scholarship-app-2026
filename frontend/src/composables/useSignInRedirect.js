import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const roleHome = {
  applicant:    '/dashboard',
  scorer:       '/scoring',
  app_admin:    '/admin/applications',
  school_admin: '/schools',
  counselor:    '/counselor',
}

// After a password sign-in: go home, offering two-factor setup first to anyone not enrolled
export function useSignInRedirect() {
  const router = useRouter()
  const authStore = useAuthStore()

  return () => {
    const home = roleHome[authStore.role] ?? '/dashboard'
    if (authStore.user?.twoFactorEnabled) router.replace(home)
    else router.replace({ path: '/two-factor', query: { next: home } })
  }
}
