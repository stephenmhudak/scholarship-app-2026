# Scholarship App 2026 — CLAUDE.md

## Dev Commands

```bash
cd frontend && npm run dev          # http://localhost:5173
cd backend/ScholarshipApi && dotnet run   # http://localhost:5098
```

Vite proxies `/api/*` → `http://localhost:5098`. No CORS needed in dev.

---

## Stack

| Layer | Tech |
|-------|------|
| Frontend | Vue 3 (Composition API), Vite, Vuetify 4 (components), Tailwind CSS v3 (layout only), Pinia, Vue Router, Axios |
| Backend | ASP.NET Core 10, SqlKata + Dapper (no EF), DbUp migrations |
| Database | MySQL — UUID (CHAR(36)) PKs, DATETIME timestamps, JSON columns |
| Auth | JWT Bearer (60 min), BCrypt passwords, role claims |
| Files | Cloudflare R2, Azure Blob, or local (`wwwroot/uploads`) via `Storage:Provider`, 5 MB max (`FileUploadLimits`) |
| Icons | Material Design Icons (`mdi-*`) |

---

## Project Layout

```
frontend/src/
  plugins/vuetify.js       # Vuetify theme (Dialect palette) + component defaults
  assets/layers.css        # CSS cascade-layer order — must be imported first in main.js
  assets/main.css          # Tailwind + Vuetify overrides + Dialect typography
  components/common/       # AppHeader, AppSidebar, DateField
  components/application/  # Question type renderers
  components/scoring/      # ScoringForm, ScoreCard
  components/admin/        # StatusBadge, AssignScorerModal
  layouts/                 # DefaultLayout.vue, AuthLayout.vue
  pages/auth|applicant|scorer|admin|school|counselor|reference/
  router/index.js          # Routes + guards + role-based redirects
  stores/                  # auth.js, application.js, scoring.js, admin.js
  services/api.js          # Axios (baseURL /api, Bearer interceptor, 401 → /login)

backend/ScholarshipApi/
  Controllers/  Models/  DTOs/  Services/
  Data/Migrations/   # 00N_description.sql — DbUp runs on startup
  Program.cs         # DI, middleware, DbUp runner
```

---

## UI: Vuetify + Tailwind

**Use Vuetify components for all UI** (`v-btn`, `v-text-field`, `v-select`, `v-alert`, `v-dialog`, `v-data-table`, `v-chip`, …). Components are auto-imported by `vite-plugin-vuetify`; defaults (variants, density, rounding) are set globally in `plugins/vuetify.js`, so don't repeat them per use. Tiptap is the one exception: it stays the rich text editor, wrapped in Vuetify chrome.

**Tailwind is for layout and text only** (`flex`, `grid`, `gap-*`, `space-y-*`, `p-*`, `text-sm`, `text-navy`). Tailwind's preflight is off — Vuetify 4 ships native CSS layers, and unlayered preflight would override them. `layers.css` declares `dialect-base` below Vuetify; Tailwind utilities are unlayered, so they win over Vuetify.

**Never use** `text-gray-*`, `bg-gray-*`, `border-gray-*`, `text-blue-*`. Use Dialect tokens / theme colors.

### Theme colors (`plugins/vuetify.js`)

`primary` #4361EE · `secondary` navy #2B3674 · `success` #018F5C · `warning` #8C6500 · `error` #C94A3E · `background` #F4F7FE · border #E9EDF7. Status colors are the dark shades so they're readable as text in tonal alerts/chips. Vuetify 4 has no material color-name classes (`grey`, `red-lighten-2`) — use theme names or hex.

### CSS classes (`main.css`)

| Class | Usage |
|-------|-------|
| `page-title` / `page-subtitle` | Page H1 + subtitle |
| `field-label` | Question text above a field |
| `eyebrow` | Small uppercase label for read-only values |
| `clickable-rows` | On `v-data-table` with `@click:row` — pointer cursor |
| `rich-text` | Tiptap editor + `RichTextAnswer` content |
| Tailwind tokens | `text-navy`, `text-slate-400`, `text-danger`, `bg-[#F4F7FE]`, `border-[#E9EDF7]`, `shadow-card` |

