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
| Frontend | Vue 3 (Composition API), Vite, Tailwind CSS v3, Pinia, Vue Router, Axios |
| Backend | ASP.NET Core 10, SqlKata + Dapper (no EF), DbUp migrations |
| Database | MySQL — UUID (CHAR(36)) PKs, DATETIME timestamps, JSON columns |
| Auth | JWT Bearer (60 min), BCrypt passwords, role claims |
| Files | Azure Blob Storage or local (`wwwroot/uploads`), 10 MB max |
| Icons | Material Design Icons (`mdi-*`) |

---

## Project Layout

```
frontend/src/
  assets/main.css          # Tailwind + Dialect Design System utilities
  components/common/       # BaseButton, BaseInput, BaseAlert, BaseModal, AppHeader, AppSidebar
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

## Dialect Design System

**Never use** `text-gray-*`, `bg-gray-*`, `border-gray-*`, `text-blue-*`. Use Dialect tokens only.

### CSS Classes (defined in `main.css`)

| Class | Usage |
|-------|-------|
| `card` | White surface (`bg-white rounded-2xl shadow-card border border-[#E9EDF7]`) |
| `section-card` | `card p-6` |
| `form-input` / `.is-error` | Text inputs/textareas |
| `form-select` / `.is-error` | Dropdowns |
| `page-title` | H1 (`text-2xl font-bold text-navy`) |
| `page-subtitle` | `text-sm text-slate-400` |
| `dialect-table` | Full-width table with Dialect styles |
| `pagination-btn` | Prev/Next table buttons |

### Color Tokens

```
Primary:  text-primary / bg-primary / text-primary-700  (#4361EE)
Text:     text-navy (#2B3674) | text-slate-400 (#A3AED0)
Borders:  border-[#E9EDF7]
BG:       bg-page / bg-[#F4F7FE] / bg-white
Success:  text-success / bg-success-light / border-success/20
Danger:   text-danger / bg-danger-light / border-danger/30
Warning:  bg-warning-light / text-[#8C6500] / border-warning/30
Shadows:  shadow-card | shadow-card-md | shadow-card-lg
```

### Patterns

- **Page shell:** `<div class="space-y-6">` → `page-title` + `page-subtitle` → `<BaseAlert>` → `section-card` / `card overflow-hidden` + `dialect-table`
- **Checkbox:** `class="w-4 h-4 rounded border-[#E9EDF7] text-primary focus:ring-primary/30"`
- **Store:** Pinia setup store — `defineStore('name', () => { ... })` with `api.get/post` calls. See any existing store for reference.

---

## Base Components

| Component | Key Props |
|-----------|-----------|
| `BaseButton` | `variant` (primary\|secondary\|ghost\|danger), `size` (sm\|md\|lg), `loading`, `disabled`, `type` |
| `BaseInput` | `modelValue`, `type`, `label`, `placeholder`, `error`, `required`, `disabled` |
| `BaseAlert` | `type` (success\|error\|warning\|info), `message`, `dismissible` |
| `BaseModal` | `show`, `title` — emits `close` |
| `StatusBadge` | `status` (draft\|submitted\|under_review\|awarded\|rejected) |

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
| POST | `/api/auth/login` | Public | Returns `{ token, userId, role, ... }` |
| POST | `/api/auth/register` | Public | Creates applicant |
| GET | `/api/auth/me` | Bearer | Current user |
| GET | `/api/applications/my` | applicant | Own applications |
| GET | `/api/applications/current` | applicant | Most recent |
| POST | `/api/applications` | applicant | Create (uses active cycle) |
| GET | `/api/applications/{id}` | owner/scorer/admin | Full app + answers |
| PUT | `/api/applications/{id}/draft` | applicant | `{ answers: [{questionId, textValue?, selectedOptions?}] }` |
| POST | `/api/applications/{id}/submit` | applicant | Submit |
| GET | `/api/cycles/{id}/questions` | auth | Questions + options |
| PUT | `/api/cycles/{id}/questions/reorder` | app_admin | `[{id, order}]` |
| GET | `/api/admin/applications` | app_admin | `?status=&page=&pageSize=` → `{data, total, page, pageSize}` |
| PATCH | `/api/admin/applications/{id}/status` | app_admin | `{ status }` |
| POST | `/api/files/upload` | auth | Multipart → `{ fileId }` |
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
- ValidationRules: `{ numberOnly, phoneFormat, emailFormat, minLength, maxLength }`

**Applications:** Id, ApplicantId, CycleId, Status (`draft`→`submitted`→`under_review`→`awarded`/`rejected`)

**ApplicationAnswers:** ApplicationId, QuestionId, TextValue, SelectedOptions (JSON array of option IDs)

**References:** Id, ApplicationId, Code (SHA-256 hash), Label, Status (`pending`/`received`), ExpiresAt

---

## Gotchas

- **No EF** — raw SqlKata + Dapper. Write explicit UPDATE queries.
- **File upload is two-step:** upload → `fileId` → store in answer. Files have `ApplicationId = "pending"` until submit.
- **Reference codes are hashed** — store SHA-256, compare hash on lookup. Plaintext returned once.
- **Both frontend routing AND backend auth enforce roles** — check both when adding features.
- **Status transitions are manual** — no state machine. Admin can set any valid status.
- **Counselors are scoped to SchoolId** — set at creation, no UI to change. Filter lists by school.
- **Sections are optional** — `SectionId = null` = ungrouped. Form groups them at the end.
