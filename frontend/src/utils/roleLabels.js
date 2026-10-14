// What each role is called on screen. The `referee` role is a Reference Provider: someone who submits references.
const LABELS = {
  applicant: 'Applicant',
  scorer: 'Scorer',
  app_admin: 'App Admin',
  super_admin: 'Super Admin',
  school_admin: 'School Admin',
  counselor: 'Counselor',
  referee: 'Reference Provider',
}

export function roleLabel(role) {
  return LABELS[role] ?? role
}
