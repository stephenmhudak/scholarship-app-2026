import { createRouter, createWebHistory } from 'vue-router'
import { roleHome } from '../utils/roleHome'
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
    path: '/verify-email',
    name: 'VerifyEmail',
    component: () => import('../pages/auth/VerifyEmailPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/forgot-password',
    name: 'ForgotPassword',
    component: () => import('../pages/auth/ForgotPasswordPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/two-factor',
    name: 'TwoFactorPrompt',
    component: () => import('../pages/auth/TwoFactorPromptPage.vue'),
    meta: { layout: 'auth' },
  },
  {
    path: '/account',
    name: 'Account',
    component: () => import('../pages/account/AccountPage.vue'),
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
    // The question builder, for one scholarship's eligibility or own questions
    path: '/admin/cycles/:cycleId/scholarships/:scholarshipId/:part(eligibility|details)',
    name: 'ScholarshipQuestions',
    component: () => import('../pages/admin/CycleQuestionsPage.vue'),
    meta: { permission: 'admin_scholarship' },
  },
  {
    // Anyone assigned to award a scholarship (the permission is added at sign-in); super admins have it too
    path: '/awards',
    name: 'AwardScholarships',
    component: () => import('../pages/admin/AwardScholarshipsPage.vue'),
    meta: { permission: 'award_scholarships' },
  },
  {
    // The question builder, for one reference form's questions
    path: '/admin/cycles/:cycleId/references/:formId',
    name: 'ReferenceFormQuestions',
    component: () => import('../pages/admin/CycleQuestionsPage.vue'),
    meta: { permission: 'admin_scholarship' },
  },
  {
    // One application period, a tab per child route
    path: '/admin/cycles/:cycleId',
    component: () => import('../pages/admin/CyclePage.vue'),
    meta: { permission: 'admin_scholarship' },
    children: [
      { path: '', name: 'CycleDetails', component: () => import('../pages/admin/cycle/CycleDetailsTab.vue') },
      { path: 'questions', name: 'CycleQuestions', component: () => import('../pages/admin/CycleQuestionsPage.vue') },
      { path: 'references', name: 'CycleReferences', component: () => import('../pages/admin/cycle/CycleReferencesTab.vue') },
      { path: 'scholarships', name: 'CycleScholarships', component: () => import('../pages/admin/cycle/CycleScholarshipsTab.vue') },
      { path: 'letters', name: 'AwardLetters', component: () => import('../pages/admin/AwardLettersPage.vue') },
    ],
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
    // Reference links the applicant shares: /r/ + 10-character code. Needs a referee, counselor or school admin
    // account; anyone else is sent to /reference-start (see the guard below).
    path: '/r/:code',
    name: 'ReferenceForm',
    component: () => import('../pages/reference/ReferenceFormPage.vue'),
    meta: { permission: 'submit_references' },
  },
  {
    // Invitation emails for counselors: create the first password
    path: '/setup-password',
    name: 'SetupPassword',
    component: () => import('../pages/auth/SetupPasswordPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/reference-start',
    name: 'ReferenceStart',
    component: () => import('../pages/reference/ReferenceStartPage.vue'),
    meta: { layout: 'auth', public: true },
  },
  {
    path: '/references',
    name: 'MyReferences',
    component: () => import('../pages/reference/MyReferencesPage.vue'),
    meta: { permission: 'submit_references' },
  },
  {
    path: '/references/:id',
    name: 'MyReference',
    component: () => import('../pages/reference/MyReferenceDetailPage.vue'),
    meta: { permission: 'submit_references' },
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
      return storedRoleHome(role)
    },
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: () => {
      const role = localStorage.getItem('auth_role')
      return storedRoleHome(role)
    },
  },
]

function storedRoleHome(role) {
  const user = JSON.parse(localStorage.getItem('auth_user') || 'null')
  return roleHome(role, user?.schoolId)
}

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to, _from, next) => {
  const authStore = useAuthStore()

  if (to.meta.public) return next()

  // Reference links explain what kind of account is needed (and remember the code) instead of a bare login or 403
  if (to.name === 'ReferenceForm' && !(authStore.isAuthenticated && authStore.hasPermission('submit_references')))
    return next({ path: '/reference-start', query: { code: to.params.code } })

  if (!authStore.isAuthenticated) return next('/login')

  if (to.meta.permission && !authStore.hasPermission(to.meta.permission))
    return next('/403')

  if (to.name === 'SchoolsList' && authStore.hasPermission('manage_school') && authStore.user?.schoolId)
    return next(`/schools/${authStore.user.schoolId}`)

  next()
})

export default router
