using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SqlKata.Execution;
using ScholarshipApi.Authorization;
using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class AuthService(QueryFactory db, IConfiguration config, IEmailService email, ITwoFactorService twoFactor, ILogger<AuthService> logger) : IAuthService
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PasswordSetupLifetime = TimeSpan.FromDays(7);
    private const int MaxResetCodeAttempts = 5;
    private static readonly TimeSpan TwoFactorChallengeLifetime = TimeSpan.FromMinutes(5);
    private const int MaxChallengeAttempts = 5;
    private static readonly TimeSpan TwoFactorAttemptWindow = TimeSpan.FromMinutes(15);
    private const int MaxTwoFactorAttemptsPerWindow = 10;

    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await db.Query("Users").Where("Email", request.Email).FirstOrDefaultAsync<User>();
        // An invited account has no password until its setup link is used (and BCrypt can't verify an empty hash)
        if (user is null || !HasPassword(user) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new ArgumentException("Invalid email or password.");

        if (user.EmailVerifiedAt is null)
            throw new EmailNotVerifiedException(user.Email);

        EnsureActive(user);
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
        if (!await twoFactor.VerifyLoginCodeAsync(user, request.Code, challengeId))
            throw new ArgumentException("That code is incorrect. Please try again.");

        var consumed = await db.Query("TwoFactorChallenges").Where("Id", challengeId).WhereNull("UsedAt").UpdateAsync(new { UsedAt = DateTime.UtcNow });
        if (consumed == 0) throw new ArgumentException(expired);

        var permissions = await GetPermissionsForUserAsync(user);
        return BuildAuthResponse(user, permissions);
    }

    // Email-method users can ask for another code while the challenge is still open
    public async Task ResendTwoFactorEmailAsync(string challengeToken)
    {
        var row = await db.Query("TwoFactorChallenges")
            .Where("TokenHash", HashToken(challengeToken ?? ""))
            .WhereNull("UsedAt")
            .Where("ExpiresAt", ">", DateTime.UtcNow)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new ArgumentException("Your sign-in attempt has expired. Please sign in again.");

        var user = await GetCurrentUserAsync((string)row.UserId);
        if (user.TwoFactorMethod != TwoFactorMethods.Email)
            throw new ArgumentException("This account doesn't use email sign-in codes.");

        var now = DateTime.UtcNow;
        if (row.EmailCodeSentAt is DateTime sentAt && sentAt > now - TwoFactorService.EmailCodeResendCooldown)
            throw new ArgumentException("A code was just sent. Please wait a minute before requesting another.");

        var challengeId = (string)row.Id;
        await db.Query("TwoFactorChallenges").Where("Id", challengeId).UpdateAsync(new { ExpiresAt = now + TwoFactorService.EmailCodeLifetime });
        await twoFactor.IssueEmailLoginCodeAsync(user, challengeId);
    }

    public const string DeactivatedMessage = "This account has been deactivated. Contact the scholarship office if you need access.";

    // Deactivated accounts get no session by any route (sign-in, 2FA, links, impersonation)
    public static void EnsureActive(User user)
    {
        if (user.DeactivatedAt is not null) throw new UnauthorizedAccessException(DeactivatedMessage);
    }

    private async Task<LoginResult> SignInOrChallengeAsync(User user)
    {
        EnsureActive(user);
        if (user.TwoFactorMethod is null)
        {
            var permissions = await GetPermissionsForUserAsync(user);
            return new LoginResult { Auth = BuildAuthResponse(user, permissions) };
        }

        var isEmail = user.TwoFactorMethod == TwoFactorMethods.Email;
        var token = NewUrlToken();
        var challengeId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        await db.Query("TwoFactorChallenges").InsertAsync(new
        {
            Id = challengeId,
            UserId = user.Id,
            TokenHash = HashToken(token),
            Attempts = 0,
            // Email can be slow to arrive, so its codes get longer than an app code needs
            ExpiresAt = now + (isEmail ? TwoFactorService.EmailCodeLifetime : TwoFactorChallengeLifetime),
            CreatedAt = now
        });
        if (isEmail) await twoFactor.IssueEmailLoginCodeAsync(user, challengeId);

        return new LoginResult
        {
            Challenge = new TwoFactorChallengeResponse
            {
                ChallengeToken = token,
                Method = user.TwoFactorMethod,
                MaskedEmail = isEmail ? MaskEmail(user.Email) : null
            }
        };
    }

    // j•••@example.com: enough to recognize the inbox without revealing the address to someone who only has the password
    private static string MaskEmail(string address)
    {
        var at = address.IndexOf('@');
        return at <= 0 ? address : $"{address[0]}•••{address[at..]}";
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var emailAddress = InputValidation.Email(request.Email);
        InputValidation.Password(request.Password);
        var firstName = InputValidation.Required(request.FirstName, "First name", 100);
        var lastName = InputValidation.Required(request.LastName, "Last name", 100);

        // Self-registration only ever makes an applicant or a referee; other roles come from invites or admins
        var selfRegisteredRole = request.AccountType switch
        {
            null or "" or "applicant" => "applicant",
            "referee" => "referee",
            _ => throw new ArgumentException("Choose a valid account type."),
        };

        var exists = await db.Query("Users").Where("Email", emailAddress).ExistsAsync();
        if (exists) throw new ArgumentException("Email is already registered.");

        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        if (db.Connection.State != System.Data.ConnectionState.Open) db.Connection.Open();
        using (var transaction = db.Connection.BeginTransaction())
        {
            string role = selfRegisteredRole;
            string? schoolId = null;

            if (!string.IsNullOrEmpty(request.InviteToken))
            {
                var schoolName = InputValidation.Required(request.SchoolName, "School name", 255);
                const string invalidInvite = "Invite link is invalid or has expired.";
                var inviteId = await db.Query("SchoolAdminInvites")
                    .Where("TokenHash", HashToken(request.InviteToken))
                    .WhereNull("UsedAt")
                    .Where("ExpiresAt", ">", now)
                    .Select("Id")
                    .FirstOrDefaultAsync<string>(transaction)
                    ?? throw new ArgumentException(invalidInvite);

                // Claim the invite first and conditionally, so two registrations racing on one link can't both use it.
                // Rolled back with everything else if the registration fails.
                var claimed = await db.StatementAsync(
                    "UPDATE SchoolAdminInvites SET UsedAt = @Now WHERE Id = @Id AND UsedAt IS NULL",
                    new { Id = inviteId, Now = now }, transaction);
                if (claimed == 0) throw new ArgumentException(invalidInvite);

                schoolId = Guid.NewGuid().ToString();
                await db.Query("Schools").InsertAsync(new
                {
                    Id = schoolId,
                    Name = schoolName,
                    AddressLine1 = InputValidation.Optional(request.SchoolAddressLine1, "Address line 1", 500, ""),
                    AddressLine2 = InputValidation.Optional(request.SchoolAddressLine2, "Address line 2", 500, ""),
                    City = InputValidation.Optional(request.SchoolCity, "City", 200, ""),
                    State = InputValidation.Optional(request.SchoolState, "State", 100, ""),
                    Zip = InputValidation.Optional(request.SchoolZip, "Zip", 20, ""),
                    CreatedAt = now
                }, transaction);
                await db.Query("SchoolAdminInvites").Where("Id", inviteId).UpdateAsync(new { SchoolId = schoolId }, transaction);
                role = "school_admin";
            }

            await db.Query("Users").InsertAsync(new
            {
                Id = id,
                Email = emailAddress,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FirstName = firstName,
                LastName = lastName,
                Role = role,
                SchoolId = schoolId,
                CreatedAt = now
            }, transaction);

            transaction.Commit();
        }

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
        if (user is null || user.DeactivatedAt is not null) return;

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
        InputValidation.Password(request.NewPassword);

        var user = await db.Query("Users").Where("Email", request.Email?.Trim() ?? "").FirstOrDefaultAsync<User>()
            ?? throw new ArgumentException(invalidCode);
        if (user.DeactivatedAt is not null) throw new ArgumentException(invalidCode);

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
        await SessionVersion.BumpAsync(db, user.Id);
        // Receiving the code proves the user owns the address, so any outstanding verification links are no longer needed
        await db.Query("Users").Where("Id", user.Id).WhereNull("EmailVerifiedAt").UpdateAsync(new { EmailVerifiedAt = now });
        await db.Query("EmailVerificationTokens").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });
        // A counselor who resets their password instead of using the setup link has set it up just the same
        await db.Query("PasswordSetupTokens").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });

        await TrySendPasswordChangedEmailAsync(user);

        // The reset code only proves access to the inbox, so enrolled users still need their second factor
        return await SignInOrChallengeAsync(await GetCurrentUserAsync(user.Id));
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync(userId);
        if (string.IsNullOrEmpty(request.CurrentPassword) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ArgumentException("Current password is incorrect.");

        InputValidation.Password(request.NewPassword);
        if (request.NewPassword == request.CurrentPassword)
            throw new ArgumentException("New password must be different from your current password.");

        await db.Query("Users").Where("Id", userId).UpdateAsync(new { PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword) });
        await db.Query("PasswordResetCodes").Where("UserId", userId).WhereNull("UsedAt").UpdateAsync(new { UsedAt = DateTime.UtcNow });
        await SessionVersion.BumpAsync(db, userId);

        await TrySendPasswordChangedEmailAsync(user);
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

    // ── Password setup (invited counselors) ──────────────────────────────────

    public static bool HasPassword(User user) => !string.IsNullOrEmpty(user.PasswordHash);

    // Emails a link to create a password. Earlier links stop working. Throws if the email couldn't be sent, so the
    // person inviting knows to try again.
    public async Task SendPasswordSetupEmailAsync(string userId)
    {
        var user = await GetCurrentUserAsync(userId);
        if (HasPassword(user)) throw new ArgumentException("This person has already created their password.");
        if (user.DeactivatedAt is not null) throw new ArgumentException("This account is deactivated. Reactivate it first.");

        var recentlySent = await db.Query("PasswordSetupTokens").Where("UserId", user.Id)
            .Where("CreatedAt", ">", DateTime.UtcNow - ResendCooldown).ExistsAsync();
        if (recentlySent) throw new ArgumentException("An invitation was just sent. Please wait a minute before sending another.");

        var now = DateTime.UtcNow;
        await db.Query("PasswordSetupTokens").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = now });
        var token = NewUrlToken();
        await db.Query("PasswordSetupTokens").InsertAsync(new
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            TokenHash = HashToken(token),
            ExpiresAt = now + PasswordSetupLifetime,
            CreatedAt = now
        });

        var schoolName = string.IsNullOrEmpty(user.SchoolId)
            ? null
            : await db.Query("Schools").Where("Id", user.SchoolId).Select("Name").FirstOrDefaultAsync<string>();
        var role = user.Role == "counselor" ? "a counselor" : "a user";
        var where = schoolName is null ? "" : $" for {schoolName}";
        var baseUrl = (config["App:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{baseUrl}/setup-password?token={token}";

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">Set up your account</h2>
              <p>Hi {WebUtility.HtmlEncode(user.FirstName)},</p>
              <p>You've been added as {role}{WebUtility.HtmlEncode(where)} in the Scholarship Portal. Create your password to start using your account.</p>
              <p style="margin:28px 0">
                <a href="{link}" style="background:#4361EE;color:#fff;padding:12px 24px;border-radius:12px;text-decoration:none;font-weight:bold">Create your password</a>
              </p>
              <p style="font-size:13px;color:#A3AED0">This link expires in 7 days. If it does, ask whoever added you to send a new one.</p>
            </div>
            """;
        var text = $"Hi {user.FirstName},\n\nYou've been added as {role}{where} in the Scholarship Portal. Create your password to start using your account:\n{link}\n\nThis link expires in 7 days. If it does, ask whoever added you to send a new one.";

        try
        {
            await email.SendAsync(user.Email, "Set up your Scholarship Portal account", html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password setup email to user {UserId}", user.Id);
            throw new ArgumentException("We couldn't send the invitation email. Please try again shortly.");
        }
    }

    public async Task<PasswordSetupInfoResponse> GetPasswordSetupAsync(string? token)
    {
        var (_, user) = await FindPasswordSetupAsync(token);
        return new PasswordSetupInfoResponse { Email = user.Email, FirstName = user.FirstName };
    }

    public async Task<LoginResult> SetupPasswordAsync(PasswordSetupRequest request)
    {
        InputValidation.Password(request.NewPassword);
        var (tokenId, user) = await FindPasswordSetupAsync(request.Token);

        var claimed = await db.StatementAsync(
            "UPDATE PasswordSetupTokens SET UsedAt = @Now WHERE Id = @Id AND UsedAt IS NULL",
            new { Id = tokenId, Now = DateTime.UtcNow });
        // Only an account with no password yet: the link can't replace a password someone already chose
        var updated = claimed == 0 ? 0 : await db.StatementAsync(
            """
            UPDATE Users SET PasswordHash = @Hash, EmailVerifiedAt = COALESCE(EmailVerifiedAt, @Now)
            WHERE Id = @Id AND PasswordHash = ''
            """,
            new { Id = user.Id, Hash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword), Now = DateTime.UtcNow });
        if (updated == 0) throw new ArgumentException(PasswordSetupInvalid);

        await db.Query("PasswordSetupTokens").Where("UserId", user.Id).WhereNull("UsedAt").UpdateAsync(new { UsedAt = DateTime.UtcNow });
        // Receiving the link proves they own the address, so it counts as verifying it
        return await SignInOrChallengeAsync(await GetCurrentUserAsync(user.Id));
    }

    private const string PasswordSetupInvalid =
        "This link is invalid, has expired, or was already used. Ask whoever added you to send a new one, or sign in if you've already created your password.";

    private async Task<(string TokenId, User User)> FindPasswordSetupAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException(PasswordSetupInvalid);
        var row = await db.Query("PasswordSetupTokens")
            .Where("TokenHash", HashToken(token))
            .WhereNull("UsedAt")
            .Where("ExpiresAt", ">", DateTime.UtcNow)
            .FirstOrDefaultAsync<dynamic>()
            ?? throw new ArgumentException(PasswordSetupInvalid);
        var user = await GetCurrentUserAsync((string)row.UserId);
        if (HasPassword(user) || user.DeactivatedAt is not null) throw new ArgumentException(PasswordSetupInvalid);
        return ((string)row.Id, user);
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
            .Where("TokenHash", HashToken(token ?? ""))
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

    // What a user's session carries: their role's permissions, plus award_scholarships when they're assigned to award
    // a scholarship (only for showing the page; AwardsController checks the assignment itself on every request)
    public async Task<IEnumerable<string>> GetPermissionsForUserAsync(User user)
    {
        var permissions = (await GetPermissionsForRoleAsync(user.Role)).ToList();
        if (!permissions.Contains("award_scholarships")
            && await db.Query("ScholarshipAwarders").Where("UserId", user.Id).ExistsAsync())
            permissions.Add("award_scholarships");
        return permissions;
    }

    public async Task<IEnumerable<string>> GetPermissionsForRoleAsync(string roleName)
    {
        // Super admins hold every permission, including ones added after the role was set up
        if (roleName == AppRoles.SuperAdmin)
            return (await db.Query("Permissions").Select("Name").GetAsync<string>()).ToList();

        var rows = await db.Query("Permissions as p")
            .Join("RolePermissions as rp", "rp.PermissionId", "p.Id")
            .Join("Roles as r", "r.Id", "rp.RoleId")
            .Where("r.Name", roleName)
            .Select("p.Name")
            .GetAsync<dynamic>();
        return rows.Select(r => (string)r.Name).ToList();
    }

    // A fresh token for a signed-in user whose session version just changed, so their own session carries on
    public async Task<string> IssueTokenAsync(string userId)
    {
        var user = await GetCurrentUserAsync(userId);
        return GenerateToken(user, (await GetPermissionsForUserAsync(user)).ToList());
    }

    // ── Impersonation (super admins) ─────────────────────────────────────────

    // A session as the target user, marked with the super admin behind it. It bypasses the target's password and
    // two-factor, so only super admins can start one, never on another super admin.
    public async Task<AuthResponse> ImpersonateAsync(string superAdminId, string targetUserId)
    {
        var admin = await GetCurrentUserAsync(superAdminId);
        if (admin.Role != AppRoles.SuperAdmin) throw new UnauthorizedAccessException("Only super admins can impersonate users.");
        if (targetUserId == superAdminId) throw new ArgumentException("You can't impersonate yourself.");
        var target = await GetCurrentUserAsync(targetUserId);
        if (target.Role == AppRoles.SuperAdmin) throw new ArgumentException("Super admins can't be impersonated.");

        logger.LogWarning("Super admin {AdminId} ({AdminEmail}) started impersonating user {UserId} ({UserEmail})",
            admin.Id, admin.Email, target.Id, target.Email);
        var response = BuildAuthResponse(target, await GetPermissionsForUserAsync(target), impersonator: admin);
        response.ReturnSession = BuildAuthResponse(admin, await GetPermissionsForUserAsync(admin));
        return response;
    }

    // Back to the super admin's own session
    public async Task<AuthResponse> EndImpersonationAsync(string impersonatorId, string impersonatedUserId)
    {
        var admin = await GetCurrentUserAsync(impersonatorId);
        if (admin.Role != AppRoles.SuperAdmin) throw new UnauthorizedAccessException("Your super admin access has been removed.");
        logger.LogWarning("Super admin {AdminId} ({AdminEmail}) stopped impersonating user {UserId}",
            admin.Id, admin.Email, impersonatedUserId);
        return BuildAuthResponse(admin, await GetPermissionsForUserAsync(admin));
    }

    private AuthResponse BuildAuthResponse(User user, IEnumerable<string> permissions, User? impersonator = null)
    {
        EnsureActive(user);
        var permList = permissions.ToList();
        var token = GenerateToken(user, permList, impersonator);
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
            TwoFactorEnabled = user.TwoFactorMethod is not null,
            ImpersonatorName = impersonator is null ? null : $"{impersonator.FirstName} {impersonator.LastName}".Trim(),
        };
    }

    private string GenerateToken(User user, List<string> permissions, User? impersonator = null)
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
            new("lastName", user.LastName),
            new(SessionVersion.Claim, user.SessionVersion.ToString())
        };
        if (!string.IsNullOrEmpty(user.SchoolId))
            claims.Add(new Claim("schoolId", user.SchoolId));
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));
        if (impersonator is not null)
        {
            claims.Add(new Claim(AppRoles.ImpersonatorClaim, impersonator.Id));
            claims.Add(new Claim(AppRoles.ImpersonatorSessionClaim, impersonator.SessionVersion.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
