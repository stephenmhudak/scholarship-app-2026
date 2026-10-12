using System.Net;
using System.Security.Cryptography;
using System.Text;
using OtpNet;
using SqlKata.Execution;
using ScholarshipApi.DTOs.Auth;
using ScholarshipApi.Models;

namespace ScholarshipApi.Services;

public class TwoFactorService(QueryFactory db, IConfiguration config, IEmailService email, TotpSecretProtector protector, ILogger<TwoFactorService> logger) : ITwoFactorService
{
    private const int RecoveryCodeCount = 10;
    // No 0/o, 1/l/i so codes survive being written down
    private const string RecoveryCodeAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    public static readonly TimeSpan EmailCodeLifetime = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan EmailCodeResendCooldown = TimeSpan.FromMinutes(1);

    public async Task<TwoFactorStatusResponse> GetStatusAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        var remaining = await db.Query("TwoFactorRecoveryCodes").Where("UserId", userId).WhereNull("UsedAt").CountAsync<int>();
        return new TwoFactorStatusResponse
        {
            Enabled = user.TwoFactorMethod is not null,
            Method = user.TwoFactorMethod,
            RecoveryCodesRemaining = user.TwoFactorMethod == TwoFactorMethods.Totp ? remaining : 0
        };
    }

    public async Task<TwoFactorSetupResponse> BeginSetupAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        RequireNotEnrolled(user);

        // Held as pending until the user proves their app generates matching codes. Reopening setup reuses it,
        // so a QR code the user already scanned keeps working instead of leaving a dead entry in their app.
        string secret;
        if (user.TotpPendingSecret is not null)
            secret = protector.Unprotect(user.TotpPendingSecret, userId);
        else
        {
            secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
            await db.Query("Users").Where("Id", userId).UpdateAsync(new { TotpPendingSecret = protector.Protect(secret, userId) });
        }

        var issuer = config["App:Name"] ?? "Scholarship Portal";
        var label = Uri.EscapeDataString($"{issuer}:{user.Email}");
        var uri = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        return new TwoFactorSetupResponse { Secret = secret, OtpauthUri = uri };
    }

    public async Task<TwoFactorEnableResponse> EnableAsync(string userId, string code)
    {
        var user = await GetUserAsync(userId);
        RequireNotEnrolled(user);
        if (user.TotpPendingSecret is null)
            throw new ArgumentException("Start setup again to get a new QR code.");

        var pendingSecret = protector.Unprotect(user.TotpPendingSecret, userId);
        if (!TryMatchTotp(pendingSecret, code, out var step))
            throw new ArgumentException(DescribeSetupMismatch(pendingSecret, code, userId));

        var enabled = await db.StatementAsync(
            """
            UPDATE Users SET TotpSecret = TotpPendingSecret, TotpPendingSecret = NULL, TotpEnabledAt = @Now, TotpLastUsedStep = @Step,
                TwoFactorMethod = @Method
            WHERE Id = @Id AND TotpPendingSecret = @Secret AND TwoFactorMethod IS NULL
            """,
            new { Id = userId, Secret = user.TotpPendingSecret, Step = step, Now = DateTime.UtcNow, Method = TwoFactorMethods.Totp });
        if (enabled == 0)
            throw new ArgumentException("Start setup again to get a new QR code.");

        var codes = await ReplaceRecoveryCodesAsync(userId);
        await TrySendNoticeAsync(user, "Two-factor authentication turned on",
            "Two-factor authentication was just turned on for your scholarship account. You'll now enter a code from your authenticator app when you sign in.");
        return new TwoFactorEnableResponse { RecoveryCodes = codes };
    }

    public async Task BeginEmailSetupAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        RequireNotEnrolled(user);

        var now = DateTime.UtcNow;
        if (user.EmailOtpSetupExpiresAt is { } expires && expires - EmailCodeLifetime + EmailCodeResendCooldown > now)
            throw new ArgumentException("A code was just sent. Please wait a minute before requesting another.");

        var code = NewSixDigitCode();
        await db.Query("Users").Where("Id", userId).UpdateAsync(new
        {
            EmailOtpSetupHash = TokenHash.Sha256($"{userId}:{code}"),
            EmailOtpSetupExpiresAt = now + EmailCodeLifetime
        });

        // Setup is interactive, so tell the user if the email didn't go out rather than leave them waiting
        if (!await TrySendCodeAsync(user, "Confirm email sign-in codes", "Enter this code to start receiving sign-in codes by email:", code))
            throw new ArgumentException("We couldn't send the email. Please try again shortly.");
    }

    public async Task EnableEmailAsync(string userId, string code)
    {
        var user = await GetUserAsync(userId);
        RequireNotEnrolled(user);
        if (user.EmailOtpSetupHash is null || user.EmailOtpSetupExpiresAt is null || user.EmailOtpSetupExpiresAt <= DateTime.UtcNow)
            throw new ArgumentException("That code has expired. Send a new one and try again.");

        if (!HashesMatch(TokenHash.Sha256($"{userId}:{NormalizeDigits(code)}"), user.EmailOtpSetupHash))
            throw new ArgumentException("That code doesn't match. Check the latest email we sent.");

        var enabled = await db.StatementAsync(
            """
            UPDATE Users SET TwoFactorMethod = @Method, EmailOtpSetupHash = NULL, EmailOtpSetupExpiresAt = NULL
            WHERE Id = @Id AND EmailOtpSetupHash = @Hash AND TwoFactorMethod IS NULL
            """,
            new { Id = userId, Hash = user.EmailOtpSetupHash, Method = TwoFactorMethods.Email });
        if (enabled == 0)
            throw new ArgumentException("That code has expired. Send a new one and try again.");

        await TrySendNoticeAsync(user, "Two-factor authentication turned on",
            "Two-factor authentication was just turned on for your scholarship account. You'll now enter a code we email you when you sign in.");
    }

    // Puts a fresh code on the sign-in challenge (replacing any earlier one) and emails it
    public async Task IssueEmailLoginCodeAsync(User user, string challengeId)
    {
        var code = NewSixDigitCode();
        await db.Query("TwoFactorChallenges").Where("Id", challengeId).UpdateAsync(new
        {
            EmailCodeHash = TokenHash.Sha256($"{challengeId}:{code}"),
            EmailCodeSentAt = DateTime.UtcNow
        });
        await TrySendCodeAsync(user, "Your sign-in code", "Enter this code to finish signing in to your scholarship account:", code);
    }

    public async Task DisableAsync(string userId, string password)
    {
        var user = await GetUserAsync(userId);
        RequirePassword(user, password);
        if (user.TwoFactorMethod is null) return;

        await ClearAsync(userId);
        await TrySendNoticeAsync(user, "Two-factor authentication turned off",
            "Two-factor authentication was just turned off for your scholarship account. Signing in now only needs your password.");
    }

    public async Task<TwoFactorEnableResponse> RegenerateRecoveryCodesAsync(string userId, string password)
    {
        var user = await GetUserAsync(userId);
        RequirePassword(user, password);
        if (user.TwoFactorMethod != TwoFactorMethods.Totp)
            throw new ArgumentException("Recovery codes are only used with an authenticator app.");

        var codes = await ReplaceRecoveryCodesAsync(userId);
        await TrySendNoticeAsync(user, "New recovery codes created",
            "A new set of two-factor recovery codes was just created for your scholarship account. Your old recovery codes no longer work.");
        return new TwoFactorEnableResponse { RecoveryCodes = codes };
    }

    // For staff helping a user who has lost both their authenticator and recovery codes
    public async Task ResetByAdminAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        if (user.TwoFactorMethod is null && user.TotpPendingSecret is null && user.EmailOtpSetupHash is null) return;

        await ClearAsync(userId);
        if (user.TwoFactorMethod is not null)
            await TrySendNoticeAsync(user, "Two-factor authentication reset",
                "An administrator reset two-factor authentication on your scholarship account, so signing in now only needs your password. You can set it up again from your Account page.");
    }

    private async Task ClearAsync(string userId)
    {
        await db.Query("Users").Where("Id", userId).UpdateAsync(new
        {
            TotpSecret = (string?)null,
            TotpPendingSecret = (string?)null,
            TotpEnabledAt = (DateTime?)null,
            TotpLastUsedStep = (long?)null,
            TwoFactorMethod = (string?)null,
            EmailOtpSetupHash = (string?)null,
            EmailOtpSetupExpiresAt = (DateTime?)null
        });
        await db.Query("TwoFactorRecoveryCodes").Where("UserId", userId).DeleteAsync();
    }

    private static void RequireNotEnrolled(User user)
    {
        if (user.TwoFactorMethod is not null)
            throw new ArgumentException("Two-factor authentication is already turned on. Turn it off first to switch methods.");
    }

    private static void RequirePassword(User user, string password)
    {
        if (string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new ArgumentException("Password is incorrect.");
    }

    // Email method: the code emailed for this challenge. Authenticator method: a 6-digit app code or an unused recovery code.
    public async Task<bool> VerifyLoginCodeAsync(User user, string code, string challengeId)
    {
        if (user.TwoFactorMethod == TwoFactorMethods.Email)
        {
            var row = await db.Query("TwoFactorChallenges").Select("EmailCodeHash").Where("Id", challengeId).FirstOrDefaultAsync<dynamic>();
            var expected = (string?)row?.EmailCodeHash;
            return expected is not null && HashesMatch(TokenHash.Sha256($"{challengeId}:{NormalizeDigits(code)}"), expected);
        }

        if (user.TwoFactorMethod != TwoFactorMethods.Totp || user.TotpSecret is null) return false;

        var normalized = (code ?? "").Replace(" ", "").Replace("-", "").ToLowerInvariant();
        if (normalized.Length == 6 && normalized.All(char.IsAsciiDigit))
        {
            if (!TryMatchTotp(protector.Unprotect(user.TotpSecret, user.Id), normalized, out var step)) return false;

            // Each code works once, even inside its 30-second window
            var advanced = await db.StatementAsync(
                "UPDATE Users SET TotpLastUsedStep = @Step WHERE Id = @Id AND (TotpLastUsedStep IS NULL OR TotpLastUsedStep < @Step)",
                new { Id = user.Id, Step = step });
            return advanced == 1;
        }

        var used = await db.Query("TwoFactorRecoveryCodes")
            .Where("UserId", user.Id)
            .Where("CodeHash", TokenHash.Sha256(normalized))
            .WhereNull("UsedAt")
            .UpdateAsync(new { UsedAt = DateTime.UtcNow });
        return used == 1;
    }

    // Setup only (the user is signed in and checking their own new secret), so a wide search here gives nothing away
    private string DescribeSetupMismatch(string secret, string code, string userId)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        if (totp.VerifyTotp(NormalizeDigits(code), out var matchedStep, new VerificationWindow(previous: 20, future: 20)))
        {
            var offsetSeconds = (matchedStep - DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30) * 30;
            logger.LogWarning("TOTP setup for user {UserId} matched a code {OffsetSeconds}s from server time; phone or server clock is off", userId, offsetSeconds);
            var minutes = Math.Max(1, (int)Math.Round(Math.Abs(offsetSeconds) / 60.0));
            return $"Your phone's clock looks about {minutes} minute{(minutes == 1 ? "" : "s")} off. Turn on automatic date and time on your phone, then try a new code.";
        }
        return "That code doesn't match. Make sure you're reading the newest Scholarship Portal entry in your authenticator app (delete any older ones), then try again.";
    }

    private static bool TryMatchTotp(string secret, string code, out long step)
    {
        step = 0;
        code = (code ?? "").Replace(" ", "");
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) return false;

        // Allows one 30-second step of clock drift either way
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code, out step, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    private static string NewSixDigitCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string NormalizeDigits(string? code) => (code ?? "").Replace(" ", "").Replace("-", "");

    private static bool HashesMatch(string actual, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(actual), Encoding.UTF8.GetBytes(expected));

    private async Task<string[]> ReplaceRecoveryCodesAsync(string userId)
    {
        await db.Query("TwoFactorRecoveryCodes").Where("UserId", userId).DeleteAsync();

        var now = DateTime.UtcNow;
        var codes = new string[RecoveryCodeCount];
        for (var i = 0; i < codes.Length; i++)
        {
            var raw = RandomNumberGenerator.GetString(RecoveryCodeAlphabet, 10);
            codes[i] = $"{raw[..5]}-{raw[5..]}";
            await db.Query("TwoFactorRecoveryCodes").InsertAsync(new
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                CodeHash = TokenHash.Sha256(raw),
                CreatedAt = now
            });
        }
        return codes;
    }

    private async Task<User> GetUserAsync(string userId) =>
        await db.Query("Users").Where("Id", userId).FirstOrDefaultAsync<User>()
            ?? throw new KeyNotFoundException("User not found.");

    private async Task<bool> TrySendCodeAsync(User user, string subject, string intro, string code)
    {
        var name = WebUtility.HtmlEncode(user.FirstName);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">{subject}</h2>
              <p>Hi {name},</p>
              <p>{intro}</p>
              <p style="margin:28px 0;font-size:32px;font-weight:bold;letter-spacing:8px">{code}</p>
              <p style="font-size:13px;color:#A3AED0">This code expires in 10 minutes. If you didn't just try to sign in, someone may know your password: reset it from the sign-in page.</p>
            </div>
            """;
        var text = $"Hi {user.FirstName},\n\n{intro} {code}\n\nThis code expires in 10 minutes. If you didn't just try to sign in, someone may know your password: reset it from the sign-in page.";

        try
        {
            await email.SendAsync(user.Email, subject, html, text);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send two-factor code to user {UserId}", user.Id);
            return false;
        }
    }

    private async Task TrySendNoticeAsync(User user, string subject, string message)
    {
        var name = WebUtility.HtmlEncode(user.FirstName);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#2B3674">
              <h2 style="margin-bottom:8px">{subject}</h2>
              <p>Hi {name},</p>
              <p>{message}</p>
              <p style="font-size:13px;color:#A3AED0">If this wasn't you, reset your password from the sign-in page right away and contact the scholarship administrator.</p>
            </div>
            """;
        var text = $"Hi {user.FirstName},\n\n{message}\n\nIf this wasn't you, reset your password from the sign-in page right away and contact the scholarship administrator.";

        try
        {
            await email.SendAsync(user.Email, subject, html, text);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send two-factor notice to user {UserId}", user.Id);
        }
    }
}
