using System.Reflection;
using System.Security.Claims;
using System.Text;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using ScholarshipApi.Authorization;
using ScholarshipApi.Data;
using ScholarshipApi.Middleware;
using ScholarshipApi.Services;
using SqlKata.Execution;

var builder = WebApplication.CreateBuilder(args);

// Secrets come from secrets/*.env (Docker) or k8s Secrets; only appsettings.Development.json carries dev values
var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:Default must be configured (see secrets/db.env.example).");

// The key signs every session token, so a missing, short or published key would let anyone mint an admin token
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32 || jwtKey.StartsWith("REPLACE_", StringComparison.Ordinal))
    throw new InvalidOperationException("Jwt:Key must be a random secret of at least 32 bytes (see secrets/jwt.env.example).");

// Run DbUp migrations before the host starts. EnsureDatabase connects to the `mysql` schema, which the app's
// scholarship user can't read (the deploy scripts create the database), so only use it when the database is missing.
try
{
    using var probe = new MySqlConnection(connectionString);
    probe.Open();
}
catch (MySqlException e) when (e.ErrorCode == MySqlErrorCode.UnknownDatabase)
{
    EnsureDatabase.For.MySqlDatabase(connectionString);
}

var upgrader = DeployChanges.To
    .MySqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

var migrationResult = upgrader.PerformUpgrade();
if (!migrationResult.Successful)
    throw new Exception("Migration failed", migrationResult.Error);

// SqlKata (registers IDbConnectionFactory + scoped QueryFactory)
builder.Services.AddSqlKata();

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            // Account changes bump Users.SessionVersion (see SessionVersion), ending every session issued before them.
            // Deleted users fail here too.
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.Principal?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
                var tokenVersion = context.Principal?.FindFirstValue(SessionVersion.Claim);
                var db = context.HttpContext.RequestServices.GetRequiredService<QueryFactory>();
                var current = userId is null ? null
                    : await db.Query("Users").Where("Id", userId).Select("SessionVersion", "DeactivatedAt").FirstOrDefaultAsync<ScholarshipApi.Models.User>();
                if (current is null || current.DeactivatedAt is not null || tokenVersion != current.SessionVersion.ToString())
                {
                    context.Fail("This session has ended. Please sign in again.");
                    return;
                }

                // An impersonation ends if the super admin behind it loses the role or has their sessions ended
                var impersonatorId = context.Principal?.FindFirstValue(AppRoles.ImpersonatorClaim);
                if (impersonatorId is null) return;
                var impersonator = await db.Query("Users").Where("Id", impersonatorId)
                    .Select("Role", "SessionVersion").FirstOrDefaultAsync<ScholarshipApi.Models.User>();
                if (impersonator is null || impersonator.Role != AppRoles.SuperAdmin
                    || context.Principal?.FindFirstValue(AppRoles.ImpersonatorSessionClaim) != impersonator.SessionVersion.ToString())
                    context.Fail("This impersonation has ended. Please sign in again.");
            }
        };
    });

builder.Services.AddAuthorization(RolePolicies.Configure);
builder.Services.AddRateLimiter(RateLimitPolicies.Configure);

// Requests reach the API through two proxies: Caddy (or the k8s ingress), then the frontend's nginx.
// The API port isn't published, so both hops are trusted and the client IP is the one before them.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 2;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
// Built eagerly so a missing or malformed TwoFactor:EncryptionKey stops startup instead of failing at sign-in
builder.Services.AddSingleton(new TotpSecretProtector(builder.Configuration));
builder.Services.AddScoped<ApplicationAccess>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IReferenceService, ReferenceService>();
builder.Services.AddHostedService<ApplicationArchiveService>();

// Email: Resend when an API key is configured, otherwise log emails to the console
if (!string.IsNullOrEmpty(builder.Configuration["Email:Resend:ApiKey"]))
    builder.Services.AddHttpClient<IEmailService, ResendEmailService>();
else
    builder.Services.AddSingleton<IEmailService, LogEmailService>();

// File storage: swap between Local, Azure, and Cloudflare R2 via appsettings
var storageProvider = builder.Configuration["Storage:Provider"];
if (storageProvider == "AzureBlob")
    builder.Services.AddScoped<IFileStorageService, AzureBlobStorageService>();
else if (storageProvider == "R2")
{
    builder.Services.AddSingleton<Amazon.S3.IAmazonS3>(sp => R2StorageService.CreateClient(sp.GetRequiredService<IConfiguration>()));
    builder.Services.AddScoped<IFileStorageService, R2StorageService>();
}
else
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", p =>
        p.WithOrigins("http://localhost:5173")
         .AllowAnyHeader()
         .AllowAnyMethod()));

builder.Services.AddControllers();

var app = builder.Build();

// Conditional questions only ever depend on earlier questions. Saving and reordering keep it that way; this tidies
// any condition saved before that rule existed. Idempotent, so it simply runs on every start.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SqlKata.Execution.QueryFactory>();
    foreach (var cycleId in await db.Query("ScholarshipCycles").Select("Id").GetAsync<string>())
    {
        var pruned = await QuestionOrder.PruneConditionsAsync(db, cycleId);
        if (pruned.ConditionsRemovedFrom.Count > 0 || pruned.NowAlwaysShown.Count > 0)
            app.Logger.LogWarning(
                "Cycle {CycleId}: removed conditions that depended on later questions from {Removed}; now always shown: {Shown}",
                cycleId, pruned.ConditionsRemovedFrom, pruned.NowAlwaysShown);
    }
}

// Deploy scripts: set up a super admin (details on stdin), then exit without serving
if (args.Contains(SuperAdminSetup.Argument))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SqlKata.Execution.QueryFactory>();
    Environment.ExitCode = await SuperAdminSetup.RunAsync(db, Console.In, app.Logger);
    return;
}

if (string.IsNullOrEmpty(app.Configuration["Email:Resend:ApiKey"]))
    app.Logger.LogWarning("Email:Resend:ApiKey is not set: emails will be written to the console instead of being sent.");

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
