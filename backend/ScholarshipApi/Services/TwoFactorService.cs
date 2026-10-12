using System.Net;
using System.Security.Cryptography;
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

    public async Task<TwoFactorStatusResponse> GetStatusAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        var remaining = await db.Query("TwoFactorRecoveryCodes").Where("UserId", userId).WhereNull("UsedAt").CountAsync<int>();
        return new TwoFactorStatusResponse
        {
            Enabled = user.TotpEnabledAt is not null,
            RecoveryCodesRemaining = user.TotpEnabledAt is null ? 0 : remaining
        };
    }

    public async Task<TwoFactorSetupResponse> BeginSetupAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        if (user.TotpEnabledAt is not null)
            throw new ArgumentException("Two-factor authentication is already turned on.");

        // Held as pending until the user proves their app generates matching codes
        var secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        await db.Query("Users").Where("Id", userId).UpdateAsync(new { TotpPendingSecret = protector.Protect(secret, userId) });

        var issuer = config["App:Name"] ?? "Scholarship Portal";
        var label = Uri.EscapeDataString($"{issuer}:{user.Email}");
        var uri = $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        return new TwoFactorSetupResponse { Secret = secret, OtpauthUri = uri };
    }

    public async Task<TwoFactorEnableResponse> EnableAsync(string userId, string code)
    {
        var user = await GetUserAsync(userId);
        if (user.TotpEnabledAt is not null)
            throw new ArgumentException("Two-factor authentication is already turned on.");
        if (user.TotpPendingSecret is null)
            throw new ArgumentException("Start setup again to get a new QR code.");

        if (!TryMatchTotp(protector.Unprotect(user.TotpPendingSecret, userId), code, out var step))
            throw new ArgumentException("That code doesn't match. Check the time on your device and try again.");

        var enabled = await db.StatementAsync(
            """
            UPDATE Users SET TotpSecret = TotpPendingSecret, TotpPendingSecret = NULL, TotpEnabledAt = @Now, TotpLastUsedStep = @Step
            WHERE Id = @Id AND TotpPendingSecret = @Secret AND TotpEnabledAt IS NULL
            """,
            new { Id = userId, Secret = user.TotpPendingSecret, Step = step, Now = DateTime.UtcNow });
        if (enabled == 0)
            throw new ArgumentException("Start setup again to get a new QR code.");

        var codes = await ReplaceRecoveryCodesAsync(userId);
        await TrySendNoticeAsync(user, "Two-factor authentication turned on",
            "Two-factor authentication was just turned on for your scholarship account. You'll now enter a code from your authenticator app when you sign in.");
        return new TwoFactorEnableResponse { RecoveryCodes = codes };
    }

    public async Task DisableAsync(string userId, string password)
    {
        var user = await GetUserAsync(userId);
        RequirePassword(user, password);
        if (user.TotpEnabledAt is null) return;

        await ClearAsync(userId);
        await TrySendNoticeAsync(user, "Two-factor authentication turned off",
            "Two-factor authentication was just turned off for your scholarship account. Signing in now only needs your password.");
    }

    public async Task<TwoFactorEnableResponse> RegenerateRecoveryCodesAsync(string userId, string password)
    {
        var user = await GetUserAsync(userId);
        RequirePassword(user, password);
        if (user.TotpEnabledAt is null)
            throw new ArgumentException("Two-factor authentication is not turned on.");

        var codes = await ReplaceRecoveryCodesAsync(userId);
        await TrySendNoticeAsync(user, "New recovery codes created",
            "A new set of two-factor recovery codes was just created for your scholarship account. Your old recovery codes no longer work.");
        return new TwoFactorEnableResponse { RecoveryCodes = codes };
    }

    // For staff helping a user who has lost both their authenticator and recovery codes
    public async Task ResetByAdminAsync(string userId)
    {
        var user = await GetUserAsync(userId);
        if (user.TotpEnabledAt is null && user.TotpPendingSecret is null) return;

        await ClearAsync(userId);
        if (user.TotpEnabledAt is not null)
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
            TotpLastUsedStep = (long?)null
        });
        await db.Query("TwoFactorRecoveryCodes").Where("UserId", userId).DeleteAsync();
    }

    private static void RequirePassword(User user, string password)
    {
        if (string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new ArgumentException("Password is incorrect.");
    }

    // Accepts either a 6-digit authenticator code or an unused recovery code
    public async Task<bool> VerifyLoginCodeAsync(User user, string code)
    {
        if (user.TotpEnabledAt is null || user.TotpSecret is null) return false;

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

    private static bool TryMatchTotp(string secret, string code, out long step)
    {
        step = 0;
        code = (code ?? "").Replace(" ", "");
        if (code.Length != 6 || !code.All(char.IsAsciiDigit)) return false;

        // Allows one 30-second step of clock drift either way
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code, out step, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

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
