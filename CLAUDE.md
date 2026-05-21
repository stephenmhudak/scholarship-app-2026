# Scholarship App 2026 — CLAUDE.md

## Dev Commands

```bash
# Frontend (Vue 3 + Vite)
cd frontend && npm run dev          # http://localhost:5173
cd frontend && npm run build        # output to dist/

# Backend (ASP.NET Core 10)
cd backend/ScholarshipApi && dotnet run   # http://localhost:5098
```

Vite proxies `/api/*` → `http://localhost:5098`. No CORS config needed in dev.

---

## Stack

| Layer | Tech |
|-------|------|
| Frontend | Vue 3 (Composition API), Vite, Tailwind CSS v3, Pinia, Vue Router, Axios |
| Backend | ASP.NET Core 10, SqlKata + Dapper (no EF), DbUp migrations |
| Database | MySQL — UUID (CHAR(36)) PKs, DATETIME timestamps, JSON columns |
| Auth | JWT Bearer (60 min), BCrypt passwords, role claims |
| Files | Azure Blob Storage or local (`wwwroot/uploads`), 10 MB max |
| Icons | Material Design Icons (`mdi-*` class names) |

---

## Project Layout

```
frontend/src/
  assets/main.css          # Tailwind + Dialect Design System utilities
  components/
    common/                # BaseButton, BaseInput, BaseAlert, BaseModal, AppHeader, AppSidebar
    application/           # Question type renderers (ShortAnswer, LongAnswer, MultipleChoice, etc.)
    scoring/               # ScoringForm, ScoreCard
    reference/             # Public reference upload flow
    admin/                 # StatusBadge, AssignScorerModal
  layouts/
    DefaultLayout.vue      # Header + sidebar shell (authenticated)
    AuthLayout.vue         # Centered card (login/register)
  pages/
    auth/                  # LoginPage, RegisterPage
    applicant/             # Dashboard, ApplicationFormPage, ApplicationStatusPage, NewApplicationPage
    scorer/                # ScoringQueuePage, ScoringDetailPage
    admin/                 # ApplicationsListPage, ApplicationDetailPage, SettingsPage, CycleQuestionsPage
    school/                # SchoolsListPage, SchoolDetailPage
    counselor/             # CounselorDashboardPage
    reference/             # ReferenceUploadPage (PUBLIC — no auth)
  router/index.js          # Routes + navigation guards + role-based redirects
  stores/                  # auth.js, application.js, scoring.js, admin.js
  services/api.js          # Axios instance (baseURL /api, Bearer token interceptor, 401 → /login)
  utils/validate.js        # Form validation
  utils/fileUpload.js      # Multipart upload helper

backend/ScholarshipApi/
  Controllers/             # Grouped by domain — AuthController, ApplicationsController, etc.
  Models/                  # POCOs mapped by Dapper
  DTOs/                    # Request/Response shapes
  Services/                # Business logic interfaces + implementations
  Data/Migrations/         # Numbered SQL files (001_, 002_…), auto-run by DbUp on startup
  Program.cs               # DI registration, middleware pipeline, DbUp runner
  appsettings.json         # DB connection, JWT config, storage config
```

---

## Dialect Design System

All UI uses the Dialect Design System (Inter font, primary blue `#4361EE`, background `#F4F7FE`, navy text `#2B3674`). Use these CSS classes — do NOT reach for raw gray Tailwind classes.

### CSS Utilities (defined in `main.css`)

| Class | Usage |
|-------|-------|
| `card` | White surface with border + shadow (`bg-white rounded-2xl shadow-card border border-[#E9EDF7]`) |
| `section-card` | `card p-6` |
| `form-input` | Text inputs/textareas |
| `form-input.is-error` | Error state (red border/ring) |
| `form-select` | Dropdown with custom chevron arrow |
| `form-select.is-error` | Error state |
| `page-title` | Page H1 (`text-2xl font-bold text-navy`) |
| `page-subtitle` | Subheading under title (`text-sm text-slate-400`) |
| `dialect-table` | Full-width table with Dialect header + row styles |
| `pagination-btn` | Previous/Next buttons for tables |

