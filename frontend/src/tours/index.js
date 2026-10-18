// Guided page tours (vue-onboarding-tour), one per role per page. PageTour (components/common) starts the current
// page's tour on its own the first time someone with that role opens it, and again whenever the tour's version
// changes; the header's help button replays it. UserTours (GET/PUT /api/tours) records each run.
//
// A tour:
//   page   — the route name it belongs to
//   label  — what it's called on screen (an admin's view of someone's tour history)
//   roles  — roles that get it; its id is "<role>:<page>"
//   files  — the page's .vue files (paths from src/). Their source and every component they import go into the
//            tour's version (build/componentHashes.js), so changing anything on the page shows the tour again.
//   steps  — [{ target, title, description }] or (role) => [...]. `target` names a data-tour="…" attribute (or a list
//            of them: the first one visible is used); leave it out for a step in the middle of the screen. Steps whose
//            target isn't on screen when the tour starts (empty lists, hidden on phones) are skipped.
import componentHashes from 'virtual:component-hashes'

const ADMINS = ['app_admin', 'super_admin']
const REFERENCE_WRITERS = ['referee', 'counselor', 'school_admin']
// Anyone can be assigned to award a scholarship, except applicants and reference providers
const AWARDERS = ['scorer', 'app_admin', 'super_admin', 'school_admin', 'counselor']
const EVERYONE = ['applicant', 'scorer', 'app_admin', 'super_admin', 'school_admin', 'counselor', 'referee']

const welcome = (description) => ({ title: 'Welcome to the Scholarship Portal', description })
const menu = (description) => ({ target: ['nav', 'nav-toggle'], title: 'Your menu', description })
const account = {
  target: 'account-link',
  title: 'Your account',
  description: 'Update your name or password, and turn on two-factor sign-in.',
}
const replay = {
  target: 'tour-button',
  title: 'Need a refresher?',
  description: 'Every page has a tour like this one. Use this button to take it again at any time.',
}

const builderSteps = [
  {
    target: 'question',
    title: 'Questions',
    description: "Use a question's pencil to edit it: its type, whether it's required, its validation, and when it's shown. The arrows move it up or down.",
  },
  {
    target: 'select-questions',
    title: 'Work on several at once',
    description: 'Tick questions to move them to another section or delete them together.',
  },
  {
    target: 'add-question',
    title: 'Add a question',
    description: 'New questions go at the end of their section.',
  },
]

