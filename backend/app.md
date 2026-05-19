# Scholarship Application — Backend Scaffold

## Tech Stack

| Tool | Purpose |
|------|---------|
| ASP.NET Core 10 Web API | HTTP API framework |
| Entity Framework Core 10 | ORM + migrations |
| SQL Server / PostgreSQL | Relational database |
| JWT Bearer | Authentication tokens |
| ASP.NET Core Authorization | Role-based access policies |
| BCrypt.Net-Next | Password hashing |
| Azure Blob Storage *(or local disk)* | File storage for uploads |

---

## User Roles

| Role Constant | Description |
|---------------|-------------|
| `applicant` | Students who submit applications |
| `scorer` | Staff who score submitted applications |
| `app_admin` | Manages application lifecycle and settings |
| `school_admin` | Manages schools and counselor accounts |
| `counselor` | Views and supports their school's applicants |

Roles are stored as a string column on the `Users` table and enforced via ASP.NET Core authorization policies.

---

## Project Structure

```
ScholarshipApi/
├── Controllers/
│   ├── AuthController.cs
│   ├── ApplicationsController.cs
│   ├── QuestionsController.cs
│   ├── FilesController.cs
│   ├── ScoringController.cs
│   ├── AdminController.cs
│   ├── SchoolsController.cs
│   ├── CounselorController.cs
│   └── ReferencesController.cs
├── Models/
│   ├── User.cs
│   ├── School.cs
│   ├── ScholarshipCycle.cs
│   ├── Question.cs
│   ├── QuestionOption.cs
│   ├── Application.cs
│   ├── ApplicationAnswer.cs
│   ├── ApplicationFile.cs
│   ├── Score.cs
│   ├── Reference.cs
│   └── ReferenceDocument.cs
├── DTOs/
│   ├── Auth/
│   │   ├── LoginRequest.cs
│   │   ├── RegisterRequest.cs
│   │   └── AuthResponse.cs
│   ├── Application/
│   │   ├── ApplicationDto.cs
│   │   ├── AnswerDto.cs
│   │   └── SubmitApplicationRequest.cs
│   ├── Scoring/
│   │   ├── ScoreDto.cs
│   │   └── SubmitScoreRequest.cs
│   └── Reference/
│       ├── ReferenceDto.cs
│       └── ReferenceUploadResponse.cs
├── Services/
│   ├── IAuthService.cs / AuthService.cs
│   ├── IApplicationService.cs / ApplicationService.cs
│   ├── IScoringService.cs / ScoringService.cs
│   ├── IFileStorageService.cs / FileStorageService.cs
│   └── IReferenceService.cs / ReferenceService.cs
├── Data/
│   ├── AppDbContext.cs
│   └── Migrations/
├── Authorization/
│   └── RolePolicies.cs
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs
├── Program.cs
└── appsettings.json
```

---

## Database Schema

### `Users`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `Email` | `nvarchar(255)` | Unique |
| `PasswordHash` | `nvarchar(255)` | BCrypt |
| `FirstName` | `nvarchar(100)` | |
| `LastName` | `nvarchar(100)` | |
| `Role` | `nvarchar(50)` | `applicant` / `scorer` / `app_admin` / `school_admin` / `counselor` |
| `SchoolId` | `Guid?` | FK → `Schools` (counselors only) |
| `CreatedAt` | `datetime2` | |

### `Schools`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `Name` | `nvarchar(255)` | |
| `Address` | `nvarchar(500)` | |
| `CreatedAt` | `datetime2` | |

### `ScholarshipCycles`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `Name` | `nvarchar(255)` | e.g. "2026 Spring Scholarship" |
| `OpenDate` | `datetime2` | |
| `CloseDate` | `datetime2` | |
| `IsActive` | `bit` | |

### `Questions`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `CycleId` | `Guid` | FK → `ScholarshipCycles` |
| `Text` | `nvarchar(max)` | |
| `Type` | `nvarchar(50)` | `short_answer` / `long_answer` / `multiple_choice` / `single_choice` / `file_upload` |
| `Order` | `int` | Display order |
| `IsRequired` | `bit` | |

### `QuestionOptions`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `QuestionId` | `Guid` | FK → `Questions` |
| `Text` | `nvarchar(500)` | |
| `Order` | `int` | |

