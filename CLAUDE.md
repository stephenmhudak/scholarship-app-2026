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
| `QuestionRenderer` | `question`, `modelValue`, `error` — picks the per-type question component |

---

## Auth & Roles

**Roles:** `applicant`, `scorer`, `app_admin`, `school_admin`, `counselor`

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
| POST | `/api/auth/change-password` | Bearer | `{ currentPassword, newPassword }` → 204 |
| GET | `/api/auth/2fa` | Bearer | `{ enabled, method, recoveryCodesRemaining }` |
| POST | `/api/auth/2fa/email/setup` · `/2fa/email/enable` | Bearer | Email method: setup emails a code → enable `{ code }` |
| POST | `/api/auth/2fa/setup` · `/2fa/enable` · `/2fa/disable` · `/2fa/recovery-codes` | Bearer | setup → `{ secret, otpauthUri }`; enable `{ code }` → `{ recoveryCodes }`; disable / recovery-codes `{ password }` |
| POST | `/api/admin/users/{id}/reset-2fa` | app_admin | Clears a user's 2FA (lost device + codes) |
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
| GET | `/api/admin/applications` | app_admin | `?status=&page=&pageSize=` → `{data, total, page, pageSize}` |
| PATCH | `/api/admin/applications/{id}/status` | app_admin | `{ status }` |
| POST | `/api/files/upload` | auth | Multipart `file`, `applicationId`, `questionId?` → `{ fileId }` |
| GET/POST | `/api/references/{code}` | **PUBLIC** | Validate / upload reference |
| GET | `/api/scoring/queue` | scorer | Assigned applications |
| POST | `/api/scoring/{appId}/score` | scorer | `{ scoreValue, comments? }` |

---

## Data Models

**Users:** Id, Email, PasswordHash, FirstName, LastName, Role, SchoolId (nullable)

**ScholarshipCycles:** Id, Name, OpenDate, CloseDate, IsActive

**Sections:** Id, CycleId, Title, Description, Order

**Questions:** Id, CycleId, SectionId (nullable), Text, Type, Order, IsRequired, ValidationRules (JSON)
- Types: `short_answer`, `long_answer`, `multiple_choice`, `single_choice`, `file_upload`, `school_select`, `date`
- ValidationRules: `{ numberOnly, phoneFormat, emailFormat, minLength, maxLength, richText }`
- `richText` (long_answer only, check with `isRichText()` in `utils/richText.js`): Tiptap editor, answer stored as HTML — sanitized on save by `RichTextSanitizer` and again on render by `RichTextAnswer` (DOMPurify); keep their tag allow-lists in sync

**Applications:** Id, ApplicantId, CycleId, Status (`draft`→`submitted`→`under_review`→`awarded`/`rejected`, or `draft`→`archived` when the cycle closes unsubmitted)

**ApplicationAnswers:** ApplicationId, QuestionId, TextValue, SelectedOptions (JSON array of option IDs)

**References:** Id, ApplicationId, Code (SHA-256 hash), Label, Status (`pending`/`received`), ExpiresAt

---

## Gotchas