export const tours = [
  // ── Everyone ──────────────────────────────────────────────────────────────
  {
    page: 'Account',
    label: 'Account',
    roles: EVERYONE,
    files: ['pages/account/AccountPage.vue'],
    steps: [
      {
        target: 'profile',
        title: 'Profile',
        description: "Fix your name here. Your email is how you sign in, so the scholarship office changes it for you.",
      },
      {
        target: 'password',
        title: 'Password',
        description: "Change your password. You'll stay signed in here and be signed out everywhere else.",
      },
      {
        target: 'two-factor',
        title: 'Two-factor sign-in',
        description: 'Add a second step when you sign in: a code from an authenticator app, or one sent to your email.',
      },
    ],
  },

  // ── Applicants ────────────────────────────────────────────────────────────
  {
    page: 'Dashboard',
    label: 'Dashboard',
    roles: ['applicant'],
    files: ['pages/applicant/DashboardPage.vue', 'components/common/AppSidebar.vue', 'components/common/AppHeader.vue'],
    steps: [
      welcome("Here's a quick look around. You can close this tour at any time."),
      {
        target: 'open-applications',
        title: 'Open applications',
        description: "The application periods accepting applications right now, with their deadlines. Start an application here, or continue one you've begun.",
      },
      {
        target: 'past-applications',
        title: 'Past applications',
        description: 'Applications from earlier periods. Open one to see its status and answers.',
      },
      {
        target: 'references',
        title: 'References',
        description: 'If your application needs references, this shows which ones have arrived. You request them from inside your application.',
      },
      menu('Everything you can do is listed here.'),
      account,
      replay,
    ],
  },
  {
    page: 'ApplicationForm',
    label: 'Application form',
    roles: ['applicant'],
    files: ['pages/applicant/ApplicationFormPage.vue'],
    steps: [
      {
        title: 'Filling in your application',
        description: 'Answer the questions on this page. Your answers save automatically as you go, so you can leave and come back.',
      },
      {
        target: 'sections',
        title: 'Sections',
        description: 'Jump to any section. A check mark appears once its required questions are answered.',
      },
      {
        target: 'reuse-answers',
        title: 'Reuse previous answers',
        description: 'Applied before? Copy matching answers from an earlier application instead of typing them again.',
      },
      {
        target: 'question',
        title: 'Questions',
        description: 'Questions marked * are required. Some questions only appear after you answer the ones before them.',
      },
      {
        target: 'references',
        title: 'References',
        description: "Create a link for each reference and send it to the person writing it. We'll email you when each one arrives.",
      },
      {
        target: 'action-bar',
        title: 'Progress and saving',
        description: 'Shows how many required questions are answered and whether your changes are saved. Use "required still empty" to jump to the ones left.',
      },
      {
        target: 'submit',
        title: 'Submit',
        description: "Once everything is answered and your references are in, submit before the deadline. Answers can't be changed after you submit.",
      },
    ],
  },
  {
    page: 'ApplicationStatus',
    label: 'Application status',
    roles: ['applicant'],
    files: ['pages/applicant/ApplicationStatusPage.vue'],
    steps: [
      {
        target: 'status',
        title: 'Your status',
        description: 'Where your application stands, and when you submitted it.',
      },
      {
        target: 'awards',
        title: 'Scholarships awarded',
        description: "Scholarships you've won, with the amount. Download your award letter here.",
      },
      {
        target: 'answers',
        title: 'Your answers',
        description: 'A read-only copy of your application.',
      },
    ],
  },

  // ── Scorers and awarders ──────────────────────────────────────────────────
  {
    page: 'ScoringQueue',
    label: 'Scoring Queue',
    roles: ['scorer'],
    files: ['pages/scorer/ScoringQueuePage.vue', 'components/common/AppSidebar.vue', 'components/common/AppHeader.vue'],
    steps: [
      welcome('As a scorer, you read applications and score them. Here\'s a quick look around.'),
      {
        target: 'queue',
        title: 'Your queue',
        description: '"Pending" applications are waiting for your scores; "In Progress" ones you\'ve started. Click one to score it.',
      },
      menu('Everything you can do is listed here.'),
      account,
      replay,
    ],
  },
  {
    page: 'ScoringDetail',
    label: 'Score Application',
    roles: ['scorer', ...ADMINS],
    files: ['pages/scorer/ScoringDetailPage.vue'],
    steps: [
      {
        target: 'answers',
        title: "The applicant's answers",
        description: "Each section's answers, or a reference, are on the left. Scholarship sections are shown for reading but aren't scored.",
      },
      {
        target: 'score',
        title: 'Your score',
        description: 'Score each section from 1 to 100, with optional comments for the committee.',
      },
      {
        target: 'save-scores',
        title: 'Save',
        description: 'Save your scores. You can come back and change them later.',
      },
    ],
  },
  {
    page: 'AwardScholarships',
    label: 'Award Scholarships',
    roles: AWARDERS,
    files: ['pages/admin/AwardScholarshipsPage.vue'],
    steps: [
      {
        target: 'scholarship-select',
        title: 'Choose a scholarship',
        description: 'Pick one of the scholarships you award. Its description and award amount show underneath.',
      },
      {
        target: 'award-letter',
        title: 'Award letter',
        description: "Write this scholarship's own letter or use the default one, then download every awardee's letter as one PDF.",
      },
      {
        target: 'candidates',
        title: 'Eligible applicants',
        description: 'Submitted applications that qualify, with their average score. Award the scholarship here, with an amount and a note.',
      },
    ],
  },

  // ── Admins ────────────────────────────────────────────────────────────────
  {
    page: 'ApplicationsList',
    label: 'Applications',
    roles: ADMINS,
    files: ['pages/admin/ApplicationsListPage.vue', 'components/common/AppSidebar.vue', 'components/common/AppHeader.vue'],
    steps: [
      welcome("Here's a quick look at managing applications."),
      {
        target: 'filters',
        title: 'Find applications',
        description: 'Search by name or email, and filter by application period, school and status.',
      },
      {
        target: 'applications-table',
        title: 'Applications',
        description: "Each application's period, school, status and any scholarships it won. Click one to see its answers, scores and references.",
      },
      {
        target: 'export',
        title: 'Export',
        description: 'Download the applications matching your filters as a spreadsheet (CSV).',
      },
      menu('Scoring Progress, Application Periods (forms, references, scholarships and letters) and Users & Schools are all here.'),
      replay,
    ],
  },
  {
    page: 'ApplicationDetail',
    label: 'Application details',
    roles: ADMINS,
    files: ['pages/admin/ApplicationDetailPage.vue'],
    steps: [
      {
        target: 'applicant-header',
        title: 'The application',
        description: 'Who applied and when. You can also score the application yourself from here.',
      },
      {
        target: 'status',
        title: 'Status',
        description: 'Change the status by hand. "Awarded" is set by awarding a scholarship on the Award Scholarships page.',
      },
      {
        target: 'awards',
        title: 'Scholarships awarded',
        description: 'The scholarships this application won, and their amounts.',
      },
      {
        target: 'scores',
        title: 'Scores',
        description: "Every scorer's scores and comments, and the overall average.",
      },
      {
        target: 'references',
        title: 'References',
        description: 'Which references were requested and which have arrived.',
      },
      {
        target: 'answers',
        title: 'Answers',
        description: "The applicant's answers, as submitted.",
      },
    ],
  },
  {
    page: 'ScoringOverview',
    label: 'Scoring Progress',
    roles: ADMINS,
    files: ['pages/admin/ScoringOverviewPage.vue'],
    steps: [
      {
        target: 'scoring-table',
        title: 'Scoring progress',
        description: 'How many scorers have scored each submitted application, and its average. Click a row for each section\'s average.',
      },
    ],
  },
  {
    page: 'ScholarshipSettings',
    label: 'Application Periods',
    roles: ADMINS,
    files: ['pages/admin/SettingsPage.vue'],
    steps: [
      {
        target: 'new-period',
        title: 'New application period',
        description: 'Create a period with its own dates and form, starting from the default questions or an earlier period.',
      },
      {
        target: 'period',
        title: 'Application periods',
        description: "Each period's dates, and whether it's accepting applications.",
      },
      {
        target: 'duplicate',
        title: 'Duplicate',
        description: "Start next year's period from this one: its form, references, scholarships and letters are copied.",
      },
      {
        target: 'set-up',
        title: 'Set up',
        description: "Open a period to edit its details, questions, references, scholarships and award letters.",
      },
    ],
  },
  {
    page: 'CycleDetails',
    label: 'Application period: Details',
    roles: ADMINS,
    files: ['pages/admin/CyclePage.vue', 'pages/admin/cycle/CycleDetailsTab.vue'],
    steps: [
      {
        target: 'period-tabs',
        title: 'Setting up a period',
        description: 'Each tab covers one part of this application period: its details, application questions, references, scholarships and award letters.',
      },
      {
        target: 'period-details',
        title: 'Details',
        description: "The period's name, its open and close dates, and whether it's accepting applications.",
      },
    ],
  },
  {
    page: 'CycleQuestions',
    label: 'Application period: Application Questions',
    roles: ADMINS,
    files: ['pages/admin/CyclePage.vue', 'pages/admin/CycleQuestionsPage.vue'],
    steps: [
      {
        title: 'Application questions',
        description: 'This is the form applicants fill in, in the order they see it. Group questions into sections; questions outside any section come last.',
      },
      ...builderSteps,
    ],
  },
  {
    page: 'CycleReferences',
    label: 'Application period: References',
    roles: ADMINS,
    files: ['pages/admin/CyclePage.vue', 'pages/admin/cycle/CycleReferencesTab.vue'],
    steps: [
      {
        target: 'reference-forms',
        title: 'References',
        description: 'The references applicants must get. Each has its own questions for the person writing it.',
      },
      {
        target: 'add-reference',
        title: 'Add a reference',
        description: "Set how many are needed, and whether it's always required or only for applicants with certain answers.",
      },
      {
        target: 'reference-form',
        title: 'Its questions',
        description: 'Use "Questions" to write what the Reference Provider answers.',
      },
    ],
  },
  {
    page: 'CycleScholarships',
    label: 'Application period: Scholarships',
    roles: ADMINS,
    files: ['pages/admin/CyclePage.vue', 'pages/admin/cycle/CycleScholarshipsTab.vue'],
    steps: [
      {
        target: 'scholarships',
        title: 'Scholarships',
        description: 'The scholarships awarded from this application period.',
      },
      {
        target: 'add-scholarship',
        title: 'Add a scholarship',
        description: 'Give it a name, amount and eligibility rule, and choose who awards it.',
      },
      {
        target: 'scholarship',
        title: 'Eligibility and questions',
        description: "Eligibility questions decide who qualifies. Scholarship questions are only shown to applicants who do.",
      },
    ],
  },
  {
    page: 'AwardLetters',
    label: 'Application period: Award Letters',
    roles: ADMINS,
    files: ['pages/admin/CyclePage.vue', 'pages/admin/AwardLettersPage.vue'],
    steps: [
      {
        target: 'base-letters',
        title: 'Base letters',
        description: 'Letters any scholarship can use. Every scholarship uses the default one unless it has its own.',
      },
      {
        target: 'scholarship-letters',
        title: 'Scholarship letters',
        description: "Give a scholarship its own letter, and download all of its awardees' letters as one PDF.",
      },
    ],
  },
  {
    page: 'ReferenceFormQuestions',
    label: 'Reference questions',
    roles: ADMINS,
    files: ['pages/admin/CycleQuestionsPage.vue'],
    steps: [
      {
        title: 'Reference questions',
        description: "What the Reference Provider answers. They also give their name and signature, so you don't need questions for those.",
      },
      ...builderSteps,
      { target: 'back', title: 'Back to References', description: 'Return to the list of references.' },
    ],
  },
  {
    page: 'ScholarshipQuestions',
    label: 'Scholarship questions',
    roles: ADMINS,
    files: ['pages/admin/CycleQuestionsPage.vue'],
    steps: [
      {
        title: 'Scholarship questions',
        description: 'Eligibility questions are asked of every applicant. Scholarship questions only appear for applicants who are eligible.',
      },
      ...builderSteps,
      { target: 'back', title: 'Back to Scholarships', description: 'Return to the list of scholarships.' },
    ],
  },
  {
    page: 'AdminSettings',
    label: 'Users & Schools',
    roles: ADMINS,
    files: ['pages/admin/AdminSettingsPage.vue'],
    steps: [
      {
        target: 'admin-tabs',
        title: 'Users, schools and invites',
        description: "Manage everyone's accounts, the schools, and the links school admins use to register.",
      },
      {
        target: 'user-filters',
        title: 'Find people',
        description: 'Search by name, email or school, or show one role at a time.',
      },
      {
        target: 'add-user',
        title: 'Add a user',
        description: 'Counselors get an email to create their own password; for everyone else, you set one.',
      },
      {
        target: 'user-actions',
        title: 'Actions',
        description: "Edit someone's details, reset their password or two-factor, limit what a scorer sees, or deactivate their account.",
      },
    ],
  },

  // ── Schools ───────────────────────────────────────────────────────────────
  {
    page: 'SchoolDetail',
    label: 'My School',
    roles: ['school_admin'],
    files: ['pages/school/SchoolDetailPage.vue', 'components/common/AppSidebar.vue', 'components/common/AppHeader.vue'],
    steps: [
      welcome("Here's a quick look at managing your school."),
      {
        target: 'school-info',
        title: 'Your school',
        description: "Keep your school's name and address up to date.",
      },
      {
        target: 'counselors',
        title: 'Counselors',
        description: 'Add your counselors; each gets an email to create a password. You can resend invites, reset passwords and remove counselors here.',
      },
      {
        target: 'students-link',
        title: 'Students',
        description: 'Your students and their applications are on the Students page.',
      },
      menu('Students, your references and everything else you can do are here.'),
      replay,
    ],
  },
  {
    page: 'CounselorDashboard',
    label: 'Students',
    roles: ['counselor', 'school_admin'],
    files: ['pages/counselor/CounselorDashboardPage.vue', 'components/common/AppSidebar.vue', 'components/common/AppHeader.vue'],
    steps: (role) => [
      ...(role === 'counselor' ? [welcome("Here's a quick look at following your students' applications.")] : []),
      {
        target: 'student-filters',
        title: 'Find students',
        description: 'Search by name or email, or show only students with a certain application status.',
      },
      {
        target: 'students-table',
        title: 'Your students',
        description: "Every student at your school with an account, their application's status and their last activity.",
      },
      {
        target: 'remind',
        title: 'Send a reminder',
        description: 'Email a student a reminder about their application.',
      },
      ...(role === 'counselor' ? [menu('Your references and account are here too.'), replay] : []),
    ],
  },

  // ── References ────────────────────────────────────────────────────────────
  {
    page: 'MyReferences',
    label: 'My References',
    roles: REFERENCE_WRITERS,
    files: ['pages/reference/MyReferencesPage.vue', 'components/common/AppSidebar.vue', 'components/common/AppHeader.vue'],
    steps: (role) => [
      ...(role === 'referee' ? [welcome('Here you write references for scholarship applicants.')] : []),
      {
        target: 'reference-code',
        title: 'Write a reference',
        description: 'Open the reference link you were sent, or type its 10-character code here.',
      },
      {
        target: 'submitted-references',
        title: 'Your submitted references',
        description: "References you've written. Open one to see what you submitted.",
      },
      ...(role === 'referee' ? [account, replay] : []),
    ],
  },
  {
    page: 'ReferenceForm',
    label: 'Write a reference',
    roles: REFERENCE_WRITERS,
    files: ['pages/reference/ReferenceFormPage.vue'],
    steps: [
      {
        target: 'ref-applicant',
        title: "Who it's for",
        description: 'The applicant this reference is for.',
      },
      {
        target: 'ref-question',
        title: 'Questions',
        description: 'Your answers go straight to the scholarship committee. The applicant never sees them.',
      },
      {
        target: 'signature',
        title: 'Sign',
        description: 'Type your full name and draw your signature.',
      },
      {
        target: 'submit-reference',
        title: 'Submit',
        description: "Submit your reference. You can't change it afterwards, but you can always look at it on My References.",
      },
    ],
  },
  {
    page: 'MyReference',
    label: 'Submitted reference',
    roles: REFERENCE_WRITERS,
    files: ['pages/reference/MyReferenceDetailPage.vue'],
    steps: [
      {
        target: 'your-reference',
        title: 'What you submitted',
        description: 'Your answers and signature, exactly as the committee sees them.',
      },
    ],
  },
]