### Patterns

- **Page shell:** `<div class="space-y-6">` → `page-title` + `page-subtitle` → `<v-alert v-if="alert" :type :text closable @click:close="alert = null">` → `<v-card title="…"><v-card-text class="pa-6">` sections. A titled card gets a blue-accent-4 (`#2962FF`) title bar with white text (`main.css`); add `card-header-secondary|success|warning|error` to the `v-card` when another color fits. Header actions go in `#append` (`variant="tonal"`, no color, so they turn white). Don't hand-write an `<h2>` inside a card for its title.
- **Tables:** `<v-card class="overflow-hidden"><v-data-table :headers :items :loading no-data-text>`; cell slots `#item.<key>="{ item, value }"`; row click `@click:row="(e, { item }) => …"`. Sorting only works on header keys (use a header `value` fn for computed sorts). Server paging: `v-data-table-server` + `@update:options` (see `ApplicationsListPage`).
- **Dialogs:** `<v-dialog v-model><v-card title>` with a close `v-btn` in `#append`, form in `v-card-text`. Rounding (`lg`) is global (`plugins/vuetify.js`) and the title bar follows the card rule above — don't restyle per dialog.
- **Confirmations:** never `window.confirm`/`alert`/`prompt`. Use `const confirm = useConfirm()` (`composables/useConfirm.js`) → `if (!await confirm({ title, text, confirmText: 'Delete', color: 'error' })) return`. It drives the single `ConfirmDialog` mounted in `App.vue`.
- **Dates:** use `DateField` (wraps `v-date-input`, v-model is a `'YYYY-MM-DD'` string parsed in local time).
- **Loading:** `v-progress-circular indeterminate` + text; empty states: `v-empty-state` inside a `v-card`.
- **Store:** Pinia setup store — `defineStore('name', () => { ... })` with `api.get/post` calls. See any existing store for reference.

---

## Shared Components

| Component | Key Props |
|-----------|-----------|
| `StatusBadge` | `status` (draft\|submitted\|under_review\|awarded\|rejected\|archived) → `v-chip` |
| `ConfirmDialog` | none — mounted once in `App.vue`, driven by `useConfirm()` |
| `DateField` | `modelValue` (`'YYYY-MM-DD'`), passes other props to `v-date-input` |
| `QuestionRenderer` | `question`, `modelValue`, `error`, `uploadUrl?` (reference links) — picks the per-type question component |
| `ApplicationReferences` | `applicationId`, `compact` — applicant's needed references, create/copy/delete links |
| `SignaturePad` | `modelValue` (JPEG data URL or null), `error` |

---

## Auth & Roles

**Roles:** `super_admin` (every permission, incl. future ones via `GetPermissionsForRoleAsync`; passes every admin check through `AppRoles.IsAdmin`; can impersonate), `applicant`, `scorer`, `app_admin`, `school_admin` (principals: managing their school plus everything a counselor can do, incl. the counselor dashboard), `counselor`, `referee` (self-registers via `/register?as=referee`; `submit_references` permission, which counselors and school admins also have)

- Token: `localStorage.auth_token` — Axios interceptor attaches it; 401 clears + redirects `/login`
- Frontend: `meta.requiredRole` / `meta.anyRole` in `router/index.js`
- Backend: `[Authorize(Policy = "app_admin")]`; userId = `User.FindFirstValue(ClaimTypes.NameIdentifier)`

---

## Backend Patterns

- **SqlKata:** `db.Query("Table as t").Join(...).Select(...).Where(...).When(...).ForPage(page, pageSize).GetAsync<Dto>()`
- **Migrations:** Add `Data/Migrations/00N_description.sql` — DbUp auto-runs on startup
- **Errors:** Throw in services → `ExceptionHandlingMiddleware` → `{ "error": "message" }` with HTTP status

---

