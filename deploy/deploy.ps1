<#
.SYNOPSIS
    Deploys the Scholarship App with Docker Compose on Windows (Docker Desktop).

.DESCRIPTION
    The Windows counterpart of deploy/deploy.sh's docker mode: installs Docker Desktop if needed (winget), asks for
    the settings and secrets, writes .env and secrets/*.env, configures MySQL and starts docker-compose.yml.
    Re-run it after pulling to redeploy; answers and secrets from the previous run are offered as defaults.

    Run from PowerShell in the repo:  powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1
#>
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
# Text piped to docker (SQL, the super admin's details) goes as UTF-8, so names with accents survive
$OutputEncoding = New-Object Text.UTF8Encoding $false

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Secrets = Join-Path $RepoRoot 'secrets'
$DotEnv = Join-Path $RepoRoot '.env'
$DbName = 'scholarship_db'
$DbUser = 'scholarship'

# ---------------------------------------------------------------------------------------------------------------------
# Output and prompts
# ---------------------------------------------------------------------------------------------------------------------

function Info($msg) { Write-Host ''; Write-Host "==> $msg" -ForegroundColor Cyan }
function Note($msg) { Write-Host "    $msg" }
function Warn($msg) { Write-Host "WARNING: $msg" -ForegroundColor Yellow }
function Fail($msg) { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# Repeats until $Check (a scriptblock returning an error message, or nothing) accepts the answer
function Ask([string]$Question, [string]$Default = '', [scriptblock]$Check = $null) {
    while ($true) {
        $prompt = if ($Default) { "$Question [$Default]" } else { $Question }
        $answer = Read-Host $prompt
        if (-not $answer) { $answer = $Default }
        if ($Check) { $err = & $Check $answer; if ($err) { Note $err; continue } }
        return $answer
    }
}

function AskYesNo([string]$Question, [bool]$Default) {
    $hint = if ($Default) { '[Y/n]' } else { '[y/N]' }
    while ($true) {
        $a = Read-Host "$Question $hint"
        if (-not $a) { return $Default }
        if ($a -match '^(y|yes)$') { return $true }
        if ($a -match '^(n|no)$') { return $false }
    }
}

# $Options: ordered list of @(value, description)
function AskChoice([string]$Question, [string]$Default, [object[]]$Options) {
    Write-Host $Question
    $defaultNum = 1
    for ($i = 0; $i -lt $Options.Count; $i++) {
        Write-Host ("  {0}) {1}" -f ($i + 1), $Options[$i][1])
        if ($Options[$i][0] -eq $Default) { $defaultNum = $i + 1 }
    }
    while ($true) {
        $a = Read-Host "Choose 1-$($Options.Count) [$defaultNum]"
        if (-not $a) { $a = "$defaultNum" }
        $n = 0
        if ([int]::TryParse($a, [ref]$n) -and $n -ge 1 -and $n -le $Options.Count) { return $Options[$n - 1][0] }
    }
}

# Hidden input. Enter keeps $Current; with none, 'generate' calls $Generator and 'optional' returns ''.
# Typing "new" generates a fresh value ('generate'); typing "none" clears it ('optional').
function AskSecret([string]$Question, [string]$Current, [string]$Mode, [scriptblock]$Check, [scriptblock]$Generator = $null) {
    if ($Current) {
        $hint = 'Enter keeps the current value'
        if ($Mode -eq 'generate') { $hint += ', "new" generates another' }
        if ($Mode -eq 'optional') { $hint += ', "none" clears it' }
    } elseif ($Mode -eq 'generate') { $hint = 'Enter generates one' }
    elseif ($Mode -eq 'optional') { $hint = 'Enter to skip' }
    else { $hint = 'required' }
    while ($true) {
        $secure = Read-Host "$Question (hidden; $hint)" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
        try { $a = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
        if ($Mode -eq 'generate' -and $a -eq 'new') { $a = & $Generator }
        elseif ($Mode -eq 'optional' -and $a -eq 'none') { return '' }
        elseif (-not $a) {
            if ($Current) { $a = $Current }
            elseif ($Mode -eq 'generate') { $a = & $Generator }
            elseif ($Mode -eq 'optional') { return '' }
            else { Note 'A value is required.'; continue }
        }
        $err = & $Check $a
        if ($err) { Note $err; continue }
        return $a
    }
}

# ---------------------------------------------------------------------------------------------------------------------
# Validation and generation (same rules as deploy.sh)
# ---------------------------------------------------------------------------------------------------------------------

# Values end up in env files read by Docker Compose, which expands "$"
$CheckEnvValue = {
    param($v)
    if ($v -match '[$\\"'']') { return "Can't contain `$, quotes or backslashes." }
    if ($v -match '^\s|\s$') { return "Can't start or end with a space." }
}
$CheckRequired = { param($v) if (-not $v) { return 'A value is required.' }; & $CheckEnvValue $v }
$CheckOptional = { param($v) & $CheckEnvValue $v }
$CheckDbPassword = {
    param($v)
    if ($v -cnotmatch '^[A-Za-z0-9!@%^*_+=.,:~-]{12,}$') {
        return 'Use 12+ characters: letters, digits and ! @ % ^ * _ + = . , : ~ - (no quotes, spaces, $ or ;).'
    }
}
$CheckJwtKey = {
    param($v)
    $e = & $CheckEnvValue $v; if ($e) { return $e }
    if ([Text.Encoding]::UTF8.GetByteCount($v) -lt 32) { return 'Must be at least 32 bytes.' }
    if ($v.StartsWith('REPLACE_')) { return 'Replace the placeholder with a real key.' }
}
$CheckTwoFactorKey = {
    param($v)
    try { if ([Convert]::FromBase64String($v).Length -eq 32) { return } } catch { }
    return 'Must be base64 for exactly 32 bytes.'
}
$CheckEmail = { param($v) if ($v -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { return 'Enter an email address.' }; & $CheckEnvValue $v }
$CheckDomain = {
    param($v)
    if ($v -and $v -notmatch '^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$') {
        return 'Enter a host name only, e.g. scholarships.example.org (no http://, no path).'
    }
}
$CheckUrl = {
    param($v)
    if ($v -notmatch '^https?://') { return 'Start with http:// or https://.' }
    if ($v.EndsWith('/')) { return 'Leave off the trailing slash.' }
    & $CheckEnvValue $v
}
$CheckAdminPassword = {
    param($v)
    if ($v.Length -lt 8 -or [Text.Encoding]::UTF8.GetByteCount($v) -gt 72) { return 'Use 8 to 72 characters.' }
}
$CheckName = { param($v) if (-not $v -or $v.Length -gt 100) { return 'Enter 1 to 100 characters.' } }
$CheckNameOrBlank = { param($v) if ($v.Length -gt 100) { return 'Use at most 100 characters.' } }
$CheckPort = { param($v) $n = 0; if (-not ([int]::TryParse($v, [ref]$n) -and $n -ge 1 -and $n -le 65535)) { return 'Enter a port number.' } }

function RandomBase64([int]$Bytes) {
    $b = New-Object byte[] $Bytes
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($b) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($b)
}
$GenJwtKey = { RandomBase64 48 }
$GenTwoFactorKey = { RandomBase64 32 }
$GenPassword = { ((RandomBase64 48) -replace '[^A-Za-z0-9]', '').Substring(0, 32) }

# The value of KEY=... in an env file, or ''
function Get-EnvValue([string]$Path, [string]$Key) {
    if (-not (Test-Path $Path)) { return '' }
    $value = ''
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        if ($line.StartsWith("$Key=")) { $value = $line.Substring($Key.Length + 1).TrimEnd("`r") }
    }
    return $value
}

function Get-ConnPassword([string]$Conn) {
    if ($Conn -match 'Password=([^;]*)') { return $Matches[1] }
    return ''
}

# LF endings, UTF-8 without BOM: what Docker Compose expects in env files
function Write-EnvFile([string]$Path, [string[]]$Lines) {
    [IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"), (New-Object Text.UTF8Encoding $false))
}

# Runs a native command; returns $true when it exits 0. Output is discarded unless -Show.
function Try-Native([scriptblock]$Block, [switch]$Show) {
    $old = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($Show) { & $Block | Out-Host } else { & $Block *> $null }
        return ($LASTEXITCODE -eq 0)
    } finally { $ErrorActionPreference = $old }
}

function Compose {
    Push-Location $RepoRoot
    try {
        & docker compose @args
        if ($LASTEXITCODE -ne 0) { throw "docker compose $($args -join ' ') failed (exit $LASTEXITCODE)." }
    } finally { Pop-Location }
}

# ---------------------------------------------------------------------------------------------------------------------
# Docker
# ---------------------------------------------------------------------------------------------------------------------

function Ensure-Docker {
    Info 'Checking Docker'
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
            Fail 'Install Docker Desktop (https://www.docker.com/products/docker-desktop/), start it, then re-run this script.'
        }
        if (-not (AskYesNo "Docker isn't installed. Install Docker Desktop with winget?" $true)) {
            Fail 'Install Docker Desktop, start it, then re-run this script.'
        }
        & winget install -e --id Docker.DockerDesktop --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) { Fail 'winget could not install Docker Desktop.' }
        Note 'Docker Desktop is installed. It may ask you to sign out or restart (WSL 2 setup).'
        Fail 'Start Docker Desktop, finish its setup, then re-run this script in a new PowerShell window.'
    }
    if (-not (Try-Native { docker info })) {
        $exe = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (Test-Path $exe) {
            Note 'Starting Docker Desktop...'
            Start-Process $exe
            for ($i = 0; $i -lt 60 -and -not (Try-Native { docker info }); $i++) { Start-Sleep -Seconds 3 }
        }
        if (-not (Try-Native { docker info })) { Fail "Docker isn't running. Start Docker Desktop, then re-run." }
    }
    if (-not (Try-Native { docker compose version })) { Fail 'Docker Compose v2 (docker compose) is required.' }
    Note (& docker --version)
}

function Load-Existing {
    $s = $Secrets
    $script:Ex = @{
        DeployEnv       = Get-EnvValue $DotEnv 'DEPLOY_ENV'
        SiteAddress     = Get-EnvValue $DotEnv 'SITE_ADDRESS'
        AcmeEmail       = Get-EnvValue $DotEnv 'ACME_EMAIL'
        AppBaseUrl      = Get-EnvValue $DotEnv 'APP_BASE_URL'
        HttpPort        = Get-EnvValue $DotEnv 'HTTP_PORT'
        HttpsPort       = Get-EnvValue $DotEnv 'HTTPS_PORT'
        MysqlPort       = Get-EnvValue $DotEnv 'MYSQL_PORT'
        SuperAdminEmail = Get-EnvValue $DotEnv 'SUPER_ADMIN_EMAIL'
        Jwt             = Get-EnvValue "$s\jwt.env" 'Jwt__Key'
        TwoFactor       = Get-EnvValue "$s\twofactor.env" 'TwoFactor__EncryptionKey'
        EmailFrom       = Get-EnvValue "$s\email.env" 'Email__From'
        Resend          = Get-EnvValue "$s\email.env" 'Email__Resend__ApiKey'
        DbRootPassword  = Get-EnvValue "$s\mysql.env" 'MYSQL_ROOT_PASSWORD'
        DbAppPassword   = ''
    }
    # Older setups kept R2 settings in r2.env; they move into storage.env
    $storage = "$s\storage.env"
    if (-not (Test-Path $storage)) { $storage = "$s\r2.env" }
    $Ex.StorageProvider = Get-EnvValue $storage 'Storage__Provider'
    $Ex.R2AccountId = Get-EnvValue $storage 'Storage__R2__AccountId'
    $Ex.R2AccessKeyId = Get-EnvValue $storage 'Storage__R2__AccessKeyId'
    $Ex.R2Secret = Get-EnvValue $storage 'Storage__R2__SecretAccessKey'
    $Ex.R2Bucket = Get-EnvValue $storage 'Storage__R2__BucketName'
    $Ex.AzureConnection = Get-EnvValue $storage 'Storage__AzureBlobConnectionString'
    $Ex.AzureContainer = Get-EnvValue $storage 'Storage__ContainerName'
    $conn = Get-EnvValue "$s\db.env" 'ConnectionStrings__Default'
    if ($conn -like "*User=$DbUser;*") { $Ex.DbAppPassword = Get-ConnPassword $conn }
}

function Read-Hidden([string]$Prompt) {
    $secure = Read-Host $Prompt -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# Nobody can register as a super admin, so the first one is made here; the API creates it (or promotes an existing
# account) after migrations. Returns $null when skipped.
function Ask-SuperAdmin {
    Info 'Super admin account'
    Note 'A super admin can do everything, including creating the other admin, scorer and staff accounts.'
    if ($Ex.SuperAdminEmail) { Note "Set up on a previous run: $($Ex.SuperAdminEmail)" }
    if (-not (AskYesNo 'Create or update a super admin account now?' (-not $Ex.SuperAdminEmail))) { return $null }

    $sa = @{ Email = Ask 'Super admin email' $Ex.SuperAdminEmail $CheckEmail }
    Note 'If an account with this email already exists, it''s made a super admin.'
    $keep = $sa.Email -eq $Ex.SuperAdminEmail
    $nameCheck = $CheckName
    if ($keep) { Note 'Leave a field blank to keep the account''s current value.'; $nameCheck = $CheckNameOrBlank }
    $sa.FirstName = Ask 'First name' '' $nameCheck
    $sa.LastName = Ask 'Last name' '' $nameCheck
    $hint = if ($keep) { '8+ characters, Enter keeps the current one' } else { '8+ characters' }
    while ($true) {
        $sa.Password = Read-Hidden "Password (hidden; $hint)"
        if (-not $sa.Password) { if ($keep) { break }; Note 'A password is required.'; continue }
        $err = & $CheckAdminPassword $sa.Password
        if ($err) { Note $err; continue }
        if ((Read-Hidden 'Confirm the password') -ceq $sa.Password) { break }
        Note 'The passwords don''t match.'
    }
    return $sa
}

function Ask-Settings {
    $c = @{}

    Info 'Environment'
    $default = if ($Ex.DeployEnv) { $Ex.DeployEnv } else { 'production' }
    $c.DeployEnv = AskChoice 'Is this deployment for testing or production?' $default @(
        @('production', 'Production: MySQL root only from localhost, MySQL not reachable from other machines'),
        @('testing', 'Testing: MySQL (root included) reachable from any host, for inspecting data from your computer'))

    Info 'Web address'
    Note 'With a domain name (DNS pointing at this machine, ports 80 and 443 reachable from the internet), Caddy'
    Note 'gets a Let''s Encrypt certificate automatically. Leave it blank to serve plain HTTP (local testing).'
    $exHost = if ($Ex.SiteAddress -and -not $Ex.SiteAddress.StartsWith(':')) { $Ex.SiteAddress } else { '' }
    $c.SiteHost = Ask 'Domain name' $exHost $CheckDomain
    # Caddy's config always names an ACME account email, even when serving plain HTTP
    $c.AcmeEmail = Ask 'Email for Let''s Encrypt certificate notices' $Ex.AcmeEmail $CheckEmail
    if ($c.SiteHost) {
        $c.SiteAddress = $c.SiteHost
        $c.HttpPort = '80'; $c.HttpsPort = '443'
        $c.AppBaseUrl = "https://$($c.SiteHost)"
    } else {
        if ($c.DeployEnv -eq 'production') { Warn 'Production without HTTPS sends passwords and applicants'' data unencrypted.' }
        $c.SiteAddress = ':80'
        $c.HttpPort = Ask 'HTTP port on this machine' $(if ($Ex.HttpPort) { $Ex.HttpPort } else { '80' }) $CheckPort
        $c.HttpsPort = if ($Ex.HttpsPort) { $Ex.HttpsPort } else { '443' }
        $defaultUrl = if ($c.HttpPort -eq '80') { 'http://localhost' } else { "http://localhost:$($c.HttpPort)" }
        if ($Ex.AppBaseUrl -like 'http://*') { $defaultUrl = $Ex.AppBaseUrl }
        Note 'The address used in emailed links (verification, invites, reference requests).'
        $c.AppBaseUrl = Ask 'Site URL' $defaultUrl $CheckUrl
    }

    Info 'Database (MySQL 8 container)'
    if ($c.DeployEnv -eq 'testing') {
        $c.MysqlBind = '0.0.0.0'
        Note 'Testing: MySQL is published on all interfaces; root can sign in from any host.'
    } else {
        $c.MysqlBind = '127.0.0.1'
        Note 'Production: MySQL is published on 127.0.0.1 only and root is limited to inside the container.'
    }
    $c.MysqlPort = Ask 'MySQL port on this machine' $(if ($Ex.MysqlPort) { $Ex.MysqlPort } else { '3306' }) $CheckPort
    if ($Ex.DbRootPassword) {
        $c.DbRootPassword = $Ex.DbRootPassword
        Note 'Keeping the MySQL root password in secrets\mysql.env (MySQL only reads it when the volume is created).'
    } else {
        $c.DbRootPassword = AskSecret 'MySQL root password' '' 'generate' $CheckDbPassword $GenPassword
    }
    Note "The app connects as '$DbUser' with rights on $DbName only."
    $c.DbAppPassword = AskSecret "Password for the $DbUser MySQL user" $Ex.DbAppPassword 'generate' $CheckDbPassword $GenPassword

    Info 'Secrets'
    Note 'Session signing key: changing it signs everyone out.'
    $c.JwtKey = AskSecret 'JWT signing key' $Ex.Jwt 'generate' $CheckJwtKey $GenJwtKey
    Note 'Two-factor encryption key: keep it stable. If it changes, everyone enrolled in two-factor needs a reset.'
    while ($true) {
        $c.TwoFactorKey = AskSecret 'Two-factor encryption key' $Ex.TwoFactor 'generate' $CheckTwoFactorKey $GenTwoFactorKey
        if (-not $Ex.TwoFactor -or $c.TwoFactorKey -eq $Ex.TwoFactor) { break }
        if (AskYesNo 'The two-factor key changed: every user enrolled in two-factor will need it reset. Continue?' $false) { break }
    }

    Info 'Email'
    Note 'Emails (verification links, password reset codes, invites) are sent through Resend (resend.com).'
    Note 'Without an API key they are only written to the API log, so nobody receives them.'
    $defaultFrom = if ($Ex.EmailFrom) { $Ex.EmailFrom } elseif ($c.SiteHost) { "Scholarship App <noreply@$($c.SiteHost)>" } else { 'Scholarship App <onboarding@resend.dev>' }
    $c.EmailFrom = Ask 'Sender (a domain verified in Resend)' $defaultFrom $CheckRequired
    $c.ResendKey = AskSecret 'Resend API key' $Ex.Resend 'optional' $CheckOptional
    if (-not $c.ResendKey -and $c.DeployEnv -eq 'production') {
        Warn 'No Resend API key: new users can''t verify their email, so they can''t sign in.'
    }

    Info 'File storage'
    $default = if ($Ex.StorageProvider) { $Ex.StorageProvider } else { 'Local' }
    $c.StorageProvider = AskChoice 'Where should uploaded files (applicant documents, signatures) be stored?' $default @(
        @('Local', 'On this machine (Docker volume uploads_data)'),
        @('R2', 'Cloudflare R2'),
        @('AzureBlob', 'Azure Blob Storage'))
    $c.StorageLines = @("Storage__Provider=$($c.StorageProvider)")
    if ($c.StorageProvider -eq 'R2') {
        $c.StorageLines += "Storage__R2__AccountId=$(Ask 'R2 account ID' $Ex.R2AccountId $CheckRequired)"
        $c.StorageLines += "Storage__R2__AccessKeyId=$(Ask 'R2 access key ID' $Ex.R2AccessKeyId $CheckRequired)"
        $c.StorageLines += "Storage__R2__SecretAccessKey=$(AskSecret 'R2 secret access key' $Ex.R2Secret 'required' $CheckRequired)"
        $bucket = if ($Ex.R2Bucket) { $Ex.R2Bucket } else { 'scholarship-files' }
        $c.StorageLines += "Storage__R2__BucketName=$(Ask 'R2 bucket name' $bucket $CheckRequired)"
    } elseif ($c.StorageProvider -eq 'AzureBlob') {
        $c.StorageLines += "Storage__AzureBlobConnectionString=$(AskSecret 'Azure Blob connection string' $Ex.AzureConnection 'required' $CheckRequired)"
        $container = if ($Ex.AzureContainer) { $Ex.AzureContainer } else { 'scholarship-files' }
        $c.StorageLines += "Storage__ContainerName=$(Ask 'Azure container name' $container $CheckRequired)"
    }

    $c.SuperAdmin = Ask-SuperAdmin

    Info 'Summary'
    Note "Deployment:   Docker Compose ($($c.DeployEnv))"
    Note "Site URL:     $($c.AppBaseUrl)"
    Note "HTTPS:        $(if ($c.SiteHost) { "Let's Encrypt for $($c.SiteHost) (Caddy)" } else { 'no' })"
    Note "MySQL:        $($c.MysqlBind):$($c.MysqlPort), root $(if ($c.DeployEnv -eq 'testing') { 'from any host' } else { 'localhost only' })"
    Note "Uploads:      $($c.StorageProvider)"
    Note "Email:        $(if ($c.ResendKey) { "Resend, from $($c.EmailFrom)" } else { 'not sent (logged only)' })"
    $saSummary = if ($c.SuperAdmin) { "$($c.SuperAdmin.Email) (create or update)" } elseif ($Ex.SuperAdminEmail) { "unchanged ($($Ex.SuperAdminEmail))" } else { 'none yet' }
    Note "Super admin:  $saSummary"
    Write-Host ''
    if (-not (AskYesNo 'Proceed with the installation?' $true)) { Fail 'Cancelled; nothing was changed.' }
    return $c
}

function Write-DotEnv($c) {
    Write-EnvFile $DotEnv @(
        '# Written by deploy/deploy.ps1: re-run it to change these. Read by docker-compose.yml.',
        "DEPLOY_ENV=$($c.DeployEnv)",
        "SITE_ADDRESS=$($c.SiteAddress)",
        "ACME_EMAIL=$($c.AcmeEmail)",
        "APP_BASE_URL=$($c.AppBaseUrl)",
        "HTTP_PORT=$($c.HttpPort)",
        "HTTPS_PORT=$($c.HttpsPort)",
        "MYSQL_BIND=$($c.MysqlBind)",
        "MYSQL_PORT=$($c.MysqlPort)",
        '# The super admin set up by the script (only the email; used as the default next time)',
        "SUPER_ADMIN_EMAIL=$($Ex.SuperAdminEmail)")
}

function Write-Files($c) {
    Info 'Writing .env and secrets\*.env'
    New-Item -ItemType Directory -Force -Path $Secrets | Out-Null
    $rootHost = if ($c.DeployEnv -eq 'testing') { '%' } else { 'localhost' }
    Write-DotEnv $c
    Write-EnvFile "$Secrets\jwt.env" @('# Written by deploy/deploy.ps1', "Jwt__Key=$($c.JwtKey)")
    Write-EnvFile "$Secrets\twofactor.env" @('# Written by deploy/deploy.ps1', "TwoFactor__EncryptionKey=$($c.TwoFactorKey)")
    Write-EnvFile "$Secrets\mysql.env" @(
        '# Written by deploy/deploy.ps1. MySQL only reads these when it first creates its data volume; the script',
        '# applies the root host and app user to an existing database itself.',
        "MYSQL_ROOT_PASSWORD=$($c.DbRootPassword)",
        "MYSQL_DATABASE=$DbName",
        "MYSQL_ROOT_HOST=$rootHost")
    Write-EnvFile "$Secrets\db.env" @('# Written by deploy/deploy.ps1',
        "ConnectionStrings__Default=Server=mysql;Database=$DbName;User=$DbUser;AllowPublicKeyRetrieval=True;Password=$($c.DbAppPassword)")
    Write-EnvFile "$Secrets\email.env" @('# Written by deploy/deploy.ps1', "Email__From=$($c.EmailFrom)", "Email__Resend__ApiKey=$($c.ResendKey)")
    Write-EnvFile "$Secrets\storage.env" (@('# Written by deploy/deploy.ps1') + $c.StorageLines)
    if (Test-Path "$Secrets\r2.env") {
        Move-Item -Force "$Secrets\r2.env" "$Secrets\r2.env.old"
        Note 'secrets\r2.env is no longer used (its settings are in storage.env); renamed it to r2.env.old.'
    }
}

# Applies the app user and the root host rule; works on new and existing data volumes alike
function Setup-Mysql($c) {
    Info 'Starting MySQL'
    Compose up -d --wait mysql
    $sql = @(
        "CREATE DATABASE IF NOT EXISTS ``$DbName``;",
        "CREATE USER IF NOT EXISTS '$DbUser'@'%' IDENTIFIED BY '$($c.DbAppPassword)';",
        "ALTER USER '$DbUser'@'%' IDENTIFIED BY '$($c.DbAppPassword)';",
        "GRANT ALL PRIVILEGES ON ``$DbName``.* TO '$DbUser'@'%';")
    if ($c.DeployEnv -eq 'testing') {
        $sql += "CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY '$($c.DbRootPassword)';"
        $sql += "ALTER USER 'root'@'%' IDENTIFIED BY '$($c.DbRootPassword)';"
        $sql += "GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;"
    } else {
        $sql += "DROP USER IF EXISTS 'root'@'%';"
    }
    $sql += 'FLUSH PRIVILEGES;'
    $text = $sql -join "`n"
    # On a new volume MySQL restarts once after initializing, so retry until the statements go through
    Push-Location $RepoRoot
    try {
        for ($i = 0; $i -lt 30; $i++) {
            if (Try-Native { $text | docker compose exec -T mysql sh -c 'MYSQL_PWD=$MYSQL_ROOT_PASSWORD exec mysql -uroot' }) {
                Note 'MySQL users configured.'
                return
            }
            Start-Sleep -Seconds 3
        }
    } finally { Pop-Location }
    Try-Native { docker compose logs --tail 40 mysql } -Show | Out-Null
    Fail "Couldn't configure MySQL as root. If the mysql_data volume predates secrets\mysql.env, its root password differs."
}

function Start-App {
    Info 'Building and starting the containers'
    Compose up -d --build --remove-orphans
    Note 'Waiting for the API (the first start runs database migrations)...'
    Push-Location $RepoRoot
    try {
        for ($i = 0; $i -lt 60; $i++) {
            $old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
            $out = & docker compose exec -T frontend wget -S -O /dev/null http://backend:8080/api/auth/me 2>&1 | Out-String
            $ErrorActionPreference = $old
            if ($out -match ' 401') { Note 'API is up.'; return }
            Start-Sleep -Seconds 3
        }
    } finally { Pop-Location }
    Try-Native { docker compose logs --tail 60 backend } -Show | Out-Null
    Fail "The API didn't start. See the log above (docker compose logs backend)."
}

# Runs the API image's --create-super-admin mode in a one-off container; details go on stdin. Returns $true on success.
function Setup-SuperAdmin($c) {
    $sa = $c.SuperAdmin
    if (-not $sa) { return $false }
    Info 'Setting up the super admin'
    $text = @($sa.Email, $sa.FirstName, $sa.LastName, $sa.Password) -join "`n"
    Push-Location $RepoRoot
    try { $ok = Try-Native { $text | docker compose run --rm -T --no-deps backend --create-super-admin } -Show }
    finally { Pop-Location }
    if ($ok) {
        $Ex.SuperAdminEmail = $sa.Email
        Write-DotEnv $c
        return $true
    }
    Warn 'The super admin wasn''t set up (see the message above). Re-run the script to try again.'
    return $false
}

# ---------------------------------------------------------------------------------------------------------------------

Ensure-Docker
Load-Existing
$settings = Ask-Settings
Write-Files $settings
Setup-Mysql $settings
Start-App
$superAdminDone = Setup-SuperAdmin $settings

Info "Done: $($settings.AppBaseUrl)"
if ($settings.SiteHost) { Note 'Caddy requests the HTTPS certificate on the first visit; give it a minute.' }
Note 'Status / logs: docker compose ps   ·   docker compose logs -f backend'
Note 'Settings:      .env and secrets\*.env (gitignored)'
if ($settings.DeployEnv -eq 'testing') {
    Note "MySQL root:    any host, port $($settings.MysqlPort); password in secrets\mysql.env"
} else {
    Note "MySQL root:    inside the container only: docker compose exec mysql sh -c 'mysql -uroot -p`"`$MYSQL_ROOT_PASSWORD`" $DbName'"
}
Note 'Redeploy:      git pull, then run deploy\deploy.ps1 again'
if ($superAdminDone) { Note "Super admin:   sign in as $($settings.SuperAdmin.Email)" }
elseif ($Ex.SuperAdminEmail) { Note "Super admin:   $($Ex.SuperAdminEmail)" }
else { Note 'Super admin:   none yet; re-run the script and answer yes to create one' }
