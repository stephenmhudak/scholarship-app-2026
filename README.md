# Scholarship App

A web app for running scholarship programs. Students apply online, ask teachers or counselors for references, and get their award letters. Schools follow their students' progress, scorers review and score applications, and administrators set up each application period and decide the awards.

- **Frontend:** Vue 3, Vuetify, Vite
- **Backend:** ASP.NET Core 10 API, MySQL 8
- **Files:** stored on the server's disk, Cloudflare R2 or Azure Blob Storage
- **Email:** sent through [Resend](https://resend.com)

---

## System requirements

### Deploying with nginx (Ubuntu only)

The app runs straight on the server: nginx serves the site, the API runs as a system service and MySQL is installed alongside them.

| | |
|---|---|
| OS | Ubuntu Server, 22.04 LTS or newer |
| Hardware | 2 CPU cores, 2 GB RAM (the app is built on the server), 10 GB free disk |
| Access | A user with `sudo` and internet access (packages are downloaded during setup) |
| Network | Ports 80 and 443 open. For HTTPS you also need a domain name whose DNS points at the server |

The setup script installs everything else: nginx, MySQL 8, Node.js 24, the .NET 10 SDK and certbot.

### Deploying with Docker (Ubuntu, macOS or Windows)

The app runs as four containers (Caddy, the website, the API and MySQL), all based on Linux images.

| Host | Requirement |
|---|---|
| Ubuntu | 22.04 LTS or newer. The script installs Docker Engine if it's missing |
| macOS | Docker Desktop. The script can install it with Homebrew |
| Windows | Windows 10 or 11 with WSL 2 and Docker Desktop. The script can install Docker Desktop with winget |
| Hardware | 2 CPU cores, 4 GB RAM (Docker Desktop needs the extra), 10 GB free disk |
| Network | For HTTPS: ports 80 and 443 reachable from the internet, plus a domain name pointing at the host. Without a domain the site is served over plain HTTP, which is fine for local testing |

### Optional services

- **Resend:** an API key and a verified sending domain. Without them no email is sent; messages are only written to the API log. That includes email verification, which every self-registered user needs before they can sign in, so production needs Resend.
- **Cloudflare R2 or Azure Blob Storage:** an alternative to keeping uploaded files on the server's disk.

### Local development

Node.js 20 or newer, the .NET 10 SDK and a MySQL 8 server.

---

## Setup

Clone the repository onto the machine that will run the app, then run the setup script from the repository folder.

```bash
git clone <repository-url> scholarship-app-2026
cd scholarship-app-2026
```

### Ubuntu with nginx

```bash
deploy/deploy.sh nginx
```

The script asks for your `sudo` password, then:

1. Installs nginx, MySQL, Node.js, .NET and certbot.
2. Creates the database and a dedicated MySQL user.
3. Builds the website and the API.
4. Starts the API as the `scholarship-api` service, which restarts automatically and runs at boot.
5. Configures nginx and, if you chose HTTPS, gets a Let's Encrypt certificate that renews automatically.

### Docker on Ubuntu or macOS

```bash
deploy/deploy.sh docker
```

### Docker on Windows

Open PowerShell in the repository folder and run:

```powershell
powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1
```

The Docker scripts check for Docker (and offer to install it), write the settings files, set up MySQL and start the containers. With a domain name, Caddy gets an HTTPS certificate automatically on the first visit.

### What the script asks

Every prompt shows its default in brackets. For keys and passwords, pressing Enter generates a strong random value. Secrets are typed without being shown on screen.

| Question | Notes |
|---|---|
| Testing or production | **Production:** the MySQL `root` account only works from the server itself, and MySQL can't be reached from other machines. **Testing:** `root` can sign in from any computer, which is handy for inspecting data. Don't use testing on a server exposed to the internet |
| Domain name | Leave it blank to serve plain HTTP on any address, for example on a local machine |
| HTTPS certificate | Let's Encrypt. Needs the domain's DNS pointing at the server and ports 80/443 open |
| Site URL | The address used in emailed links (verification, invites, reference requests) |
| MySQL passwords | The app's own database user and, when needed, `root` |
| JWT signing key | Signs sign-in sessions. Changing it signs everyone out |
| Two-factor encryption key | Protects users' authenticator secrets. **Keep it safe and never change it**: if it's lost or changed, everyone who set up two-factor needs it reset by an admin |
| Email | Sender address and Resend API key |
| File storage | Server disk, Cloudflare R2 or Azure Blob Storage |
| Super admin account | Email, name and password for the first administrator (see below). On later runs this defaults to skipping, or you can update the account |

The script shows a summary and asks before it changes anything.

### The first administrator

Nobody can register as an administrator, so the setup script asks for a **super admin** account: email, first and last name, and a password (typed twice, not shown). Once the app is running, the script creates the account, already verified. If an account with that email already exists, for example one you registered earlier, it's made a super admin instead, and any field you leave blank keeps its current value.

Sign in with it, then create every other account from **Users & Schools**.

To add or reset a super admin later, run the script again and answer yes when it asks. The password is passed to the API privately and isn't saved anywhere; only the email is remembered as the default for next time.

### Updating

Pull the latest code and run the same script again. Your previous answers and secrets are the defaults, so pressing Enter through the prompts redeploys with the same settings. Database changes are applied automatically when the API starts.

```bash
git pull
deploy/deploy.sh nginx      # or: deploy/deploy.sh docker, or deploy\deploy.ps1 on Windows
```

### Where things are

| | nginx | Docker |
|---|---|---|
| Settings and secrets | `/etc/scholarship-app/api.env` (readable by root only) | `.env` and `secrets/*.env` in the repository folder (not committed to git) |
| Uploaded files (disk storage) | `/var/lib/scholarship-app/uploads` | the `uploads_data` volume |
| Database | MySQL on the server | the `mysql_data` volume |
| API logs | `journalctl -u scholarship-api -f` | `docker compose logs -f backend` |
| Status | `systemctl status scholarship-api` | `docker compose ps` |

**Back up** the database, the uploaded files (when stored on disk) and the settings files. Losing the two-factor encryption key means every enrolled user has to reset two-factor.

### Local development

1. Point `ConnectionStrings:Default` in `backend/ScholarshipApi/appsettings.Development.json` at a MySQL 8 server. The database is created and migrated on first start.
2. Start the API and the frontend:

   ```bash
   cd backend/ScholarshipApi && dotnet run   # API on http://localhost:5098
   cd frontend && npm install && npm run dev # site on http://localhost:5173
   ```

The frontend's dev server forwards `/api` requests to the API. Without a Resend key, emails (including verification links) are printed in the API's console.

---

## Roles

Every account has one role. The navigation only shows the pages that role can use, and the API enforces the same rules.

| Role | Who it's for | How the account is created |
|---|---|---|
| Applicant | Students applying for scholarships | Registers on the site |
| Reference Provider | Teachers, coaches, employers writing references | Registers on the site through a reference link |
| Counselor | School counselors | Invited by their school's admin, or by an app admin |
| School Admin | Principals and other school leads | Registers through an invite link from an app admin |
| Scorer | Reviewers who score applications | Created by an app admin |
| App Admin | Staff who run the scholarship program | Created by an app admin |
| Super Admin | Technical owners of the system | Promoted in MySQL (first one) or created by another super admin |

### Applicant

- Starts an application for any open application period: one application per period.
- Answers the questions, uploads documents and saves drafts until the deadline. Answers can be copied from an earlier period's application.
- Creates a reference link for each reference the application needs and shares it with the person writing it. The applicant sees whether each reference has been submitted, but never sees what it says.
- Submits the application once every required question and reference is complete.
- Follows the application's status and, once awarded, sees the scholarships won and downloads the award letters.

### Reference Provider

- Opens the link (or enters the code) an applicant shared, then signs in or registers.
- Fills in the reference form, types their name and draws a signature.
- Sees the references they've submitted on **My References**.
- Can't write a reference for themselves.

### Counselor

- Sees the students at their school on **Students**: everyone who chose that school on their application, with each application's status and last activity.
- Emails a student a reminder to finish their application.
- Writes references for students, just like a Reference Provider.
- Only sees their own school. The school is set when the account is created and can't be changed by the counselor.

### School Admin

Everything a counselor can do, plus:

- Edits the school's details on **My School**.
- Invites counselors by email. Each counselor sets their own password from the invite link.
- Resends counselor invites, resets counselors' passwords and two-factor, and removes counselors.

### Scorer

- Works through their assigned applications on **Scoring Queue**.
- Reads each application, its uploaded files and its submitted references.
- Gives a score from 1 to 100 for each section of the application and for each reference, with optional comments.
- Only sees applications once they're submitted, or once the application period has ended. An admin can limit a scorer to applications that match certain answers (for example one school or one program).

### App Admin

Runs the scholarship program:

- **Application Periods:** creates each period with its open and close dates, and builds its application form:
  - sections and questions, including questions that only appear depending on earlier answers
  - reference forms and how many of each are required
  - scholarships, with their eligibility rules and who awards them
  - award letter templates
- **Applications:** browses, searches and filters every application, exports them to CSV, changes their status and sees every score with its comments.
- **Scoring Progress:** follows how far scoring has got across all applications.
- **Users & Schools:**
  - creates and edits accounts, deactivates and reactivates them
  - resets passwords and two-factor
  - sets which applications a scorer can see
  - edits schools and creates the invite links that let school admins register their school

### Super Admin

Everything an app admin can do, plus:

- Has every permission, including any added in the future.
- Awards any scholarship.
- Creates and manages other super admins. No other role can.
- **Impersonates** another user (not another super admin) to see the app exactly as they do, for support. Impersonation is logged, and the header shows an **End impersonation** button.

### Scholarship awarders

Awarding isn't a role. An app admin assigns specific accounts to each scholarship. Those accounts see **Award Scholarships**, where they can:

- review the eligible applications with their average scores
- award the scholarship, with an optional amount and note
- download the award letters

Super admins can award every scholarship.

### Every account

Anyone signed in can:

- change their name and password
- turn on two-factor sign-in, using an authenticator app or emailed codes
- replay the guided tour of the current page from the help button in the header
