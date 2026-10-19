#!/usr/bin/env bash
# Installs what the Scholarship App needs and deploys it, either as:
#   nginx   Ubuntu only. nginx serves the built frontend and proxies /api to the API, which runs as the
#           scholarship-api systemd service; MySQL 8 is installed on the same server.
#   docker  Ubuntu or macOS (Windows: deploy.ps1). docker-compose.yml: Caddy, nginx + frontend, API, MySQL.
#
# Usage: deploy/deploy.sh [nginx|docker]
# Run it from a checkout of the repo; re-run it after pulling to redeploy. Answers and secrets from the previous run
# are offered as defaults, so pressing Enter through the prompts keeps everything as it is.
set -euo pipefail

SCRIPT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/$(basename "${BASH_SOURCE[0]}")"
REPO_ROOT="$(cd "$(dirname "$SCRIPT_PATH")/.." && pwd)"

DB_NAME=scholarship_db
DB_USER=scholarship
API_PORT=5098

# nginx (monolith) layout
SERVICE_NAME=scholarship-api
SERVICE_USER=scholarship
APP_DIR=/opt/scholarship-app/api
WEB_DIR=/var/www/scholarship-app
DATA_DIR=/var/lib/scholarship-app
CONF_DIR=/etc/scholarship-app
API_ENV_FILE=$CONF_DIR/api.env
DEPLOY_CONF=$CONF_DIR/deploy.conf
MYSQL_ROOT_FILE=$CONF_DIR/mysql-root.env
ACME_WEBROOT=/var/www/letsencrypt
NGINX_SITE=/etc/nginx/sites-available/scholarship-app
CERT_RENEW_SCRIPT=/usr/local/sbin/scholarship-cert-renew
CERT_RENEW_UNIT=scholarship-cert-renew
CERT_MAX_AGE_DAYS=30

# ---------------------------------------------------------------------------------------------------------------------
# Output and prompts
# ---------------------------------------------------------------------------------------------------------------------

info() { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
note() { printf '    %s\n' "$*"; }
warn() { printf '\033[33mWARNING:\033[0m %s\n' "$*" >&2; }
die() { printf '\033[31mERROR:\033[0m %s\n' "$*" >&2; exit 1; }

# ask VAR "Question" [default]
ask() {
    local __var=$1 __q=$2 __def=${3-} __ans
    if [ -n "$__def" ]; then printf '%s [%s]: ' "$__q" "$__def"; else printf '%s: ' "$__q"; fi
    IFS= read -r __ans || die "Input closed."
    printf -v "$__var" '%s' "${__ans:-$__def}"
}

# ask_valid VAR "Question" default validator — repeats until the validator accepts the answer
ask_valid() {
    while :; do
        ask "$1" "$2" "${3-}"
        "$4" "${!1}" && return 0
    done
}

# ask_yn VAR "Question" y|n
ask_yn() {
    local __var=$1 __q=$2 __def=$3 __ans
    while :; do
        if [ "$__def" = y ]; then printf '%s [Y/n]: ' "$__q"; else printf '%s [y/N]: ' "$__q"; fi
        IFS= read -r __ans || die "Input closed."
        case "${__ans:-$__def}" in
            y|Y|yes|YES|Yes) printf -v "$__var" y; return ;;
            n|N|no|NO|No) printf -v "$__var" n; return ;;
        esac
    done
}

# ask_choice VAR "Question" default-value "value|description" ...
ask_choice() {
    local __var=$1 __q=$2 __def=$3 __i=0 __defnum=1 __opt __ans
    shift 3
    echo "$__q"
    for __opt in "$@"; do
        __i=$((__i + 1))
        printf '  %d) %s\n' "$__i" "${__opt#*|}"
        [ "${__opt%%|*}" = "$__def" ] && __defnum=$__i
    done
    while :; do
        printf 'Choose 1-%d [%d]: ' "$__i" "$__defnum"
        IFS= read -r __ans || die "Input closed."
        __ans=${__ans:-$__defnum}
        case "$__ans" in *[!0-9]*|'') continue ;; esac
        if [ "$__ans" -ge 1 ] && [ "$__ans" -le "$__i" ]; then
            __opt=$(eval "printf '%s' \"\${$__ans}\"")
            printf -v "$__var" '%s' "${__opt%%|*}"
            return
        fi
    done
}

# ask_secret VAR "Question" current-value generate|optional|required validator [generator]
#   Input is hidden. Enter keeps the current value; with none, "generate" makes one, "optional" leaves it empty.
#   Typing "new" generates a fresh value ("generate"); typing "none" clears it ("optional").
ask_secret() {
    local __var=$1 __q=$2 __cur=$3 __mode=$4 __check=$5 __gen=${6-} __ans __hint
    if [ -n "$__cur" ]; then
        __hint="Enter keeps the current value"
        [ "$__mode" = generate ] && __hint="$__hint, \"new\" generates another"
        [ "$__mode" = optional ] && __hint="$__hint, \"none\" clears it"
    elif [ "$__mode" = generate ]; then __hint="Enter generates one"
    elif [ "$__mode" = optional ]; then __hint="Enter to skip"
    else __hint="required"
    fi
    while :; do
        printf '%s (hidden; %s): ' "$__q" "$__hint"
        IFS= read -rs __ans || die "Input closed."
        echo
        if [ "$__mode" = generate ] && [ "$__ans" = new ]; then __ans=$($__gen)
        elif [ "$__mode" = optional ] && [ "$__ans" = none ]; then printf -v "$__var" '%s' ''; return
        elif [ -z "$__ans" ]; then
            if [ -n "$__cur" ]; then __ans=$__cur
            elif [ "$__mode" = generate ]; then __ans=$($__gen)
            elif [ "$__mode" = optional ]; then printf -v "$__var" '%s' ''; return
            else echo "    A value is required."; continue
            fi
        fi
        "$__check" "$__ans" || continue
        printf -v "$__var" '%s' "$__ans"
        return
    done
}

# ---------------------------------------------------------------------------------------------------------------------
# Validation and generation
# ---------------------------------------------------------------------------------------------------------------------

# Values end up in env files read by systemd and Docker Compose; Compose expands "$", systemd treats "\" and quotes
valid_env_value() {
    case "$1" in
        *'$'*|*'\'*|*'"'*|*"'"*) echo "    Can't contain \$, quotes or backslashes."; return 1 ;;
        ' '*|*' ') echo "    Can't start or end with a space."; return 1 ;;
    esac
}
valid_required() { [ -n "$1" ] || { echo "    A value is required."; return 1; }; valid_env_value "$1"; }
valid_optional() { valid_env_value "$1"; }
valid_db_password() {
    local re='^[A-Za-z0-9!@%^*_+=.,:~-]{12,}$'
    [[ $1 =~ $re ]] && return 0
    echo "    Use 12+ characters: letters, digits and ! @ % ^ * _ + = . , : ~ - (no quotes, spaces, \$ or ;)."
    return 1
}
valid_jwt_key() {
    valid_env_value "$1" || return 1
    [ "$(printf '%s' "$1" | wc -c | tr -d ' ')" -ge 32 ] || { echo "    Must be at least 32 bytes."; return 1; }
    case "$1" in REPLACE_*) echo "    Replace the placeholder with a real key."; return 1 ;; esac
}
valid_twofactor_key() {
    local n
    n=$(printf '%s' "$1" | openssl base64 -d -A 2>/dev/null | wc -c | tr -d ' ')
    [ "$n" = 32 ] && return 0
    echo "    Must be base64 for exactly 32 bytes (openssl rand -base64 32)."
    return 1
}
valid_email() {
    case "$1" in *@*.*) valid_env_value "$1" ;; *) echo "    Enter an email address."; return 1 ;; esac
}
valid_domain() {
    local re='^[A-Za-z0-9]([A-Za-z0-9.-]*[A-Za-z0-9])?$'
    [ -z "$1" ] && return 0
    [[ $1 =~ $re ]] && return 0
    echo "    Enter a host name only, e.g. scholarships.example.org (no http://, no path)."
    return 1
}
valid_url() {
    case "$1" in
        http://*|https://*) case "$1" in */) echo "    Leave off the trailing slash."; return 1 ;; esac; valid_env_value "$1" ;;
        *) echo "    Start with http:// or https://."; return 1 ;;
    esac
}
valid_https_url() {
    case "$1" in https://*) valid_url "$1" ;; *) echo "    The site is HTTPS only: start with https://."; return 1 ;; esac
}
valid_port() {
    case "$1" in *[!0-9]*|'') echo "    Enter a port number."; return 1 ;; esac
    [ "$1" -ge 1 ] && [ "$1" -le 65535 ] || { echo "    Enter a port number."; return 1; }
}