## Key API Endpoints

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| POST | `/api/auth/login` | Public | Returns `{ token, userId, role, twoFactorEnabled, ... }`, or `{ twoFactorRequired, challengeToken, method, maskedEmail? }` if enrolled in 2FA |
| POST | `/api/auth/login/2fa` | Public | `{ challengeToken, code }` (app, recovery or emailed code) → same payload as login |
| POST | `/api/auth/login/2fa/resend` | Public | `{ challengeToken }` → 204; new emailed code (email method, 1 min cooldown) |
| POST | `/api/auth/register` | Public | Creates applicant, emails verification link (no token returned) |
| POST | `/api/auth/verify-email` | Public | `{ token }` → same payload as login (2FA challenge included) |
| POST | `/api/auth/resend-verification` | Public | `{ email }` → 204 always (no enumeration, 1 min cooldown) |
| POST | `/api/auth/forgot-password` | Public | `{ email }` → 204 always; emails a 6-digit code (15 min, 5 tries, 1 min cooldown) |
| POST | `/api/auth/reset-password` | Public | `{ email, code, newPassword }` → same payload as login (2FA challenge included) |
| POST | `/api/auth/setup-password/check` · `/setup-password` | Public | `{ token }` → `{ email, firstName }`; `{ token, newPassword }` → same payload as login. Invited counselors create their first password |
| POST | `/api/schools/{id}/counselors/{cid}/resend-invite` · `/api/admin/users/{id}/resend-invite` | school_admin · app_admin | Emails a new setup link (1 min cooldown) |
| POST | `/api/auth/change-password` | Bearer | `{ currentPassword, newPassword }` → 204 |
| GET | `/api/auth/2fa` | Bearer | `{ enabled, method, recoveryCodesRemaining }` |
| POST | `/api/auth/2fa/email/setup` · `/2fa/email/enable` | Bearer | Email method: setup emails a code → enable `{ code }` |
| POST | `/api/auth/2fa/setup` · `/2fa/enable` · `/2fa/disable` · `/2fa/recovery-codes` | Bearer | setup → `{ secret, otpauthUri }`; enable `{ code }` → `{ recoveryCodes }`; disable / recovery-codes `{ password }` |
| POST | `/api/admin/users/{id}/reset-2fa` | app_admin | Clears a user's 2FA (lost device + codes) |
| GET/PUT | `/api/admin/users/{id}/scoring-scope` | app_admin | Scorers only: `{ scope: all\|filtered, filters: [{questionId, optionId?, textContains?}] }` |
| POST | `/api/schools/{id}/counselors/{cid}/reset-2fa` | school_admin | Same, for the school's counselors |
| GET | `/api/auth/me` | Bearer | Current user |
| GET | `/api/applications/my` | applicant | Own applications |
| GET | `/api/applications/current` | applicant | Most recent |
| GET | `/api/applications/open-cycles` | applicant | Cycles accepting applications now |
| POST | `/api/applications` | applicant | `{ cycleId? }` — one per cycle; returns the existing one if already started |
| POST | `/api/applications/{id}/copy-answers` | applicant | `{ sourceApplicationId }` → `{ copied, alreadyAnswered }` |
| GET | `/api/applications/{id}` | owner/scorer/admin | Full app + answers |
| PUT | `/api/applications/{id}/draft` | applicant | `{ answers: [{questionId, textValue?, selectedOptions?}] }` |
| POST | `/api/applications/{id}/submit` | applicant | Submit |
| GET | `/api/cycles/{id}/questions` | auth | Questions + options |
| PUT | `/api/cycles/{id}/questions/reorder` | app_admin | `[{id, order}]` |
| POST | `/api/cycles/{id}/questions/bulk-delete` | app_admin | `{ questionIds }` (`?force=true` after a 409 listing answers/conditions that go too) |
| PUT | `/api/cycles/{id}/questions/move` | app_admin | `{ questionIds, sectionId }` (null = ungrouped); appended to the end of the section |
| GET | `/api/admin/applications` | app_admin | `?status=&page=&pageSize=` → `{data, total, page, pageSize}` |
| PATCH | `/api/admin/applications/{id}/status` | app_admin | `{ status }` |
| POST | `/api/files/upload` | auth | Multipart `file`, `applicationId`, `questionId?` → `{ fileId }` |
| GET/POST/PUT/DELETE | `/api/cycles/{id}/reference-forms` | app_admin | Reference forms: `{ title, description, requiredCount, requirement: always\|conditional, conditions: [{questionId, optionId?, textContains?}] }`; questions via `/api/cycles/{id}/questions?referenceFormId=` |
| GET/POST/DELETE | `/api/applications/{id}/references` | applicant (own) / admin | Needed forms + links (status only). POST `{ formId, label? }` → link; DELETE `/{requestId}` (pending only) |
| GET | `/api/reference-links/{code}` | Referee policy | Referee's form: applicant name/school, questions, `status` open\|submitted\|closed |
| POST | `/api/reference-links/{code}/files` · `/submit` | Referee policy | Multipart `file`, `questionId` → `{ fileId }`; submit `{ signerName, signature (JPEG data URL), answers }` |
| GET | `/api/my-references` · `/{id}` · `/{id}/signature` · `/files/{fileId}` | Referee policy | References the caller submitted (answers, who for; never scores) |
| GET | `/api/scoring/{appId}/references` | scorer/admin | Submitted references with answers; `/api/scoring/references/{id}/signature`, `/references/files/{fileId}` |
| GET | `/api/scoring/queue` | scorer | Assigned applications |
| POST | `/api/scoring/{appId}/score` | scorer | `{ scoreValue, comments? }` |

