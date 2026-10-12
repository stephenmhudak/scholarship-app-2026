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

public class AuthService(QueryFactory db, IConfiguration config, IEmailService email, ITwoFactorService twoFactor, ILogger<AuthService> logger) : IAuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);
    private const int MaxResetCodeAttempts = 5;
    private const int MinPasswordLength = 8;
    private static readonly TimeSpan TwoFactorChallengeLifetime = TimeSpan.FromMinutes(5);
    private const int MaxChallengeAttempts = 5;
    private static readonly TimeSpan TwoFactorAttemptWindow = TimeSpan.FromMinutes(15);
    private const int MaxTwoFactorAttemptsPerWindow = 10;

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await db.Query("Users").Where("Email", request.Email).FirstOrDefaultAsync<User>();
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ArgumentException("Invalid email or password.");

        if (user.EmailVerifiedAt is null)
            throw new EmailNotVerifiedException(user.Email);

        return await SignInOrChallengeAsync(user);
    }

    public async Task<AuthResponse> CompleteTwoFactorLoginAsync(TwoFactorLoginRequest request)
    {
        const string expired = "Your sign-in attempt has expired. Please sign in again.";

        var row = await db.Query("TwoFactorChallenges")
            .Where("TokenHash", HashToken(request.ChallengeToken ?? ""))
            .WhereNull("UsedAt")
            .Where("ExpiresAt", ">", DateTime.UtcNow)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new ArgumentException(expired);
        var challengeId = (string)row.Id;
        var userId = (string)row.UserId;

        // A fresh challenge only needs the password, so also cap guesses per user across challenges
        var recentAttempts = await db.Query("TwoFactorChallenges")
            .Where("UserId", userId)
            .Where("CreatedAt", ">", DateTime.UtcNow - TwoFactorAttemptWindow)
            .SumAsync<decimal>("Attempts");
        if (recentAttempts >= MaxTwoFactorAttemptsPerWindow)
            throw new ArgumentException("Too many incorrect codes. Please wait 15 minutes and try again.");

        var counted = await db.StatementAsync(
            "UPDATE TwoFactorChallenges SET Attempts = Attempts + 1 WHERE Id = @Id AND UsedAt IS NULL AND Attempts < @Max",
            new { Id = challengeId, Max = MaxChallengeAttempts });
        if (counted == 0)
            throw new ArgumentException("Too many incorrect codes. Please sign in again.");

        var user = await GetCurrentUserAsync(userId);
        if (!await twoFactor.VerifyLoginCodeAsync(user, request.Code))
            throw new ArgumentException("That code is incorrect. Please try again.");

        var consumed = await db.Query("TwoFactorChallenges").Where("Id", challengeId).WhereNull("UsedAt").UpdateAsync(new { UsedAt = DateTime.UtcNow });
        if (consumed == 0) throw new ArgumentException(expired);

        var permissions = await GetPermissionsForRoleAsync(user.Role);
        return BuildAuthResponse(user, permissions);
    }

    private async Task<LoginResult> SignInOrChallengeAsync(User user)
    {
        if (user.TotpEnabledAt is null)
        {
            var permissions = await GetPermissionsForRoleAsync(user.Role);
            return new LoginResult { Auth = BuildAuthResponse(user, permissions) };
        }

        var token = NewUrlToken();
        var now = DateTime.UtcNow;
        await db.Query("TwoFactorChallenges").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            Attempts = 0,
            ExpiresAt = now + TwoFactorChallengeLifetime,
            CreatedAt = now
        });
        return new LoginResult { Challenge = new TwoFactorChallengeResponse { ChallengeToken = token } };
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

    public async Task<LoginResult> VerifyEmailAsync(string token)
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

        return await SignInOrChallengeAsync(await GetCurrentUserAsync(userId));
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
        var token = NewUrlToken();
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

    // Always succeeds silently so the endpoint can't be used to discover which emails are registered
    public async Task ForgotPasswordAsync(string emailAddress)
    {
        if (string.IsNullOrWhiteSpace(emailAddress)) return;

        var user = await db.Query("Users").Where("Email", emailAddress.Trim()).FirstOrDefaultAsync<User>();
        if (user is null) return;

        var recentlySent = await db.Query("PasswordResetCodes")
            .Where("UserId", user.Id)
            .Where("CreatedAt", ">", DateTime.UtcNow - ResendCooldown)
            .ExistsAsync();
        if (recentlySent) return;

        // Only the newest code is valid
        var now = DateTime.UtcNow;
        await db.Query("PasswordResetCodes").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        await db.Query("PasswordResetCodes").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            CodeHash = HashResetCode(user.Id, code),
            Attempts = 0,
            ExpiresAt = now + ResetCodeLifetime,
            CreatedAt = now
        });

        var name = WebUtility.HtmlEncode(user.FirstName);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">Reset your password</h2>
              <p>Hi {name},</p>
              <p>Use this code to reset your scholarship account password:</p>
              <p style="margin:28px 0;font-size:32px;font-weight:bold;letter-spacing:8px">{code}</p>
              <p style="font-size:13px;color:#A3AED0">This code expires in 15 minutes and can only be used once. If you didn't ask to reset your password, you can ignore this email.</p>
            </div>
            """;
        var text = $"Hi {user.FirstName},\n\nUse this code to reset your scholarship account password: {code}\n\nThis code expires in 15 minutes and can only be used once. If you didn't ask to reset your password, you can ignore this email.";

        try
        {
            await email.SendAsync(user.Email, "Your password reset code", html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to user {UserId}", user.Id);
        }
    }

    public async Task<LoginResult> ResetPasswordAsync(ResetPasswordWithCodeRequest request)
    {
        const string invalidCode = "That code is invalid or has expired. Request a new one and try again.";
        ValidateNewPassword(request.NewPassword);

        var user = await db.Query("Users").Where("Email", request.Email?.Trim() ?? "").FirstOrDefaultAsync<User>()
            ?? throw new ArgumentException(invalidCode);

        var row = await db.Query("PasswordResetCodes")
            .Where("UserId", user.Id)
            .WhereNull("UsedAt")
            .Where("ExpiresAt", ">", DateTime.UtcNow)
            .Where("Attempts", "<", MaxResetCodeAttempts)
            .OrderByDesc("CreatedAt")
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new ArgumentException(invalidCode);

        // Count the attempt before comparing, conditionally, so parallel guesses can't exceed the limit
        var counted = await db.StatementAsync(
            "UPDATE PasswordResetCodes SET Attempts = Attempts + 1 WHERE Id = @Id AND UsedAt IS NULL AND Attempts < @Max",
            new { Id = (string)row.Id, Max = MaxResetCodeAttempts });
        if (counted == 0 || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(HashResetCode(user.Id, request.Code?.Trim() ?? "")),
                Encoding.UTF8.GetBytes((string)row.CodeHash)))
            throw new ArgumentException(invalidCode);

        var now = DateTime.UtcNow;
        await db.Query("PasswordResetCodes").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });
        await db.Query("Users").Where("Id", user.Id).UpdateAsync(new { PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword) });
        // Receiving the code proves the user owns the address, so any outstanding verification links are no longer needed
        await db.Query("Users").Where("Id", user.Id).WhereNull("EmailVerifiedAt").UpdateAsync(new { EmailVerifiedAt = now });
        await db.Query("EmailVerificationTokens").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });

        await TrySendPasswordChangedEmailAsync(user);

        // The reset code only proves access to the inbox, so enrolled users still need their second factor
        return await SignInOrChallengeAsync(await GetCurrentUserAsync(user.Id));
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync(userId);
        if (string.IsNullOrEmpty(request.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ArgumentException("Current password is incorrect.");

        ValidateNewPassword(request.NewPassword);
        if (request.NewPassword == request.CurrentPassword)
            throw new ArgumentException("New password must be different from your current password.");

        await db.Query("Users").Where("Id", userId).UpdateAsync(new { PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword) });
        await db.Query("PasswordResetCodes").Where("UserId", userId).WhereNull("UsedAt").UpdateAsync(new { UsedAt = DateTime.UtcNow });

        await TrySendPasswordChangedEmailAsync(user);
    }

    private static void ValidateNewPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
    }

    private async Task TrySendPasswordChangedEmailAsync(User user)
    {
        var name = WebUtility.HtmlEncode(user.FirstName);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">Your password was changed</h2>
              <p>Hi {name},</p>
              <p>The password for your scholarship account was just changed.</p>
              <p style="font-size:13px;color:#A3AED0">If this wasn't you, reset your password from the sign-in page right away and contact the scholarship administrator.</p>
            </div>
            """;
        var text = $"Hi {user.FirstName},\n\nThe password for your scholarship account was just changed.\n\nIf this wasn't you, reset your password from the sign-in page right away and contact the scholarship administrator.";

        try
        {
            await email.SendAsync(user.Email, "Your password was changed", html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password changed email to user {UserId}", user.Id);
        }
    }

    // Salted with the user id so identical codes for different users don't share a hash
    private static string HashResetCode(string userId, string code) => HashToken($"{userId}:{code}");

    private static string HashToken(string token) => TokenHash.Sha256(token);

    private static string NewUrlToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

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
            Permissions = permList.ToArray(),
            TwoFactorEnabled = user.TotpEnabledAt is not null
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