valid_admin_password() {
    local bytes
    bytes=$(printf '%s' "$1" | wc -c | tr -d ' ')
    [ "${#1}" -ge 8 ] && [ "$bytes" -le 72 ] && return 0
    echo "    Use 8 to 72 characters."
    return 1
}
valid_name() { [ -n "$1" ] && [ "${#1}" -le 100 ] && return 0; echo "    Enter 1 to 100 characters."; return 1; }
valid_name_or_blank() { [ "${#1}" -le 100 ] && return 0; echo "    Use at most 100 characters."; return 1; }

gen_jwt_key() { openssl rand -base64 48 | tr -d '\n'; }
gen_twofactor_key() { openssl rand -base64 32 | tr -d '\n'; }
gen_password() { local p; p=$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9'); printf '%s' "${p:0:32}"; }

# env_get FILE KEY — the value of KEY=... in an env file, or nothing
env_get() {
    [ -r "$1" ] || return 0
    sed -n "s/^$2=//p" "$1" | tail -n 1
}

conn_password() { printf '%s' "$1" | sed -n 's/.*Password=\([^;]*\).*/\1/p'; }

# write_file PATH MODE — writes stdin to PATH atomically with the given permissions
write_file() {
    local tmp
    tmp=$(mktemp "$1.XXXXXX")
    cat > "$tmp"
    chmod "$2" "$tmp"
    mv -f "$tmp" "$1"
}

# ---------------------------------------------------------------------------------------------------------------------
# Questions shared by both deployment types
# ---------------------------------------------------------------------------------------------------------------------

prompt_environment() {
    info "Environment"
    ask_choice DEPLOY_ENV "Is this deployment for testing or production?" "${EX_DEPLOY_ENV:-production}" \
        "production|Production: MySQL root only from localhost, MySQL not reachable from other machines" \
        "testing|Testing: MySQL (root included) reachable from any host, for inspecting data from your computer"
}

# Sets JWT_KEY, TWOFACTOR_KEY, EMAIL_FROM, RESEND_KEY and the STORAGE_* / R2_* / AZURE_* settings
prompt_app_settings() {
    local default_from
    info "Secrets"
    note "Session signing key: changing it signs everyone out."
    ask_secret JWT_KEY "JWT signing key" "$EX_JWT" generate valid_jwt_key gen_jwt_key
    note "Two-factor encryption key: keep it stable. If it changes, everyone enrolled in two-factor needs a reset."
    while :; do
        ask_secret TWOFACTOR_KEY "Two-factor encryption key" "$EX_TWOFACTOR" generate valid_twofactor_key gen_twofactor_key
        [ -z "$EX_TWOFACTOR" ] || [ "$TWOFACTOR_KEY" = "$EX_TWOFACTOR" ] && break
        local sure
        ask_yn sure "The two-factor key changed: every user enrolled in two-factor will need it reset. Continue?" n
        [ "$sure" = y ] && break
    done

    info "Email"
    note "Emails (verification links, password reset codes, invites) are sent through Resend (resend.com)."
    note "Without an API key they are only written to the API log, so nobody receives them."
    if [ -n "$SITE_HOST" ]; then default_from="Scholarship App <noreply@$SITE_HOST>"; else default_from="Scholarship App <onboarding@resend.dev>"; fi
    ask_valid EMAIL_FROM "Sender (a domain verified in Resend)" "${EX_EMAIL_FROM:-$default_from}" valid_required
    ask_secret RESEND_KEY "Resend API key" "$EX_RESEND" optional valid_optional
    if [ -z "$RESEND_KEY" ] && [ "$DEPLOY_ENV" = production ]; then
        warn "No Resend API key: new users can't verify their email, so they can't sign in."
    fi

    info "File storage"
    ask_choice STORAGE_PROVIDER "Where should uploaded files (applicant documents, signatures) be stored?" \
        "${EX_STORAGE_PROVIDER:-Local}" \
        "Local|On this server's disk" \
        "R2|Cloudflare R2" \
        "AzureBlob|Azure Blob Storage"
    R2_ACCOUNT_ID='' R2_ACCESS_KEY_ID='' R2_SECRET='' R2_BUCKET='' AZURE_CONNECTION='' AZURE_CONTAINER=''
    case "$STORAGE_PROVIDER" in
        R2)
            ask_valid R2_ACCOUNT_ID "R2 account ID" "$EX_R2_ACCOUNT_ID" valid_required
            ask_valid R2_ACCESS_KEY_ID "R2 access key ID" "$EX_R2_ACCESS_KEY_ID" valid_required
            ask_secret R2_SECRET "R2 secret access key" "$EX_R2_SECRET" required valid_required
            ask_valid R2_BUCKET "R2 bucket name" "${EX_R2_BUCKET:-scholarship-files}" valid_required
            ;;
        AzureBlob)
            ask_secret AZURE_CONNECTION "Azure Blob connection string" "$EX_AZURE_CONNECTION" required valid_required
            ask_valid AZURE_CONTAINER "Azure container name" "${EX_AZURE_CONTAINER:-scholarship-files}" valid_required
            ;;
    esac
}

# Sets SA_SETUP (y/n) and SA_EMAIL, SA_FIRST, SA_LAST, SA_PASSWORD. Nobody can register as a super admin, so the
# first one is made here; the API creates it (or promotes an existing account) after migrations.
prompt_super_admin() {
    info "Super admin account"
    note "A super admin can do everything, including creating the other admin, scorer and staff accounts."
    local default=y again check=valid_name
    if [ -n "$EX_SUPER_ADMIN_EMAIL" ]; then
        note "Set up on a previous run: $EX_SUPER_ADMIN_EMAIL"
        default=n
    fi
    SA_EMAIL='' SA_FIRST='' SA_LAST='' SA_PASSWORD=''
    ask_yn SA_SETUP "Create or update a super admin account now?" "$default"
    [ "$SA_SETUP" = y ] || return 0

    ask_valid SA_EMAIL "Super admin email" "$EX_SUPER_ADMIN_EMAIL" valid_email
    note "If an account with this email already exists, it's made a super admin."
    if [ "$SA_EMAIL" = "$EX_SUPER_ADMIN_EMAIL" ]; then
        note "Leave a field blank to keep the account's current value."
        check=valid_name_or_blank
    fi
    ask_valid SA_FIRST "First name" "" "$check"
    ask_valid SA_LAST "Last name" "" "$check"
    while :; do
        printf 'Password (hidden; 8+ characters%s): ' "$([ "$check" = valid_name_or_blank ] && echo ", Enter keeps the current one")"
        IFS= read -rs SA_PASSWORD || die "Input closed."
        echo
        if [ -z "$SA_PASSWORD" ]; then
            [ "$check" = valid_name_or_blank ] && break
            echo "    A password is required."
            continue
        fi
        valid_admin_password "$SA_PASSWORD" || continue
        printf 'Confirm the password: '
        IFS= read -rs again || die "Input closed."
        echo
        [ "$again" = "$SA_PASSWORD" ] && break
        echo "    The passwords don't match."
    done
}

