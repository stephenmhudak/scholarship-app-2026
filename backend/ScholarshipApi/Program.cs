using System.Reflection;
using System.Text;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ScholarshipApi.Authorization;
using ScholarshipApi.Data;
using ScholarshipApi.Middleware;
using ScholarshipApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Run DbUp migrations before the host starts
var connectionString = builder.Configuration.GetConnectionString("Default")!;
EnsureDatabase.For.MySqlDatabase(connectionString);

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
var jwtKey = builder.Configuration["Jwt:Key"]!;
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
    });

builder.Services.AddAuthorization(RolePolicies.Configure);

// Application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<IScoringService, ScoringService>();
builder.Services.AddScoped<IReferenceService, ReferenceService>();

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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