// cyrb53: a small, fast string hash (two 32-bit halves); plenty to tell versions apart
function hash(text) {
  let h1 = 0xdeadbeef
  let h2 = 0x41c6ce57
  for (let i = 0; i < text.length; i++) {
    const ch = text.charCodeAt(i)
    h1 = Math.imul(h1 ^ ch, 2654435761)
    h2 = Math.imul(h2 ^ ch, 1597334677)
  }
  h1 = Math.imul(h1 ^ (h1 >>> 16), 2246822507) ^ Math.imul(h2 ^ (h2 >>> 13), 3266489909)
  h2 = Math.imul(h2 ^ (h2 >>> 16), 2246822507) ^ Math.imul(h1 ^ (h1 >>> 13), 3266489909)
  return (h2 >>> 0).toString(16).padStart(8, '0') + (h1 >>> 0).toString(16).padStart(8, '0')
}

/** The tour for a page and role: { id, version, steps }, or null if there isn't one. */
export function tourFor(page, role) {
  const tour = tours.find((t) => t.page === page && t.roles.includes(role))
  if (!tour) return null
  const steps = typeof tour.steps === 'function' ? tour.steps(role) : tour.steps
  const version = hash(JSON.stringify({
    steps: steps.map(({ target, title, description }) => ({ target, title, description })),
    files: tour.files.map((f) => [f, componentHashes[f] ?? 'missing']),
  }))
  return { id: `${role}:${page}`, version, steps }
}

/** On-screen name of a tour id ("<role>:<page>"), e.g. for an admin's view of someone's tours. */
export function tourLabel(id) {
  const page = id.split(':')[1]
  return tours.find((t) => t.page === page)?.label ?? page
}