- **No EF** — raw SqlKata + Dapper. Write explicit UPDATE queries.
- **File upload is two-step:** upload (multipart `file`, `applicationId`, `questionId`) → `fileId` → store in answer. `ApplicationFiles.ApplicationId` is a FK, so uploads must name the applicant's own draft application.
- **Email verification:** self-registered users can't log in until `Users.EmailVerifiedAt` is set (login → 403 `{ code: "email_not_verified" }`). Admin/school-created users are pre-verified. Emails go through `IEmailService` — Resend if `Email:Resend:ApiKey` is set, otherwise logged to console. In Docker, credentials go in `secrets/*.env` (copy the `.example` files): `jwt.env`, `db.env` and `mysql.env` are required, as are the matching `k8s/*-secret.yaml` Secrets in k8s. Only `appsettings.Development.json` holds dev values; the API refuses to start without `ConnectionStrings:Default` or with a missing, short or placeholder `Jwt:Key`. MySQL isn't published to the host.
- **Password reset codes** live in `PasswordResetCodes`, hashed as SHA-256 of `userId:code`. Only the newest code is valid; a successful reset or change marks all codes used and also verifies the email. Both send a "password changed" email.
- **Two-factor is opt-in, one method at a time:** `Users.TwoFactorMethod` = `totp` (authenticator app, Otp.NET, with recovery codes) or `email` (6-digit code emailed per sign-in, stored hashed on the challenge, 10 min; no recovery codes). Null = off; it's the only "is 2FA on" flag (`TotpEnabledAt` is just the TOTP enrollment time). `TwoFactorService` handles enrollment and code checks. `Users.TotpSecret` is AES-GCM encrypted by `TotpSecretProtector` with `TwoFactor:EncryptionKey` (base64 32 bytes; required, the API won't start without it; dev key in `appsettings.Development.json`, Docker `secrets/twofactor.env`, k8s `twofactor-credentials`). Losing or changing the key means every enrolled user needs a 2FA reset. After a password sign-in, `useSignInRedirect()` sends non-enrolled users to `/two-factor` (setup or "Not now") on every login. Enrolled users get a `TwoFactorChallenges` row (5 min, 5 tries; 10 tries per user per 15 min); TOTP steps can't be replayed (`TotpLastUsedStep`). Any new sign-in path must go through `SignInOrChallengeAsync`, not `BuildAuthResponse`, or it bypasses 2FA.
- **Public endpoints are rate limited per client IP** (`RateLimitPolicies`: `auth` 10/min for sign-in, 2FA, register, reset and verification; `public` 30/min for invite and reference links) → 429 `{ error }`. Accounts are never locked. The client IP comes from `X-Forwarded-For` across two proxy hops (Caddy/ingress → nginx); see `UseForwardedHeaders` in `Program.cs`.
- **Account changes end sessions:** `SessionVersion.BumpAsync` (password reset/change, admin or school-admin password reset, 2FA enable/disable/reset) increments `Users.SessionVersion`; tokens carry it as the `sv` claim and `OnTokenValidated` rejects stale ones (and deleted users). When the change is the user's own, the endpoint returns a replacement token in `X-Session-Token`, which `api.js` stores.
- **Input rules live in `InputValidation`:** 8+ character passwords (max 72 bytes, BCrypt's limit) everywhere a password is set, email format, roles, application statuses, required/max-length text. Use it for any new endpoint that takes user input.
- **School admin invites** are stored as `TokenHash` (SHA-256); the link is only shown when generated. Registration claims the invite with a conditional update inside a transaction, so a link can only be used once.
- **Reference codes are hashed** — store SHA-256, compare hash on lookup. Plaintext returned once.
- **Both frontend routing AND backend auth enforce roles** — check both when adding features.
- **Who can read an application** (answers, references, files) is decided by `ApplicationAccess`: app_admin all, scorer once scoreable, applicant their own, everyone else denied. **Scoreable** = submitted (or later), or a draft/archived application whose cycle has ended; scores are 1–100, one per cycle section (or one with no section when the cycle has none). Use it for anything new hanging off an application. School endpoints are limited to the caller's own `schoolId` claim (app_admin excepted). Draft file answers must name the applicant's own uploads.
- **Status transitions are manual** — no state machine. Admin can set any valid status. Exception: `ApplicationArchiveService` (hourly) archives drafts in closed cycles.
- **Multiple open cycles** — a cycle is open when `IsActive` and today is within Open/CloseDate, through the end of the close day (`CycleWindow`). One application per applicant per cycle. Save/submit/copy reject closed cycles.
- **Copying answers** between cycles only fills unanswered questions whose text, type, rich text flag, and option texts match; choice answers are remapped by option text.
- **Counselors are scoped to SchoolId** — set at creation, no UI to change. Filter lists by school.
- **Sections are optional** — `SectionId = null` = ungrouped. Form groups them at the end.
