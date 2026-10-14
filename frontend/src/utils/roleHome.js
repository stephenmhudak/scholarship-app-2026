// Where each role lands after signing in (or starting/ending an impersonation)
export function roleHome(role, schoolId = null) {
  switch (role) {
    case 'scorer': return '/scoring'
    case 'app_admin':
    case 'super_admin': return '/admin/applications'
    case 'school_admin': return schoolId ? `/schools/${schoolId}` : '/schools'
    case 'counselor': return '/counselor'
    case 'referee': return '/references'
    default: return '/dashboard'
  }
}