super_admin_summary() {
    if [ "$SA_SETUP" = y ]; then echo "$SA_EMAIL (create or update)"
    elif [ -n "$EX_SUPER_ADMIN_EMAIL" ]; then echo "unchanged ($EX_SUPER_ADMIN_EMAIL)"
    else echo "none yet"
    fi
}

super_admin_done_note() {
    if [ "$SA_SETUP" = y ] && [ "${SA_FAILED-}" != y ]; then note "Super admin:   sign in as $SA_EMAIL"
    elif [ -n "$EX_SUPER_ADMIN_EMAIL" ]; then note "Super admin:   $EX_SUPER_ADMIN_EMAIL"
    else note "Super admin:   none yet; re-run the script and answer yes to create one"
    fi
}

# Env lines for the app settings above (shared by api.env and the Docker secrets files)
storage_env_lines() {
    echo "Storage__Provider=$STORAGE_PROVIDER"
    case "$STORAGE_PROVIDER" in
        R2)
            echo "Storage__R2__AccountId=$R2_ACCOUNT_ID"
            echo "Storage__R2__AccessKeyId=$R2_ACCESS_KEY_ID"
            echo "Storage__R2__SecretAccessKey=$R2_SECRET"
            echo "Storage__R2__BucketName=$R2_BUCKET"
            ;;
        AzureBlob)
            echo "Storage__AzureBlobConnectionString=$AZURE_CONNECTION"
            echo "Storage__ContainerName=$AZURE_CONTAINER"
            ;;
    esac
}

confirm_or_exit() {
    local go
    echo
    ask_yn go "Proceed with the installation?" y
    [ "$go" = y ] || die "Cancelled; nothing was changed."
}

# ---------------------------------------------------------------------------------------------------------------------
# nginx monolith (Ubuntu)
# ---------------------------------------------------------------------------------------------------------------------

apt_install() { DEBIAN_FRONTEND=noninteractive apt-get install -y -q "$@"; }

nginx_load_existing() {
    EX_DEPLOY_ENV=$(env_get "$DEPLOY_CONF" DEPLOY_ENV)
    EX_SITE_HOST=$(env_get "$DEPLOY_CONF" SITE_HOST)
    EX_USE_TLS=$(env_get "$DEPLOY_CONF" USE_TLS)
    EX_ACME_EMAIL=$(env_get "$DEPLOY_CONF" ACME_EMAIL)
    EX_SUPER_ADMIN_EMAIL=$(env_get "$DEPLOY_CONF" SUPER_ADMIN_EMAIL)
    EX_APP_BASE_URL=$(env_get "$API_ENV_FILE" App__BaseUrl)
    EX_JWT=$(env_get "$API_ENV_FILE" Jwt__Key)
    EX_TWOFACTOR=$(env_get "$API_ENV_FILE" TwoFactor__EncryptionKey)
    EX_EMAIL_FROM=$(env_get "$API_ENV_FILE" Email__From)
    EX_RESEND=$(env_get "$API_ENV_FILE" Email__Resend__ApiKey)
    EX_STORAGE_PROVIDER=$(env_get "$API_ENV_FILE" Storage__Provider)
    EX_R2_ACCOUNT_ID=$(env_get "$API_ENV_FILE" Storage__R2__AccountId)
    EX_R2_ACCESS_KEY_ID=$(env_get "$API_ENV_FILE" Storage__R2__AccessKeyId)
    EX_R2_SECRET=$(env_get "$API_ENV_FILE" Storage__R2__SecretAccessKey)
    EX_R2_BUCKET=$(env_get "$API_ENV_FILE" Storage__R2__BucketName)
    EX_AZURE_CONNECTION=$(env_get "$API_ENV_FILE" Storage__AzureBlobConnectionString)
    EX_AZURE_CONTAINER=$(env_get "$API_ENV_FILE" Storage__ContainerName)
    EX_DB_APP_PASSWORD=$(conn_password "$(env_get "$API_ENV_FILE" ConnectionStrings__Default)")
    EX_DB_ROOT_PASSWORD=$(env_get "$MYSQL_ROOT_FILE" MYSQL_ROOT_PASSWORD)
}

nginx_prompts() {
    local ip default_url
    prompt_environment

    info "Web address"
    note "With a domain name (DNS pointing at this server), nginx answers for that name and can get a free"
    note "Let's Encrypt certificate. Leave it blank to answer on any name or IP address over plain HTTP."
    ask_valid SITE_HOST "Domain name" "$EX_SITE_HOST" valid_domain
    USE_TLS=n ACME_EMAIL=''
    if cert_valid; then
        # A site with a working certificate is only ever served over HTTPS
        USE_TLS=y ACME_EMAIL=$EX_ACME_EMAIL
        note "$SITE_HOST already has a valid certificate: the site is served over HTTPS only (HTTP redirects)."
    elif [ -n "$SITE_HOST" ]; then
        local tls_default=y
        [ "$DEPLOY_ENV" = testing ] && tls_default=n
        [ -n "$EX_USE_TLS" ] && tls_default=$EX_USE_TLS
        ask_yn USE_TLS "Get a Let's Encrypt HTTPS certificate? (ports 80 and 443 must be reachable from the internet)" "$tls_default"
        [ "$USE_TLS" = y ] && ask_valid ACME_EMAIL "Email for certificate expiry notices" "$EX_ACME_EMAIL" valid_email
    fi
    if [ "$USE_TLS" = n ] && [ "$DEPLOY_ENV" = production ]; then
        warn "Production without HTTPS sends passwords and applicants' data unencrypted."
    fi
    ip=$(hostname -I 2>/dev/null | awk '{print $1}')
    if [ "$USE_TLS" = y ]; then default_url="https://$SITE_HOST"
    elif [ -n "$SITE_HOST" ]; then default_url="http://$SITE_HOST"
    else default_url="http://${ip:-localhost}"
    fi
    # The URL changes with the address answers, so only reuse the old one if those didn't change
    [ "$SITE_HOST" = "$EX_SITE_HOST" ] && [ "$USE_TLS" = "${EX_USE_TLS:-n}" ] && [ -n "$EX_APP_BASE_URL" ] && default_url=$EX_APP_BASE_URL
    note "The address used in emailed links (verification, invites, reference requests)."
    if [ "$USE_TLS" = y ]; then ask_valid APP_BASE_URL "Site URL" "$default_url" valid_https_url
    else ask_valid APP_BASE_URL "Site URL" "$default_url" valid_url
    fi

    info "Database (MySQL 8 on this server)"
    note "The app connects as '$DB_USER'@'127.0.0.1' with rights on $DB_NAME only."
    ask_secret DB_APP_PASSWORD "Password for the $DB_USER MySQL user" "$EX_DB_APP_PASSWORD" generate valid_db_password gen_password
    DB_ROOT_PASSWORD=''
    if [ "$DEPLOY_ENV" = testing ]; then
        note "Testing: root can sign in from any host with this password (port 3306 is opened)."
        ask_secret DB_ROOT_PASSWORD "Password for remote MySQL root" "$EX_DB_ROOT_PASSWORD" generate valid_db_password gen_password
    else
        note "Production: root stays local-only (sudo mysql); any remote root account is removed."
    fi

    prompt_app_settings
    prompt_super_admin

    info "Summary"
    note "Deployment:   nginx + systemd ($DEPLOY_ENV)"
    note "Site URL:     $APP_BASE_URL"
    note "HTTPS:        $([ "$USE_TLS" = y ] && echo "Let's Encrypt for $SITE_HOST" || echo "no")"
    note "MySQL root:   $([ "$DEPLOY_ENV" = testing ] && echo "any host (port 3306 open)" || echo "localhost only")"
    note "Uploads:      $STORAGE_PROVIDER$([ "$STORAGE_PROVIDER" = Local ] && echo " ($DATA_DIR/uploads)")"
    note "Email:        $([ -n "$RESEND_KEY" ] && echo "Resend, from $EMAIL_FROM" || echo "not sent (logged only)")"
    note "Super admin:  $(super_admin_summary)"
    confirm_or_exit
}

