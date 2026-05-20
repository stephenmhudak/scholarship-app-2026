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
    meta: { role: 'applicant' },
  },
  {
    path: '/application/new',
    name: 'NewApplication',
    component: () => import('../pages/applicant/NewApplicationPage.vue'),
    meta: { role: 'applicant' },
  },
  {
    path: '/application/:id',
    name: 'ApplicationForm',
    component: () => import('../pages/applicant/ApplicationFormPage.vue'),
    meta: { role: 'applicant' },
  },
  {
    path: '/application/:id/status',
    name: 'ApplicationStatus',
    component: () => import('../pages/applicant/ApplicationStatusPage.vue'),
    meta: { role: 'applicant' },
  },
  {
    path: '/scoring',
    name: 'ScoringQueue',
    component: () => import('../pages/scorer/ScoringQueuePage.vue'),
    meta: { role: 'scorer' },
  },
  {
    path: '/scoring/:applicationId',
    name: 'ScoringDetail',
    component: () => import('../pages/scorer/ScoringDetailPage.vue'),
    meta: { role: 'scorer' },
  },
  {
    path: '/admin/applications',
    name: 'ApplicationsList',
    component: () => import('../pages/admin/ApplicationsListPage.vue'),
    meta: { role: 'app_admin' },
  },
  {
    path: '/admin/applications/:id',
    name: 'ApplicationDetail',
    component: () => import('../pages/admin/ApplicationDetailPage.vue'),
    meta: { role: 'app_admin' },
  },
  {
    path: '/admin/settings',
    name: 'Settings',
    component: () => import('../pages/admin/SettingsPage.vue'),
    meta: { role: 'app_admin' },
  },
  {
    path: '/schools',
    name: 'SchoolsList',
    component: () => import('../pages/school/SchoolsListPage.vue'),
    meta: { role: 'school_admin' },
  },
  {
    path: '/schools/:id',
    name: 'SchoolDetail',
    component: () => import('../pages/school/SchoolDetailPage.vue'),
    meta: { role: 'school_admin' },
  },
  {
    path: '/counselor',
    name: 'CounselorDashboard',
    component: () => import('../pages/counselor/CounselorDashboardPage.vue'),
    meta: { role: 'counselor' },
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
    case 'school_admin': return '/schools'
    case 'counselor': return '/counselor'
    default: return '/dashboard'
  }
}

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()

  if (to.meta.public) return next()

  if (!authStore.isAuthenticated) return next('/login')

  if (to.meta.role && authStore.role !== to.meta.role) return next('/403')

  next()
})

export default router