### `Applications`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ApplicantId` | `Guid` | FK → `Users` |
| `CycleId` | `Guid` | FK → `ScholarshipCycles` |
| `Status` | `nvarchar(50)` | `draft` / `submitted` / `under_review` / `awarded` / `rejected` |
| `SubmittedAt` | `datetime2?` | |
| `CreatedAt` | `datetime2` | |

### `ApplicationAnswers`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ApplicationId` | `Guid` | FK → `Applications` |
| `QuestionId` | `Guid` | FK → `Questions` |
| `TextValue` | `nvarchar(max)?` | Short/long answer |
| `SelectedOptions` | `nvarchar(max)?` | JSON array of `QuestionOption` IDs for MC/SC |

### `ApplicationFiles`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ApplicationId` | `Guid` | FK → `Applications` |
| `QuestionId` | `Guid?` | FK → `Questions` (for file upload questions) |
| `FileName` | `nvarchar(255)` | Original filename |
| `StoragePath` | `nvarchar(1000)` | Blob path or local path |
| `UploadedAt` | `datetime2` | |

### `Scores`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ApplicationId` | `Guid` | FK → `Applications` |
| `ScoredById` | `Guid` | FK → `Users` (scorer) |
| `Score` | `decimal(5,2)` | |
| `Comments` | `nvarchar(max)?` | |
| `ScoredAt` | `datetime2` | |

### `References`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ApplicationId` | `Guid` | FK → `Applications` |
| `Code` | `nvarchar(32)` | Unique, cryptographically random |
| `Label` | `nvarchar(255)?` | e.g. "Math Teacher" |
| `Status` | `nvarchar(50)` | `pending` / `received` |
| `ExpiresAt` | `datetime2` | Matches cycle close date |
| `CreatedAt` | `datetime2` | |

### `ReferenceDocuments`
| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ReferenceId` | `Guid` | FK → `References` |
| `FileName` | `nvarchar(255)` | Original filename |
| `StoragePath` | `nvarchar(1000)` | |
| `UploadedAt` | `datetime2` | |

---

## API Endpoints

### Auth — `/api/auth`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/login` | Public | Returns JWT |
| `POST` | `/register` | Public | Creates applicant account |
| `GET` | `/me` | Any | Returns current user profile |

### Applications — `/api/applications`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/my` | `applicant` | List own applications |
| `POST` | `/` | `applicant` | Create new application for active cycle |
| `GET` | `/{id}` | Owner / scorer / app_admin | Get application detail + answers |
| `PUT` | `/{id}/draft` | `applicant` (owner) | Save draft answers |
| `POST` | `/{id}/submit` | `applicant` (owner) | Submit application |
| `GET` | `/{id}/references` | Owner / app_admin | List references and their status |
| `POST` | `/{id}/references` | `applicant` (owner) | Generate a new reference slot + code |

### Questions — `/api/cycles/{cycleId}/questions`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` | Any authenticated | Get all questions for a cycle |
| `POST` | `/` | `app_admin` | Add a question |
| `PUT` | `/{questionId}` | `app_admin` | Update a question |
| `DELETE` | `/{questionId}` | `app_admin` | Delete a question |
| `PUT` | `/reorder` | `app_admin` | Reorder questions |

### File Uploads — `/api/files`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/upload` | Any authenticated | Upload a file; returns `{ fileId }` |
| `GET` | `/{fileId}` | Any authenticated | Stream/download a file |

> Accept: `multipart/form-data`. Validate MIME type and extension server-side. Enforce a max file size (e.g. 10 MB) via `RequestSizeLimitAttribute`.

### Scoring — `/api/scoring`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/queue` | `scorer` | List applications assigned to the current scorer |
| `GET` | `/{applicationId}` | `scorer` | Get application detail for scoring |
| `POST` | `/{applicationId}/score` | `scorer` | Submit a score and comments |

### Admin — `/api/admin`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/applications` | `app_admin` | Paginated list with filters/sort |
| `PATCH` | `/applications/{id}/status` | `app_admin` | Change application status |
| `POST` | `/applications/{id}/assign` | `app_admin` | Assign to a scorer |
| `GET` | `/applications/export` | `app_admin` | CSV/Excel export |
| `GET` | `/cycles` | `app_admin` | List scholarship cycles |
| `POST` | `/cycles` | `app_admin` | Create a cycle |
| `PUT` | `/cycles/{id}` | `app_admin` | Update a cycle |

