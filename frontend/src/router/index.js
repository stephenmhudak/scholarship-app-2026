import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const Forbidden = {
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-50">
      <div class="text-center">
        <h1 class="text-6xl font-bold text-gray-300">403</h1>
        <p class="mt-4 text-xl text-gray-600">You are not authorized to view this page.</p>
        <a href="/dashboard" class="mt-6 inline-block px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700">
          Go to Dashboard
        </a>
      </div>
    </div>
  `,
}

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: () => import('../pages/auth/LoginPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/register',
    name: 'Register',
    component: () => import('../pages/auth/RegisterPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: () => import('../pages/applicant/DashboardPage.vue'),
    meta: { permission: 'view_dashboard' },
  },
  {
    path: '/application/new',
    name: 'NewApplication',
    component: () => import('../pages/applicant/NewApplicationPage.vue'),
    meta: { permission: 'manage_application' },
  },
  {
    path: '/application/:id',
    name: 'ApplicationForm',
    component: () => import('../pages/applicant/ApplicationFormPage.vue'),
    meta: { permission: 'manage_application' },
  },
  {
    path: '/application/:id/status',
    name: 'ApplicationStatus',
    component: () => import('../pages/applicant/ApplicationStatusPage.vue'),
    meta: { permission: 'manage_application' },
  },
  {
    path: '/scoring',
    name: 'ScoringQueue',
    component: () => import('../pages/scorer/ScoringQueuePage.vue'),
    meta: { permission: 'view_scoring_queue' },
  },
  {
    path: '/scoring/:applicationId',
    name: 'ScoringDetail',
    component: () => import('../pages/scorer/ScoringDetailPage.vue'),
    meta: { permission: 'score_application' },
  },
  {
    path: '/admin/applications',
    name: 'ApplicationsList',
    component: () => import('../pages/admin/ApplicationsListPage.vue'),
    meta: { permission: 'admin_applications' },
  },
  {
    path: '/admin/applications/:id',
    name: 'ApplicationDetail',
    component: () => import('../pages/admin/ApplicationDetailPage.vue'),
    meta: { permission: 'admin_applications' },
  },
  {
    path: '/admin/scholarship-settings',
    name: 'ScholarshipSettings',
    component: () => import('../pages/admin/SettingsPage.vue'),
    meta: { permission: 'admin_scholarship' },
  },
  {
    path: '/admin/settings',
    name: 'AdminSettings',
    component: () => import('../pages/admin/AdminSettingsPage.vue'),
    meta: { permission: 'admin_settings' },
  },
  {
    path: '/admin/scoring',
    name: 'ScoringOverview',
    component: () => import('../pages/admin/ScoringOverviewPage.vue'),
    meta: { permission: 'view_scoring_overview' },
  },
  {
    path: '/admin/cycles/:cycleId/questions',
    name: 'CycleQuestions',
    component: () => import('../pages/admin/CycleQuestionsPage.vue'),
    meta: { permission: 'admin_scholarship' },
  },
  {
    path: '/schools',
    name: 'SchoolsList',
    component: () => import('../pages/school/SchoolsListPage.vue'),
    meta: { permission: 'manage_school' },
  },
  {
    path: '/schools/:id',
    name: 'SchoolDetail',
    component: () => import('../pages/school/SchoolDetailPage.vue'),
    meta: { permission: 'manage_school' },
  },
  {
    path: '/counselor',
    name: 'CounselorDashboard',
    component: () => import('../pages/counselor/CounselorDashboardPage.vue'),
    meta: { permission: 'view_counselor_dashboard' },
  },
  {
    path: '/reference',
    name: 'ReferenceUpload',
    component: () => import('../pages/reference/ReferenceUploadPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/403',
    name: 'Forbidden',
    component: Forbidden,
  },
  {
    path: '/',
    redirect: () => {
      const role = localStorage.getItem('auth_role')
      return roleHome(role)
    },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: () => {
      const role = localStorage.getItem('auth_role')
      return roleHome(role)
    },
  },
]

function roleHome(role) {
  switch (role) {
    case 'scorer': return '/scoring'
    case 'app_admin': return '/admin/applications'
    case 'school_admin': {
      const user = JSON.parse(localStorage.getItem('auth_user') || 'null')
      return user?.schoolId ? `/schools/${user.schoolId}` : '/schools'
    }
    case 'counselor': return '/counselor'
    default: return '/dashboard'
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore()

  if (to.meta.public) return next()

  if (!authStore.isAuthenticated) return next('/login')

  if (to.meta.permission && !authStore.hasPermission(to.meta.permission))
    return next('/403')

  if (to.name === 'SchoolsList' && authStore.hasPermission('manage_school') && authStore.user?.schoolId)
    return next(`/schools/${authStore.user.schoolId}`)

  next()
})

export default router