---

## Data Models

**Users:** Id, Email, PasswordHash, FirstName, LastName, Role, SchoolId (nullable)

**ScholarshipCycles:** Id, Name, OpenDate, CloseDate, IsActive

**Sections:** Id, CycleId, Title, Description, Order

**Questions:** Id, CycleId, SectionId (nullable), Text, Type, Order, IsRequired, ValidationRules (JSON), Visibility (`always`/`conditional`), ConditionMatch (`any`/`all`)
- Types: `short_answer`, `long_answer`, `multiple_choice`, `single_choice`, `file_upload`, `school_select`, `date`
- ValidationRules: `{ numberOnly, phoneFormat, emailFormat, minLength, maxLength, richText }`
- `richText` (long_answer only, check with `isRichText()` in `utils/richText.js`): Tiptap editor, answer stored as HTML — sanitized on save by `RichTextSanitizer` and again on render by `RichTextAnswer` (DOMPurify); keep their tag allow-lists in sync

**Applications:** Id, ApplicantId, CycleId, Status (`draft`→`submitted`→`under_review`→`awarded`/`rejected`, or `draft`→`archived` when the cycle closes unsubmitted)

**ApplicationAnswers:** ApplicationId, QuestionId, TextValue, SelectedOptions (JSON array of option IDs)

**ReferenceForms:** Id, CycleId, Title, Description, RequiredCount, Requirement (`always`/`conditional`), Order. Questions with `ReferenceFormId` set belong to a form (application queries filter `WhereNull("ReferenceFormId")`).

**ReferenceFormConditions:** FormId, QuestionId (application question), OptionId (choice) or TextContains (text, case-insensitive). A conditional form is required when any condition matches the saved answers (`ReferenceRequirements`).

**ReferenceRequests:** Id, ApplicationId, FormId, Label, CodeHash, CodeEncrypted, Status (`pending`/`submitted`), SignerName, SignaturePath, SubmittedAt. Answers in **ReferenceAnswers**, uploads in **ReferenceFiles**. (The old `References`/`ReferenceDocuments` tables are unused.)

---

## Gotchas