install_node() {
    local major=0
    command -v node >/dev/null 2>&1 && major=$(node -p 'process.versions.node.split(".")[0]' 2>/dev/null || echo 0)
    if [ "$major" -ge 20 ] && command -v npm >/dev/null 2>&1; then
        note "Node.js $(node --version) already installed."
        return
    fi
    info "Installing Node.js 24 (NodeSource)"
    curl -fsSL https://deb.nodesource.com/setup_24.x | bash -
    apt_install nodejs
}

install_dotnet() {
    if command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
        note ".NET SDK $(dotnet --version) already installed."
        return
    fi
    info "Installing the .NET 10 SDK"
    if apt-cache show dotnet-sdk-10.0 >/dev/null 2>&1; then
        apt_install dotnet-sdk-10.0
        return
    fi
    # Ubuntu releases that don't ship .NET 10 get it from Canonical's backports PPA
    apt_install software-properties-common
    if add-apt-repository -y ppa:dotnet/backports && apt-get update -q && apt-cache show dotnet-sdk-10.0 >/dev/null 2>&1; then
        apt_install dotnet-sdk-10.0
        return
    fi
    add-apt-repository -y --remove ppa:dotnet/backports >/dev/null 2>&1 || true
    apt-get update -q
    # Last resort: Microsoft's install script (not updated by apt; re-run this script to upgrade)
    apt_install libicu-dev
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
    rm -f /tmp/dotnet-install.sh
    ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet
}

nginx_install_packages() {
    info "Installing packages"
    apt-get update -q
    apt_install ca-certificates curl gnupg rsync openssl nginx mysql-server
    [ "$USE_TLS" = y ] && apt_install certbot
    install_node
    install_dotnet
}

# True when SITE_HOST has a Let's Encrypt certificate on this server that hasn't expired
cert_valid() {
    local cert="/etc/letsencrypt/live/$SITE_HOST/fullchain.pem"
    [ -n "$SITE_HOST" ] && [ -f "$cert" ] && command -v openssl >/dev/null 2>&1 &&
        openssl x509 -checkend 0 -noout -in "$cert" >/dev/null 2>&1
}

# Runs SQL from stdin as MySQL root over the local socket (Ubuntu's root uses auth_socket)
mysql_root() {
    if [ -n "${MYSQL_ROOT_LOCAL_PASSWORD-}" ]; then MYSQL_PWD=$MYSQL_ROOT_LOCAL_PASSWORD mysql -uroot "$@"
    else mysql -uroot "$@"
    fi
}

nginx_setup_mysql() {
    info "Configuring MySQL"
    systemctl enable --now mysql
    if ! mysql_root -e 'SELECT 1' >/dev/null 2>&1; then
        # root was given a password at some point instead of Ubuntu's socket authentication
        ask_secret MYSQL_ROOT_LOCAL_PASSWORD "Current MySQL root password on this server" "" required valid_optional
        mysql_root -e 'SELECT 1' >/dev/null 2>&1 || die "Can't sign in to MySQL as root."
    fi

    local bind=127.0.0.1 cnf=/etc/mysql/mysql.conf.d/zz-scholarship.cnf new_cnf
    [ "$DEPLOY_ENV" = testing ] && bind=0.0.0.0
    new_cnf="# Managed by deploy/deploy.sh ($DEPLOY_ENV)
[mysqld]
bind-address = $bind
mysqlx-bind-address = 127.0.0.1"
    if [ "$(cat "$cnf" 2>/dev/null)" != "$new_cnf" ]; then
        printf '%s\n' "$new_cnf" | write_file "$cnf" 644
        systemctl restart mysql
    fi

    {
        echo "CREATE DATABASE IF NOT EXISTS \`$DB_NAME\`;"
        echo "CREATE USER IF NOT EXISTS '$DB_USER'@'127.0.0.1' IDENTIFIED BY '$DB_APP_PASSWORD';"
        echo "ALTER USER '$DB_USER'@'127.0.0.1' IDENTIFIED BY '$DB_APP_PASSWORD';"
        echo "GRANT ALL PRIVILEGES ON \`$DB_NAME\`.* TO '$DB_USER'@'127.0.0.1';"
        if [ "$DEPLOY_ENV" = testing ]; then
            echo "CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY '$DB_ROOT_PASSWORD';"
            echo "ALTER USER 'root'@'%' IDENTIFIED BY '$DB_ROOT_PASSWORD';"
            echo "GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;"
        else
            echo "DROP USER IF EXISTS 'root'@'%';"
        fi
        echo "FLUSH PRIVILEGES;"
    } | mysql_root

    if [ "$DEPLOY_ENV" = testing ]; then
        printf '# Remote MySQL root password (testing deployment)\nMYSQL_ROOT_PASSWORD=%s\n' "$DB_ROOT_PASSWORD" | write_file "$MYSQL_ROOT_FILE" 600
    else
        rm -f "$MYSQL_ROOT_FILE"
    fi
}

nginx_write_deploy_conf() {
    {
        echo "# Answers from the last deploy/deploy.sh run (secrets are in api.env)"
        echo "DEPLOY_ENV=$DEPLOY_ENV"
        echo "SITE_HOST=$SITE_HOST"
        echo "USE_TLS=$USE_TLS"
        echo "ACME_EMAIL=$ACME_EMAIL"
        echo "SUPER_ADMIN_EMAIL=$EX_SUPER_ADMIN_EMAIL"
    } | write_file "$DEPLOY_CONF" 600
}

