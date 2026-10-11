using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SqlKata.Execution;
using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class AuthService(QueryFactory db, IConfiguration config, IEmailService email, ILogger<AuthService> logger) : IAuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await db.Query("Users").Where("Email", request.Email).FirstOrDefaultAsync<User>();
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ArgumentException("Invalid email or password.");

        if (user.EmailVerifiedAt is null)
            throw new EmailNotVerifiedException(user.Email);

        var permissions = await GetPermissionsForRoleAsync(user.Role);
        return BuildAuthResponse(user, permissions);
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var exists = await db.Query("Users").Where("Email", request.Email).ExistsAsync();
        if (exists) throw new ArgumentException("Email is already registered.");

        string role = "applicant";
        string? schoolId = null;
        string? inviteId = null;

        if (!string.IsNullOrEmpty(request.InviteToken))
        {
            var invite = await db.Query("SchoolAdminInvites")
                .Where("Token", request.InviteToken)
                .WhereNull("UsedAt")
                .Where("ExpiresAt", ">", DateTime.UtcNow)
                .FirstOrDefaultAsync<dynamic>()
                ?? throw new ArgumentException("Invite link is invalid or has expired.");

            if (string.IsNullOrWhiteSpace(request.SchoolName))
                throw new ArgumentException("School name is required.");

            var newSchoolId = Guid.NewGuid().ToString();
            await db.Query("Schools").InsertAsync(new
            {
                Id = newSchoolId,
                Name = request.SchoolName.Trim(),
                AddressLine1 = request.SchoolAddressLine1?.Trim() ?? string.Empty,
                AddressLine2 = request.SchoolAddressLine2?.Trim() ?? string.Empty,
                City = request.SchoolCity?.Trim() ?? string.Empty,
                State = request.SchoolState?.Trim() ?? string.Empty,
                Zip = request.SchoolZip?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            role = "school_admin";
            schoolId = newSchoolId;
            inviteId = (string)invite.Id;
        }

        var id = Guid.NewGuid().ToString();
        await db.Query("Users").InsertAsync(new
        {
            Id = id,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = role,
            SchoolId = schoolId,
            CreatedAt = DateTime.UtcNow
        });

        if (inviteId is not null)
            await db.Query("SchoolAdminInvites").Where("Id", inviteId).UpdateAsync(new { UsedAt = DateTime.UtcNow, SchoolId = schoolId });

        var user = await db.Query("Users").Where("Id", id).FirstAsync<User>();
        await TrySendVerificationEmailAsync(user);
        return new RegisterResponse { Email = user.Email };
    }

    public async Task<AuthResponse> VerifyEmailAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Verification link is invalid or has expired.");

        var row = await db.Query("EmailVerificationTokens")
            .Where("TokenHash", HashToken(token))
            .WhereNull("UsedAt")
            .Where("ExpiresAt", ">", DateTime.UtcNow)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new ArgumentException("Verification link is invalid or has expired.");

        var userId = (string)row.UserId;
        var now = DateTime.UtcNow;
        await db.Query("EmailVerificationTokens").Where("UserId", userId).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });
        await db.Query("Users").Where("Id", userId).WhereNull("EmailVerifiedAt").UpdateAsync(new { EmailVerifiedAt = now });

        var user = await GetCurrentUserAsync(userId);
        var permissions = await GetPermissionsForRoleAsync(user.Role);
        return BuildAuthResponse(user, permissions);
    }

    // Always succeeds silently so the endpoint can't be used to discover which emails are registered
    public async Task ResendVerificationAsync(string emailAddress)
    {
        var user = await db.Query("Users").Where("Email", emailAddress).FirstOrDefaultAsync<User>();
        if (user is null || user.EmailVerifiedAt is not null) return;

        var recentlySent = await db.Query("EmailVerificationTokens")
            .Where("UserId", user.Id)
            .Where("CreatedAt", ">", DateTime.UtcNow - ResendCooldown)
            .ExistsAsync();
        if (recentlySent) return;

        await TrySendVerificationEmailAsync(user);
    }

    private async Task TrySendVerificationEmailAsync(User user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTime.UtcNow;
        await db.Query("EmailVerificationTokens").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresAt = now + VerificationTokenLifetime,
            CreatedAt = now
        });

        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{baseUrl}/verify-email?token={token}";
        var name = WebUtility.HtmlEncode(user.FirstName);

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">Confirm your email</h2>
              <p>Hi {name},</p>
              <p>Thanks for creating a scholarship account. Please confirm your email address to finish setting it up.</p>
              <p style="margin:28px 0">
                <a href="{link}" style="background:#4361EE;color:#fff;padding:12px 24px;border-radius:12px;text-decoration:none;font-weight:bold">Verify email</a>
              </p>
              <p style="font-size:13px;color:#A3AED0">This link expires in 24 hours. If you didn't create an account, you can ignore this email.</p>
            </div>
            """;
        var text = $"Hi {user.FirstName},\n\nConfirm your email address to finish setting up your scholarship account:\n{link}\n\nThis link expires in 24 hours. If you didn't create an account, you can ignore this email.";

        // A delivery failure shouldn't fail registration; the user can request a new link from the sign-in page
        try
        {
            await email.SendAsync(user.Email, "Confirm your email address", html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification email to user {UserId}", user.Id);
        }
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLower();
    }

    public async Task<InviteInfoResponse> GetInviteAsync(string token)
    {
        var row = await db.Query("SchoolAdminInvites")
            .Select("ExpiresAt", "UsedAt")
            .Where("Token", token)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new KeyNotFoundException("Invite not found.");

        if (row.UsedAt is not null)
            throw new ArgumentException("This invite has already been used.");

        if ((DateTime)row.ExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("This invite has expired.");

        return new InviteInfoResponse { ExpiresAt = (DateTime)row.ExpiresAt };
    }

    public async Task<User> GetCurrentUserAsync(string userId)
    {
        var user = await db.Query("Users").Where("Id", userId).FirstOrDefaultAsync<User>();
        return user ?? throw new KeyNotFoundException("User not found.");
    }

    public async Task<IEnumerable<string>> GetPermissionsForRoleAsync(string roleName)
    {
        var rows = await db.Query("Permissions as p")
            .Join("RolePermissions as rp", "rp.PermissionId", "p.Id")
            .Join("Roles as r", "r.Id", "rp.RoleId")
            .Where("r.Name", roleName)
            .Select("p.Name")
            .GetAsync<dynamic>();
        return rows.Select(r => (string)r.Name).ToList();
    }

    private AuthResponse BuildAuthResponse(User user, IEnumerable<string> permissions)
    {
        var permList = permissions.ToList();
        var token = GenerateToken(user, permList);
        return new AuthResponse
        {
            Token = token,
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role,
            SchoolId = user.SchoolId,
            Permissions = permList.ToArray()
        };
    }

    private string GenerateToken(User user, List<string> permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(double.Parse(config["Jwt:ExpiresInMinutes"]!));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.Role, user.Role),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };
        if (!string.IsNullOrEmpty(user.SchoolId))
            claims.Add(new Claim("schoolId", user.SchoolId));
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