### Schools — `/api/schools`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` | `school_admin` | List all schools |
| `POST` | `/` | `school_admin` | Create a school |
| `GET` | `/{id}` | `school_admin` | Get school detail |
| `PUT` | `/{id}` | `school_admin` | Update a school |
| `DELETE` | `/{id}` | `school_admin` | Delete a school |
| `GET` | `/{id}/applicants` | `school_admin` / `counselor` | List applicants from this school |
| `POST` | `/{id}/counselors/invite` | `school_admin` | Invite a counselor (sends email) |

### Counselor — `/api/counselor`
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/applicants` | `counselor` | List applicants at the counselor's school |
| `POST` | `/nudge/{applicantId}` | `counselor` | Send a reminder email to an applicant |

### References — `/api/references` *(public)*
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/{code}` | **Public** | Validate a reference code; returns applicant name + label |
| `POST` | `/{code}/upload` | **Public** | Upload a reference document (`.doc`, `.docx`, `.pdf`) |

> `POST /{code}/upload` validates: code exists, not expired, status is `pending`, file extension is `.doc`/`.docx`/`.pdf`. On success, marks the reference `received` and stores the file.

---

## Authorization Policies (`Authorization/RolePolicies.cs`)

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Applicant",   p => p.RequireRole("applicant"));
    options.AddPolicy("Scorer",      p => p.RequireRole("scorer"));
    options.AddPolicy("AppAdmin",    p => p.RequireRole("app_admin"));
    options.AddPolicy("SchoolAdmin", p => p.RequireRole("school_admin"));
    options.AddPolicy("Counselor",   p => p.RequireRole("counselor"));
    options.AddPolicy("Staff",       p => p.RequireRole("scorer", "app_admin", "school_admin", "counselor"));
});
```

---

## File Upload Rules

- **Application question files** (`/api/files/upload`): any file type allowed by `app_admin` config; referenced by `fileId` in the answers JSON payload.
- **Reference documents** (`/api/references/{code}/upload`): `.doc`, `.docx`, `.pdf` only. Validated by extension **and** MIME type.
- All files are stored via `IFileStorageService` — swap between local disk and Azure Blob Storage without changing controllers.
- Filenames are never stored as-is; a UUID key is used for the storage path to prevent path traversal.

---

## Reference Code Generation

```csharp
// ReferenceService.cs
private static string GenerateCode()
{
    var bytes = RandomNumberGenerator.GetBytes(16);
    return Convert.ToHexString(bytes).ToLower(); // 32-char hex string
}
```

Codes are stored hashed (SHA-256) in the database; the plaintext is returned to the applicant once. Lookup hashes the incoming code and compares.

---

## `Program.cs` Outline

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* configure signing key, issuer, audience */ });

builder.Services.AddAuthorization(RolePolicies.Configure);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IReferenceService, ReferenceService>();
builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>(); // or LocalFileStorageService

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", p => p.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## `appsettings.json`

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=ScholarshipDb;Trusted_Connection=True;"
  },
  "Jwt": {
    "Key": "REPLACE_WITH_SECRET_KEY_MIN_32_CHARS",
    "Issuer": "ScholarshipApi",
    "Audience": "ScholarshipApp",
    "ExpiresInMinutes": 480
  },
  "Storage": {
    "Provider": "AzureBlob",
    "AzureBlobConnectionString": "",
    "ContainerName": "scholarship-files",
    "LocalPath": "wwwroot/uploads"
  },
  "FileUpload": {
    "MaxFileSizeBytes": 10485760
  }
}
```

---

## Setup

```bash
dotnet new webapi -n ScholarshipApi
cd ScholarshipApi

# ORM
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools

# Auth
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

# Password hashing
dotnet add package BCrypt.Net-Next

# File storage (optional Azure)
dotnet add package Azure.Storage.Blobs

# Run initial migration
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## Notes

- The `/api/references/{code}` endpoints have no `[Authorize]` attribute — they are intentionally public so references can upload without an account.
- JWT tokens carry the user's `Id` and `Role` as claims; the `Role` claim is what ASP.NET Core policies evaluate.
- All file storage paths go through `IFileStorageService` so the implementation (local vs. Azure) can be swapped via `appsettings.json` without changing controllers.
- The `counselor`'s school association (`SchoolId` on `Users`) is set at account creation by the `school_admin`; counselors can only see applicants whose `SchoolId` matches their own.
- Paginated list endpoints (`/api/admin/applications`, `/api/counselor/applicants`) should accept `?page=`, `?pageSize=`, and `?search=` query params to support DataTables server-side mode.