nginx_write_config_files() {
    info "Writing configuration"
    id "$SERVICE_USER" >/dev/null 2>&1 ||
        useradd --system --home-dir "$DATA_DIR" --no-create-home --shell /usr/sbin/nologin "$SERVICE_USER"
    install -d -m 700 -o root -g root "$CONF_DIR"
    install -d -m 755 "$APP_DIR" "$WEB_DIR" "$ACME_WEBROOT"
    install -d -m 750 -o "$SERVICE_USER" -g "$SERVICE_USER" "$DATA_DIR" "$DATA_DIR/uploads"

    {
        echo "# Managed by deploy/deploy.sh: re-run it to change these values."
        echo "ASPNETCORE_ENVIRONMENT=Production"
        echo "ASPNETCORE_URLS=http://127.0.0.1:$API_PORT"
        echo "App__BaseUrl=$APP_BASE_URL"
        echo "ConnectionStrings__Default=Server=127.0.0.1;Port=3306;Database=$DB_NAME;User=$DB_USER;Password=$DB_APP_PASSWORD"
        echo "Jwt__Key=$JWT_KEY"
        echo "TwoFactor__EncryptionKey=$TWOFACTOR_KEY"
        echo "Email__From=$EMAIL_FROM"
        echo "Email__Resend__ApiKey=$RESEND_KEY"
        storage_env_lines
        echo "Storage__LocalPath=$DATA_DIR/uploads"
    } | write_file "$API_ENV_FILE" 600

    nginx_write_deploy_conf

    write_file "/etc/systemd/system/$SERVICE_NAME.service" 644 <<EOF
# Managed by deploy/deploy.sh
[Unit]
Description=Scholarship App API
After=network-online.target mysql.service
Wants=network-online.target
Requires=mysql.service

[Service]
Type=simple
User=$SERVICE_USER
Group=$SERVICE_USER
WorkingDirectory=$APP_DIR
ExecStart=$(command -v dotnet) $APP_DIR/ScholarshipApi.dll
EnvironmentFile=$API_ENV_FILE
Environment=HOME=$DATA_DIR DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=$SERVICE_NAME
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
ReadWritePaths=$DATA_DIR

[Install]
WantedBy=multi-user.target
EOF
    systemctl daemon-reload
}

nginx_build_and_install() {
    local build_user stage
    build_user=$(stat -c %U "$REPO_ROOT")
    as_builder() { if [ "$build_user" = root ]; then "$@"; else sudo -u "$build_user" -H -- "$@"; fi; }

    info "Building the frontend (as $build_user)"
    (cd "$REPO_ROOT/frontend" && as_builder npm ci --no-audit --no-fund && as_builder npm run build)

    info "Building the API (as $build_user)"
    stage=$(mktemp -d)
    chown "$build_user" "$stage"
    as_builder env DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 \
        dotnet publish "$REPO_ROOT/backend/ScholarshipApi/ScholarshipApi.csproj" -c Release -o "$stage" --nologo

    info "Installing the build"
    # appsettings.Development.json holds dev-only secrets; production never loads it, so don't ship it
    rsync -a --delete --exclude appsettings.Development.json --chown=root:root "$stage/" "$APP_DIR/"
    rsync -a --delete --chown=root:root "$REPO_ROOT/frontend/dist/" "$WEB_DIR/"
    chmod -R go-w,a+rX "$APP_DIR" "$WEB_DIR"
    rm -rf "$stage"

    systemctl enable "$SERVICE_NAME" >/dev/null 2>&1
    systemctl restart "$SERVICE_NAME"
    note "Waiting for the API (the first start runs database migrations)..."
    local i code
    for i in $(seq 1 60); do
        code=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$API_PORT/api/auth/me" || true)
        [ "$code" = 401 ] && { note "API is up."; return; }
        sleep 2
    done
    journalctl -u "$SERVICE_NAME" -n 60 --no-pager || true
    die "The API didn't start. See the log above (journalctl -u $SERVICE_NAME)."
}

# Runs the API's --create-super-admin mode as the service would run (same user and settings); details go on stdin
nginx_setup_super_admin() {
    [ "$SA_SETUP" = y ] || return 0
    info "Setting up the super admin"
    if printf '%s\n' "$SA_EMAIL" "$SA_FIRST" "$SA_LAST" "$SA_PASSWORD" |
        systemd-run --quiet --wait --pipe --collect \
            -p User="$SERVICE_USER" -p Group="$SERVICE_USER" -p WorkingDirectory="$APP_DIR" \
            -p EnvironmentFile="$API_ENV_FILE" -p Environment="HOME=$DATA_DIR DOTNET_NOLOGO=1" \
            "$(command -v dotnet)" "$APP_DIR/ScholarshipApi.dll" --create-super-admin; then
        EX_SUPER_ADMIN_EMAIL=$SA_EMAIL
        nginx_write_deploy_conf
    else
        SA_FAILED=y
        warn "The super admin wasn't set up (see the message above). Re-run the script to try again."
    fi
}