- **No EF** — raw SqlKata + Dapper. Write explicit UPDATE queries.
- **File upload is two-step:** upload (multipart `file`, `applicationId`, `questionId`) → `fileId` → store in answer. `ApplicationFiles.ApplicationId` is a FK, so uploads must name the applicant's own draft application.
- **Email verification:** self-registered users can't log in until `Users.EmailVerifiedAt` is set (login → 403 `{ code: "email_not_verified" }`). Admin/school-created users are pre-verified. Emails go through `IEmailService` — Resend if `Email:Resend:ApiKey` is set, otherwise logged to console. In Docker, credentials go in `secrets/*.env` (copy the `.example` files): `jwt.env`, `db.env` and `mysql.env` are required, as are the matching `k8s/*-secret.yaml` Secrets in k8s. Only `appsettings.Development.json` holds dev values; the API refuses to start without `ConnectionStrings:Default` or with a missing, short or placeholder `Jwt:Key`. MySQL isn't published to the host.
- **Counselors are invited, not given a password:** adding one (school admin, or admin "Add User" with role counselor) creates the user with `PasswordHash = ''` and emails a `PasswordSetupTokens` link (`/setup-password?token=`, hashed, 7 days, newest only). Login treats an empty hash as invalid (`AuthService.HasPassword`; BCrypt can't verify one), so the account can't sign in until the link is used, which also verifies the email. Lists show `InvitePending`; school admins can't set a pending counselor's password (resend instead). Forgot-password also works for a pending account.
- **Password reset codes** live in `PasswordResetCodes`, hashed as SHA-256 of `userId:code`. Only the newest code is valid; a successful reset or change marks all codes used and also verifies the email. Both send a "password changed" email.
- **Super admins & impersonation:** only a super admin can create or manage (password, 2FA, invite) a super admin; promote the first one in SQL (see `025_super_admin.sql`). Role checks must use `AppRoles.IsAdmin(role)`, not `== "app_admin"`. `POST /api/admin/users/{id}/impersonate` (SuperAdmin policy; not for super admins or yourself) returns a normal session for the target with `imp`/`imp_sv` claims, so every API and page behaves as that user; `OnTokenValidated` also ends it if the super admin loses the role or their sessions. `POST /api/auth/end-impersonation` returns the super admin's session. Password and 2FA endpoints use the `NotImpersonating` policy. Frontend: `authStore.impersonatorName`/`isImpersonating`; start/end reload the page; `AppHeader` swaps Logout for End impersonation. Starts and ends are logged.
- **Two-factor is opt-in, one method at a time:** `Users.TwoFactorMethod` = `totp` (authenticator app, Otp.NET, with recovery codes) or `email` (6-digit code emailed per sign-in, stored hashed on the challenge, 10 min; no recovery codes). Null = off; it's the only "is 2FA on" flag (`TotpEnabledAt` is just the TOTP enrollment time). `TwoFactorService` handles enrollment and code checks. `Users.TotpSecret` is AES-GCM encrypted by `TotpSecretProtector` with `TwoFactor:EncryptionKey` (base64 32 bytes; required, the API won't start without it; dev key in `appsettings.Development.json`, Docker `secrets/twofactor.env`, k8s `twofactor-credentials`). Losing or changing the key means every enrolled user needs a 2FA reset. After a password sign-in, `useSignInRedirect()` sends non-enrolled users to `/two-factor` (setup or "Not now") on every login. Enrolled users get a `TwoFactorChallenges` row (5 min, 5 tries; 10 tries per user per 15 min); TOTP steps can't be replayed (`TotpLastUsedStep`). Any new sign-in path must go through `SignInOrChallengeAsync`, not `BuildAuthResponse`, or it bypasses 2FA.
- **Public endpoints are rate limited per client IP** (`RateLimitPolicies`: `auth` 10/min for sign-in, 2FA, register, reset and verification; `public` 30/min for invite and reference links) → 429 `{ error }`. Accounts are never locked. The client IP comes from `X-Forwarded-For` across two proxy hops (Caddy/ingress → nginx); see `UseForwardedHeaders` in `Program.cs`.
- **Account changes end sessions:** `SessionVersion.BumpAsync` (password reset/change, admin or school-admin password reset, 2FA enable/disable/reset) increments `Users.SessionVersion`; tokens carry it as the `sv` claim and `OnTokenValidated` rejects stale ones (and deleted users). When the change is the user's own, the endpoint returns a replacement token in `X-Session-Token`, which `api.js` stores.
- **Input rules live in `InputValidation`:** 8+ character passwords (max 72 bytes, BCrypt's limit) everywhere a password is set, email format, roles, application statuses, required/max-length text. Use it for any new endpoint that takes user input.
- **School admin invites** are stored as `TokenHash` (SHA-256); the link is only shown when generated. Registration claims the invite with a conditional update inside a transaction, so a link can only be used once.
- **References:** the applicant creates a link per required reference (`/r/` + 10 chars `A–Z0–9`, `ReferenceCode`) and shares it (or just the code). The referee must be signed in with a `submit_references` account (referee, counselor, school admin) and can't be the applicant; `/r/CODE` sends anyone else to `/reference-start`, which remembers the code for after sign-in (`utils/pendingReference.js`, `useSignInRedirect`). Submissions record `SubmittedById`, and referees see their own on My References (`/references`). The referee answers the form, gives their name and draws a signature (`SignaturePad` → JPEG data URL, checked for the JPEG header, ≤ 1 MB). Codes are stored as SHA-256 for lookup and AES-GCM encrypted (two-factor key, request id as associated data) so the applicant can copy the link again. **Applicants never see reference answers**, only status; scorers see them once the application is scoreable and score each reference separately (`Scores.ReferenceRequestId`). Submitting an application requires every required form's `RequiredCount` references to be submitted. The applicant gets an email each time one arrives. Links close when the cycle closes. Reference forms can't use `school_select` (referees have no account).
- **Editing a choice question keeps option ids** whose text didn't change (answers and reference conditions point at them); removed options cascade-delete conditions on them.
- **Both frontend routing AND backend auth enforce roles** — check both when adding features.
- **Who can read an application** (answers, references, files) is decided by `ApplicationAccess`: app_admin all, scorer once scoreable, applicant their own, everyone else denied. **Scoreable** = submitted (or later), or a draft/archived application whose cycle has ended; scores are 1–100, one per cycle section (or one with no section when the cycle has none). Use it for anything new hanging off an application. **Scorers can be limited** (`Users.ScoringScope = 'filtered'`) to applications whose answers match any of their **ScorerFilters** (`ApplicationAccess.ScorerAllowedAsync`, same matching as conditions); it applies to viewing, the queue, references, files and submitting scores. A filtered scorer with no filters left sees nothing. Copying a cycle copies filters onto the new questions. School endpoints are limited to the caller's own `schoolId` claim (app_admin excepted). Draft file answers must name the applicant's own uploads.
- **Status transitions are manual** — no state machine. Admin can set any valid status. Exception: `ApplicationArchiveService` (hourly) archives drafts in closed cycles.
- **Multiple open cycles** — a cycle is open when `IsActive` and today is within Open/CloseDate, through the end of the close day (`CycleWindow`). One application per applicant per cycle. Save/submit/copy reject closed cycles.
- **Copying answers** between cycles only fills unanswered questions whose text, type, rich text flag, and option texts match; choice answers are remapped by option text.
- **Counselors are scoped to SchoolId** — set at creation, no UI to change. Filter lists by school.
- **Conditional questions:** an application question with `Visibility = 'conditional'` is only shown when any/all of its **QuestionConditions** (SourceQuestionId + OptionId for choice, or TextContains for text) match, and a condition only counts while its source question is itself shown. The same rule runs in `QuestionVisibility` (server) and `utils/questionVisibility.js` (form, status and admin views); keep them in sync. Saving a draft drops answers to hidden questions; submitting removes any left over and requires every shown required question. A condition's source must come **before** the question on the form (sections in order, ungrouped last, then `Order`); saving rejects anything else. After any reorder, move, section change/delete, option removal or question delete, `QuestionOrder.PruneConditionsAsync` removes conditions whose source no longer comes first (deleted sources go by cascade) and turns conditional questions with none left into `always`; those endpoints return `{ conditionsRemovedFrom, nowAlwaysShown }` for the builder to report. It also runs after copying a cycle and on API startup for every cycle (tidies older data). Question and section lists are ordered by `Order`, then `Id`, matching `QuestionOrder.Positions`. New questions are always appended (server sets `Order`). `AnswerConditions` is shared with reference-form conditions.
- **Sections are optional** — `SectionId = null` = ungrouped. Form groups them at the end.