### Color Tokens

```
Primary:  text-primary / bg-primary / text-primary-700 / bg-primary-700
          (blue-indigo #4361EE, defined as both `primary` and `blue` in Tailwind)

Text:     text-navy (dark #2B3674)  |  text-slate-400 (muted #A3AED0)
Borders:  border-[#E9EDF7]
BG:       bg-page (#F4F7FE)  |  bg-[#F4F7FE]  |  bg-white

Status:
  Success:  text-success / bg-success-light / text-success-dark / border-success/20
  Danger:   text-danger  / bg-danger-light  / text-danger-dark  / border-danger/30
  Warning:  bg-warning-light / text-[#8C6500] / border-warning/30

Shadows:  shadow-card  |  shadow-card-md  |  shadow-card-lg
```

### Page Template Pattern

```vue
<template>
  <div class="space-y-6">
    <div>
      <h1 class="page-title">Page Title</h1>
      <p class="page-subtitle">Description</p>
    </div>

    <BaseAlert v-if="alert" :type="alert.type" :message="alert.message" />

    <div class="section-card">
      <!-- content -->
    </div>

    <!-- Table -->
    <div class="card overflow-hidden">
      <table class="dialect-table">
        <thead><tr><th>Col</th></tr></thead>
        <tbody><tr><td>Data</td></tr></tbody>
      </table>
    </div>
  </div>
</template>
```

### Form Pattern

```vue
<BaseInput v-model="form.name" label="Name" placeholder="..." :required="true" :error="errors.name" />
<input class="form-input" v-model="value" />
<select class="form-select w-full" v-model="selected">...</select>
<input type="checkbox" class="w-4 h-4 rounded border-[#E9EDF7] text-primary focus:ring-primary/30" />
```

---

## Base Components

### `BaseButton`
Props: `variant` (primary | secondary | ghost | danger), `size` (sm | md | lg), `loading`, `disabled`, `type`

### `BaseInput`
Props: `modelValue`, `type`, `label`, `placeholder`, `error`, `required`, `disabled`, `autocomplete`

### `BaseAlert`
Props: `type` (success | error | warning | info), `message`, `dismissible`

### `BaseModal`
Props: `show`, `title` — Emits: `close`

### `StatusBadge`
Props: `status` (draft | submitted | under_review | awarded | rejected)

---

## Auth & Roles

**Roles:** `applicant`, `scorer`, `app_admin`, `school_admin`, `counselor`

Token stored in localStorage as `auth_token`. Axios interceptor attaches it. 401 response → clears token + redirects to `/login`.

Frontend routes use `meta.requiredRole` or `meta.anyRole` — checked in `router/index.js` `beforeEach`.

Backend: `[Authorize(Policy = "app_admin")]` on controllers/actions. `ClaimTypes.NameIdentifier` = userId.

---

## Backend Patterns

### SqlKata Queries

```csharp
// Inject: QueryFactory db
var result = await db.Query("Applications as a")
    .Join("Users as u", "u.Id", "a.ApplicantId")
    .Select("a.Id", "a.Status", "u.FirstName")
    .Where("a.ApplicantId", userId)
    .When(status != null, q => q.Where("a.Status", status))
    .ForPage(page, pageSize)          // 1-indexed
    .GetAsync<ApplicationDto>();

var total = await db.Query("Applications").Where(...).CountAsync<int>();
```

### Controller Pattern

```csharp
[HttpGet]
[Authorize(Policy = "applicant")]
public async Task<IActionResult> GetMine()
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var result = await _service.GetByApplicantAsync(userId);
    return Ok(result);
}
```

### Migrations

Add a new file: `Data/Migrations/00N_description.sql`. DbUp runs all unapplied migrations on startup in filename order.

### Error Handling

Throw exceptions in services — `ExceptionHandlingMiddleware` catches and returns:
```json
{ "error": "message" }
```
With appropriate HTTP status code.