# The part of the nginx server block that serves the app; $1 = y adds HSTS (HTTPS only)
nginx_app_block() {
    cat <<'EOF'
    server_tokens off;

    root /var/www/scholarship-app;
    index index.html;

    # Same headers as frontend/nginx.conf (the Docker image). Locations below must not use add_header, or these stop
    # being inherited.
    add_header Content-Security-Policy "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob:; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'" always;
EOF
    [ "$1" = y ] && echo '    add_header Strict-Transport-Security "max-age=31536000" always;'
    cat <<'EOF'
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header Referrer-Policy "strict-origin-when-cross-origin" always;
    add_header Cache-Control $scholarship_cache_control always;
    add_header Permissions-Policy "camera=(), microphone=(), geolocation=(), payment=()" always;

    # Matches FileUploadLimits (5 MB file)
    client_max_body_size 6m;

    location /api/ {
        proxy_pass http://127.0.0.1:5098;
        proxy_set_header Host $host;
        # The API trusts two forwarding hops (Caddy and nginx under Docker). Here nginx is the only one, so it
        # replaces any X-Forwarded-For the client sent instead of appending to it; otherwise clients could choose
        # the IP that rate limiting sees.
        proxy_set_header X-Forwarded-For $remote_addr;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location / {
        try_files $uri $uri/ /index.html;
    }
EOF
}

# $1 = y for the HTTPS config (needs the certificate), n for HTTP only
nginx_site_config() {
    local tls=$1 name="${SITE_HOST:-_}" default_server=''
    [ -z "$SITE_HOST" ] && default_server=' default_server'
    cat <<'EOF'
# Managed by deploy/deploy.sh: re-run it instead of editing this file.

# index.html (the SPA shell) must be revalidated or browsers keep one pointing at the previous deploy's bundles
map $sent_http_content_type $scholarship_cache_control {
    ~^text/html   "no-cache";
    default       "";
}

EOF
    if [ "$tls" = y ]; then
        # HTTPS only: every plain-HTTP request, for this name or any other (an IP address, an old name), is
        # redirected. Only Let's Encrypt's challenge files stay on HTTP, which renewals need.
        echo "server {"
        echo "    listen 80 default_server;"
        echo "    listen [::]:80 default_server;"
        echo "    server_name _;"
        echo "    server_tokens off;"
        echo
        echo "    location ^~ /.well-known/acme-challenge/ {"
        echo "        root $ACME_WEBROOT;"
        echo "    }"
        echo '    location / {'
        echo "        return 301 https://$SITE_HOST\$request_uri;"
        echo '    }'
        echo "}"
        echo
        echo "server {"
        echo "    listen 443 ssl http2;"
        echo "    listen [::]:443 ssl http2;"
        echo "    server_name $name;"
        echo
        echo "    ssl_certificate /etc/letsencrypt/live/$SITE_HOST/fullchain.pem;"
        echo "    ssl_certificate_key /etc/letsencrypt/live/$SITE_HOST/privkey.pem;"
        echo "    ssl_protocols TLSv1.2 TLSv1.3;"
        echo "    ssl_session_cache shared:scholarship_ssl:10m;"
        echo "    ssl_session_timeout 1d;"
        echo
    else
        echo "server {"
        echo "    listen 80$default_server;"
        echo "    listen [::]:80$default_server;"
        echo "    server_name $name;"
        echo
        echo "    location ^~ /.well-known/acme-challenge/ {"
        echo "        root $ACME_WEBROOT;"
        echo "    }"
    fi
    nginx_app_block "$tls"
    echo "}"
}

# Renews the certificate once it's CERT_MAX_AGE_DAYS old: a daily timer checks its age and forces a renewal
# (certbot's own timer only renews 30 days before expiry, i.e. at 60 days for Let's Encrypt's 90-day certificates,
# and stays as a fallback). The renewal reuses the deploy hook stored with the certificate, which reloads nginx.
nginx_setup_cert_renewal() {
    if [ "$USE_TLS" != y ]; then
        systemctl disable --now "$CERT_RENEW_UNIT.timer" >/dev/null 2>&1 || true
        rm -f "/etc/systemd/system/$CERT_RENEW_UNIT.timer" "/etc/systemd/system/$CERT_RENEW_UNIT.service" "$CERT_RENEW_SCRIPT"
        systemctl daemon-reload
        return
    fi
    write_file "$CERT_RENEW_SCRIPT" 755 <<EOF
#!/bin/sh
# Managed by deploy/deploy.sh: renews $SITE_HOST's certificate once it is $CERT_MAX_AGE_DAYS days old.
set -eu
cert=/etc/letsencrypt/live/$SITE_HOST/cert.pem
[ -f "\$cert" ] || exit 0
issued=\$(date -d "\$(openssl x509 -noout -startdate -in "\$cert" | cut -d= -f2)" +%s)
age_days=\$(( (\$(date +%s) - issued) / 86400 ))
if [ "\$age_days" -lt $CERT_MAX_AGE_DAYS ]; then
    echo "Certificate for $SITE_HOST is \$age_days days old; renewing at $CERT_MAX_AGE_DAYS."
    exit 0
fi
echo "Certificate for $SITE_HOST is \$age_days days old: renewing."
exec certbot renew --cert-name "$SITE_HOST" --force-renewal --non-interactive
EOF
    write_file "/etc/systemd/system/$CERT_RENEW_UNIT.service" 644 <<EOF
# Managed by deploy/deploy.sh
[Unit]
Description=Renew the Scholarship App certificate every $CERT_MAX_AGE_DAYS days
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=$CERT_RENEW_SCRIPT
EOF
    write_file "/etc/systemd/system/$CERT_RENEW_UNIT.timer" 644 <<EOF
# Managed by deploy/deploy.sh. Checks daily, so a failed renewal is retried the next day.
[Unit]
Description=Daily check of the Scholarship App certificate's age

[Timer]
OnCalendar=daily
RandomizedDelaySec=1h
Persistent=true

[Install]
WantedBy=timers.target
EOF
    systemctl daemon-reload
    systemctl enable --now "$CERT_RENEW_UNIT.timer" >/dev/null 2>&1
    note "Certificate renews every $CERT_MAX_AGE_DAYS days ($CERT_RENEW_UNIT.timer)."
}

nginx_configure_web() {
    info "Configuring nginx"
    # Ubuntu's stock site would otherwise answer plain HTTP for other host names (both configs use default_server)
    rm -f /etc/nginx/sites-enabled/default
    ln -sf "$NGINX_SITE" /etc/nginx/sites-enabled/scholarship-app
    systemctl enable --now nginx >/dev/null 2>&1

    if [ "$USE_TLS" = y ] && ! cert_valid; then
        # Missing or expired: serve HTTP first so Let's Encrypt can reach the challenge files, then switch to HTTPS
        local account=(--email "$ACME_EMAIL")
        [ -z "$ACME_EMAIL" ] && account=(--register-unsafely-without-email)
        nginx_site_config n | write_file "$NGINX_SITE" 644
        nginx -t && systemctl reload nginx
        note "Requesting a certificate for $SITE_HOST..."
        certbot certonly --webroot -w "$ACME_WEBROOT" -d "$SITE_HOST" "${account[@]}" --keep-until-expiring \
            --agree-tos --non-interactive --deploy-hook "systemctl reload nginx" && cert_valid ||
            die "Certificate request failed: check that $SITE_HOST points at this server and port 80 is reachable. The site is running on HTTP until a certificate is issued; re-run to try again."
    fi
    nginx_site_config "$USE_TLS" | write_file "$NGINX_SITE" 644
    nginx -t
    systemctl reload nginx
    nginx_setup_cert_renewal

    if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -q 'Status: active'; then
        note "ufw is active: allowing HTTP/HTTPS$([ "$DEPLOY_ENV" = testing ] && echo " and MySQL (3306)")."
        ufw allow 'Nginx Full' >/dev/null
        if [ "$DEPLOY_ENV" = testing ]; then ufw allow 3306/tcp >/dev/null
        else ufw delete allow 3306/tcp >/dev/null 2>&1 || true
        fi
    fi
}

deploy_nginx() {
    [ -r /etc/os-release ] && . /etc/os-release
    [ "${ID:-}" = ubuntu ] || die "The nginx deployment supports Ubuntu only (use docker elsewhere)."
    if [ "$(id -u)" -ne 0 ]; then
        echo "The nginx deployment installs system packages; re-running with sudo."
        exec sudo -- bash "$SCRIPT_PATH" nginx
    fi
    nginx_load_existing
    nginx_prompts
    nginx_install_packages
    nginx_setup_mysql
    nginx_write_config_files
    nginx_build_and_install
    nginx_configure_web
    nginx_setup_super_admin

    info "Done: $APP_BASE_URL"
    note "API service:   systemctl status $SERVICE_NAME   ·   logs: journalctl -u $SERVICE_NAME -f"
    note "Settings:      $API_ENV_FILE (root only)"
    [ "$STORAGE_PROVIDER" = Local ] && note "Uploads:       $DATA_DIR/uploads (back this up with the database)"
    if [ "$DEPLOY_ENV" = testing ]; then
        note "MySQL root:    any host, port 3306; password in $MYSQL_ROOT_FILE"
    else
        note "MySQL root:    localhost only: sudo mysql $DB_NAME"
    fi
    note "Redeploy:      git pull && sudo deploy/deploy.sh nginx"
    super_admin_done_note
}

# ---------------------------------------------------------------------------------------------------------------------
# Docker (Ubuntu, macOS)
# ---------------------------------------------------------------------------------------------------------------------

DOCKER=(docker)
compose() { (cd "$REPO_ROOT" && "${DOCKER[@]}" compose "$@"); }

docker_install_ubuntu() {
    info "Installing Docker Engine (download.docker.com)"
    local codename
    codename=$(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}")
    sudo apt-get update -q
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -q ca-certificates curl
    sudo install -m 0755 -d /etc/apt/keyrings
    sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
    sudo chmod a+r /etc/apt/keyrings/docker.asc
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $codename stable" |
        sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
    sudo apt-get update -q
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -q \
        docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    sudo systemctl enable --now docker
}

docker_install_macos() {
    local yes
    command -v brew >/dev/null 2>&1 ||
        die "Install Docker Desktop (https://www.docker.com/products/docker-desktop/), start it, then re-run this script."
    ask_yn yes "Docker isn't installed. Install Docker Desktop with Homebrew?" y
    [ "$yes" = y ] || die "Install Docker Desktop, start it, then re-run this script."
    brew install --cask docker-desktop || brew install --cask docker
}

ensure_docker() {
    info "Checking Docker"
    local os
    os=$(uname -s)
    if ! command -v docker >/dev/null 2>&1; then
        if [ "$os" = Darwin ]; then docker_install_macos
        elif [ -r /etc/os-release ] && [ "$(. /etc/os-release && echo "$ID")" = ubuntu ]; then docker_install_ubuntu
        else die "Install Docker Engine with the compose plugin, then re-run this script."
        fi
    fi
    if ! docker info >/dev/null 2>&1; then
        if [ "$os" = Darwin ]; then
            note "Starting Docker Desktop..."
            open -a Docker || true
            local i
            for i in $(seq 1 60); do docker info >/dev/null 2>&1 && break; sleep 3; done
            docker info >/dev/null 2>&1 || die "Docker Desktop didn't start. Open it, finish its setup, then re-run."
        elif sudo docker info >/dev/null 2>&1; then
            # Not in the docker group (yet): use sudo for docker commands
            DOCKER=(sudo docker)
        else
            die "Docker is installed but not running (sudo systemctl start docker)."
        fi
    fi
    if ! "${DOCKER[@]}" compose version >/dev/null 2>&1; then
        [ "$os" = Linux ] && sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -q docker-compose-plugin
        "${DOCKER[@]}" compose version >/dev/null 2>&1 || die "Docker Compose v2 (docker compose) is required."
    fi
    note "$("${DOCKER[@]}" --version)"
}

docker_load_existing() {
    local s=$REPO_ROOT/secrets dotenv=$REPO_ROOT/.env storage conn
    EX_DEPLOY_ENV=$(env_get "$dotenv" DEPLOY_ENV)
    EX_SITE_ADDRESS=$(env_get "$dotenv" SITE_ADDRESS)
    EX_ACME_EMAIL=$(env_get "$dotenv" ACME_EMAIL)
    EX_APP_BASE_URL=$(env_get "$dotenv" APP_BASE_URL)
    EX_HTTP_PORT=$(env_get "$dotenv" HTTP_PORT)
    EX_HTTPS_PORT=$(env_get "$dotenv" HTTPS_PORT)
    EX_MYSQL_PORT=$(env_get "$dotenv" MYSQL_PORT)
    EX_SUPER_ADMIN_EMAIL=$(env_get "$dotenv" SUPER_ADMIN_EMAIL)
    EX_JWT=$(env_get "$s/jwt.env" Jwt__Key)
    EX_TWOFACTOR=$(env_get "$s/twofactor.env" TwoFactor__EncryptionKey)
    EX_EMAIL_FROM=$(env_get "$s/email.env" Email__From)
    EX_RESEND=$(env_get "$s/email.env" Email__Resend__ApiKey)
    # Older setups kept R2 settings in r2.env; they move into storage.env
    storage=$s/storage.env
    [ -f "$storage" ] || storage=$s/r2.env
    EX_STORAGE_PROVIDER=$(env_get "$storage" Storage__Provider)
    EX_R2_ACCOUNT_ID=$(env_get "$storage" Storage__R2__AccountId)
    EX_R2_ACCESS_KEY_ID=$(env_get "$storage" Storage__R2__AccessKeyId)
    EX_R2_SECRET=$(env_get "$storage" Storage__R2__SecretAccessKey)
    EX_R2_BUCKET=$(env_get "$storage" Storage__R2__BucketName)
    EX_AZURE_CONNECTION=$(env_get "$storage" Storage__AzureBlobConnectionString)
    EX_AZURE_CONTAINER=$(env_get "$storage" Storage__ContainerName)
    EX_DB_ROOT_PASSWORD=$(env_get "$s/mysql.env" MYSQL_ROOT_PASSWORD)
    conn=$(env_get "$s/db.env" ConnectionStrings__Default)
    EX_DB_APP_PASSWORD=''
    case "$conn" in *"User=$DB_USER;"*) EX_DB_APP_PASSWORD=$(conn_password "$conn") ;; esac
}