---

## Frontend Store Pattern

```js
// stores/example.js
import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../services/api'

export const useExampleStore = defineStore('example', () => {
  const items = ref([])

  async function fetchItems() {
    const res = await api.get('/items')
    items.value = res.data
  }

  return { items, fetchItems }
})
```

---

## Key API Endpoints

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| POST | `/api/auth/login` | Public | Returns `{ token, userId, role, ... }` |
| POST | `/api/auth/register` | Public | Creates applicant |
| GET | `/api/auth/me` | Bearer | Current user |
| GET | `/api/applications/my` | applicant | Own applications |
| GET | `/api/applications/current` | applicant | Most recent application |
| POST | `/api/applications` | applicant | Create (uses active cycle) |
| GET | `/api/applications/{id}` | owner/scorer/admin | Full application + answers |
| PUT | `/api/applications/{id}/draft` | applicant | `{ answers: [{questionId, textValue?, selectedOptions?}] }` |
| POST | `/api/applications/{id}/submit` | applicant | Submit for review |
| GET | `/api/cycles/{id}/questions` | auth | Questions + options |
| PUT | `/api/cycles/{id}/questions/reorder` | app_admin | `[{id, order}]` |
| GET | `/api/admin/applications` | app_admin | `?status=&page=&pageSize=` → `{data, total, page, pageSize}` |
| PATCH | `/api/admin/applications/{id}/status` | app_admin | `{ status }` |
| POST | `/api/files/upload` | auth | Multipart → `{ fileId }` |
| GET/POST | `/api/references/{code}` | **PUBLIC** | Validate code / upload reference letter |
| GET | `/api/scoring/queue` | scorer | Assigned applications |
| POST | `/api/scoring/{appId}/score` | scorer | `{ scoreValue, comments? }` |

---

## Data Models (Key Fields)

**Users:** Id, Email, PasswordHash, FirstName, LastName, Role, SchoolId (nullable, for counselors)

**ScholarshipCycles:** Id, Name, OpenDate, CloseDate, IsActive

**Sections:** Id, CycleId, Title, Description, Order

**Questions:** Id, CycleId, SectionId (nullable), Text, Description, Type, Order, IsRequired, ValidationRules (JSON)
- Types: `short_answer`, `long_answer`, `multiple_choice`, `single_choice`, `file_upload`, `school_select`, `date`
- ValidationRules JSON: `{ numberOnly, phoneFormat, emailFormat, minLength, maxLength }`

**Applications:** Id, ApplicantId, CycleId, Status (`draft`→`submitted`→`under_review`→`awarded`/`rejected`), SubmittedAt, CreatedAt

**ApplicationAnswers:** ApplicationId, QuestionId, TextValue (nullable), SelectedOptions (JSON array of option IDs)

**References:** Id, ApplicationId, Code (SHA-256 hash), Label, Status (`pending`/`received`), ExpiresAt
- Plaintext code returned once to applicant; lookup hashes incoming code

---

## Gotchas

- **No EF** — raw SqlKata + Dapper. No change tracking. Write explicit UPDATE queries.
- **File upload is two-step:** upload → get `fileId` → store in answer as string. Files live in `ApplicationFiles` with `ApplicationId = "pending"` until submit.
- **Reference codes are hashed** — store SHA-256, compare hash on lookup.
- **Both frontend routing AND backend authorization enforce roles** — check both when adding features.
- **Application status transitions** are manual — no state machine enforcement. Admin can set any valid status.
- **Counselors are scoped to SchoolId** — set at creation, no UI to change it. Filter applicant lists by school.
- **Sections are optional** — questions with `SectionId = null` are "ungrouped." The form groups them at the end.
- **Dialect tokens only** — never use `text-gray-*`, `bg-gray-*`, `border-gray-*`, `text-blue-*` directly. Use `text-navy`, `text-slate-400`, `border-[#E9EDF7]`, `text-primary`.