docker_prompts() {
    prompt_environment

    info "Web address"
    note "With a domain name (DNS pointing at this machine, ports 80 and 443 reachable from the internet), Caddy"
    note "gets a Let's Encrypt certificate automatically. Leave it blank to serve plain HTTP (local testing)."
    local ex_host=''
    case "$EX_SITE_ADDRESS" in ''|:*) ;; *) ex_host=$EX_SITE_ADDRESS ;; esac
    ask_valid SITE_HOST "Domain name" "$ex_host" valid_domain
    # Caddy's config always names an ACME account email, even when serving plain HTTP
    ask_valid ACME_EMAIL "Email for Let's Encrypt certificate notices" "$EX_ACME_EMAIL" valid_email
    if [ -n "$SITE_HOST" ]; then
        SITE_ADDRESS=$SITE_HOST
        HTTP_PORT=80 HTTPS_PORT=443
        APP_BASE_URL="https://$SITE_HOST"
    else
        [ "$DEPLOY_ENV" = production ] && warn "Production without HTTPS sends passwords and applicants' data unencrypted."
        SITE_ADDRESS=':80'
        ask_valid HTTP_PORT "HTTP port on this machine" "${EX_HTTP_PORT:-80}" valid_port
        HTTPS_PORT=${EX_HTTPS_PORT:-443}
        local default_url="http://localhost"
        [ "$HTTP_PORT" != 80 ] && default_url="http://localhost:$HTTP_PORT"
        case "$EX_APP_BASE_URL" in http://*) default_url=$EX_APP_BASE_URL ;; esac
        note "The address used in emailed links (verification, invites, reference requests)."
        ask_valid APP_BASE_URL "Site URL" "$default_url" valid_url
    fi

    info "Database (MySQL 8 container)"
    if [ "$DEPLOY_ENV" = testing ]; then
        MYSQL_BIND=0.0.0.0
        note "Testing: MySQL is published on all interfaces; root can sign in from any host."
    else
        MYSQL_BIND=127.0.0.1
        note "Production: MySQL is published on 127.0.0.1 only and root is limited to inside the container."
    fi
    ask_valid MYSQL_PORT "MySQL port on this machine" "${EX_MYSQL_PORT:-3306}" valid_port
    if [ -n "$EX_DB_ROOT_PASSWORD" ]; then
        DB_ROOT_PASSWORD=$EX_DB_ROOT_PASSWORD
        note "Keeping the MySQL root password in secrets/mysql.env (MySQL only reads it when the volume is created)."
    else
        ask_secret DB_ROOT_PASSWORD "MySQL root password" "" generate valid_db_password gen_password
    fi
    note "The app connects as '$DB_USER' with rights on $DB_NAME only."
    ask_secret DB_APP_PASSWORD "Password for the $DB_USER MySQL user" "$EX_DB_APP_PASSWORD" generate valid_db_password gen_password

    prompt_app_settings
    prompt_super_admin

    info "Summary"
    note "Deployment:   Docker Compose ($DEPLOY_ENV)"
    note "Site URL:     $APP_BASE_URL"
    note "HTTPS:        $([ -n "$SITE_HOST" ] && echo "Let's Encrypt for $SITE_HOST (Caddy)" || echo "no")"
    note "MySQL:        $MYSQL_BIND:$MYSQL_PORT, root $([ "$DEPLOY_ENV" = testing ] && echo "from any host" || echo "localhost only")"
    note "Uploads:      $STORAGE_PROVIDER$([ "$STORAGE_PROVIDER" = Local ] && echo " (uploads_data volume)")"
    note "Email:        $([ -n "$RESEND_KEY" ] && echo "Resend, from $EMAIL_FROM" || echo "not sent (logged only)")"
    note "Super admin:  $(super_admin_summary)"
    confirm_or_exit
}

docker_write_dotenv() {
    {
        echo "# Written by deploy/deploy.sh: re-run it to change these. Read by docker-compose.yml."
        echo "DEPLOY_ENV=$DEPLOY_ENV"
        echo "SITE_ADDRESS=$SITE_ADDRESS"
        echo "ACME_EMAIL=$ACME_EMAIL"
        echo "APP_BASE_URL=$APP_BASE_URL"
        echo "HTTP_PORT=$HTTP_PORT"
        echo "HTTPS_PORT=$HTTPS_PORT"
        echo "MYSQL_BIND=$MYSQL_BIND"
        echo "MYSQL_PORT=$MYSQL_PORT"
        echo "# The super admin set up by the script (only the email; used as the default next time)"
        echo "SUPER_ADMIN_EMAIL=$EX_SUPER_ADMIN_EMAIL"
    } | write_file "$REPO_ROOT/.env" 600
}

docker_write_files() {
    info "Writing .env and secrets/*.env"
    local s=$REPO_ROOT/secrets root_host=localhost
    [ "$DEPLOY_ENV" = testing ] && root_host=%
    mkdir -p "$s"
    docker_write_dotenv
    printf '# Written by deploy/deploy.sh\nJwt__Key=%s\n' "$JWT_KEY" | write_file "$s/jwt.env" 600
    printf '# Written by deploy/deploy.sh\nTwoFactor__EncryptionKey=%s\n' "$TWOFACTOR_KEY" | write_file "$s/twofactor.env" 600
    {
        echo "# Written by deploy/deploy.sh. MySQL only reads these when it first creates its data volume; the script"
        echo "# applies the root host and app user to an existing database itself."
        echo "MYSQL_ROOT_PASSWORD=$DB_ROOT_PASSWORD"
        echo "MYSQL_DATABASE=$DB_NAME"
        echo "MYSQL_ROOT_HOST=$root_host"
    } | write_file "$s/mysql.env" 600
    printf '# Written by deploy/deploy.sh\nConnectionStrings__Default=Server=mysql;Database=%s;User=%s;Password=%s\n' \
        "$DB_NAME" "$DB_USER" "$DB_APP_PASSWORD" | write_file "$s/db.env" 600
    printf '# Written by deploy/deploy.sh\nEmail__From=%s\nEmail__Resend__ApiKey=%s\n' "$EMAIL_FROM" "$RESEND_KEY" |
        write_file "$s/email.env" 600
    { echo "# Written by deploy/deploy.sh"; storage_env_lines; } | write_file "$s/storage.env" 600
    if [ -f "$s/r2.env" ]; then
        mv -f "$s/r2.env" "$s/r2.env.old"
        note "secrets/r2.env is no longer used (its settings are in storage.env); renamed it to r2.env.old."
    fi
}

# Applies the app user and the root host rule; works on new and existing data volumes alike
docker_setup_mysql() {
    info "Starting MySQL"
    compose up -d --wait mysql
    local sql i
    sql="CREATE DATABASE IF NOT EXISTS \`$DB_NAME\`;
CREATE USER IF NOT EXISTS '$DB_USER'@'%' IDENTIFIED BY '$DB_APP_PASSWORD';
ALTER USER '$DB_USER'@'%' IDENTIFIED BY '$DB_APP_PASSWORD';
GRANT ALL PRIVILEGES ON \`$DB_NAME\`.* TO '$DB_USER'@'%';"
    if [ "$DEPLOY_ENV" = testing ]; then
        sql="$sql
CREATE USER IF NOT EXISTS 'root'@'%' IDENTIFIED BY '$DB_ROOT_PASSWORD';
ALTER USER 'root'@'%' IDENTIFIED BY '$DB_ROOT_PASSWORD';
GRANT ALL PRIVILEGES ON *.* TO 'root'@'%' WITH GRANT OPTION;"
    else
        sql="$sql
DROP USER IF EXISTS 'root'@'%';"
    fi
    sql="$sql
FLUSH PRIVILEGES;"
    # On a new volume MySQL restarts once after initializing, so retry until the statements go through
    for i in $(seq 1 30); do
        if printf '%s\n' "$sql" | compose exec -T mysql sh -c 'MYSQL_PWD=$MYSQL_ROOT_PASSWORD exec mysql -uroot' 2>/dev/null; then
            note "MySQL users configured."
            return
        fi
        sleep 3
    done
    compose logs --tail 40 mysql || true
    die "Couldn't configure MySQL as root. If the mysql_data volume predates secrets/mysql.env, its root password differs."
}

docker_start() {
    info "Building and starting the containers"
    compose up -d --build --remove-orphans
    note "Waiting for the API (the first start runs database migrations)..."
    local i
    for i in $(seq 1 60); do
        if compose exec -T frontend wget -S -O /dev/null http://backend:8080/api/auth/me 2>&1 | grep -q ' 401'; then
            note "API is up."
            return
        fi
        sleep 3
    done
    compose logs --tail 60 backend || true
    die "The API didn't start. See the log above (docker compose logs backend)."
}

# Runs the API image's --create-super-admin mode in a one-off container; details go on stdin
docker_setup_super_admin() {
    [ "$SA_SETUP" = y ] || return 0
    info "Setting up the super admin"
    if printf '%s\n' "$SA_EMAIL" "$SA_FIRST" "$SA_LAST" "$SA_PASSWORD" |
        compose run --rm -T --no-deps backend --create-super-admin; then
        EX_SUPER_ADMIN_EMAIL=$SA_EMAIL
        docker_write_dotenv
    else
        SA_FAILED=y
        warn "The super admin wasn't set up (see the message above). Re-run the script to try again."
    fi
}

deploy_docker() {
    command -v openssl >/dev/null 2>&1 || die "openssl is required (sudo apt-get install openssl)."
    ensure_docker
    docker_load_existing
    docker_prompts
    docker_write_files
    docker_setup_mysql
    docker_start
    docker_setup_super_admin

    local dc="docker compose"
    [ "${DOCKER[0]}" = sudo ] && dc="sudo docker compose"
    info "Done: $APP_BASE_URL"
    [ -n "$SITE_HOST" ] && note "Caddy requests the HTTPS certificate on the first visit; give it a minute."
    note "Status / logs: $dc ps   ·   $dc logs -f backend"
    note "Settings:      .env and secrets/*.env (gitignored)"
    if [ "$DEPLOY_ENV" = testing ]; then
        note "MySQL root:    any host, port $MYSQL_PORT; password in secrets/mysql.env"
    else
        note "MySQL root:    inside the container only: $dc exec mysql sh -c 'mysql -uroot -p\"\$MYSQL_ROOT_PASSWORD\" $DB_NAME'"
    fi
    note "Redeploy:      git pull && deploy/deploy.sh docker"
    super_admin_done_note
}

# ---------------------------------------------------------------------------------------------------------------------

main() {
    local mode=${1-}
    case "$mode" in
        -h|--help) sed -n '2,10p' "$SCRIPT_PATH" | sed 's/^# \{0,1\}//'; exit 0 ;;
        nginx|docker|'') ;;
        *) die "Unknown option '$mode'. Usage: deploy/deploy.sh [nginx|docker]" ;;
    esac
    if [ -z "$mode" ]; then
        if [ "$(uname -s)" = Linux ] && [ -r /etc/os-release ] && [ "$(. /etc/os-release && echo "$ID")" = ubuntu ]; then
            ask_choice mode "How should the Scholarship App be deployed?" nginx \
                "nginx|nginx + systemd service on this Ubuntu server (MySQL installed locally)" \
                "docker|Docker Compose (Caddy, nginx, API and MySQL containers)"
        else
            mode=docker
            echo "Deploying with Docker Compose (the nginx deployment is Ubuntu-only)."
        fi
    fi
    if [ "$mode" = nginx ]; then deploy_nginx; else deploy_docker; fi
}

main "$@"
