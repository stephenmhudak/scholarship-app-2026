using System.Net.Mail;

namespace ScholarshipApi.Services;

/// <summary>
/// Shared checks for user-supplied values. Each throws <see cref="ArgumentException"/> (→ 400) with a
/// message fit to show the user, and returns the trimmed value to store.
/// </summary>
public static class InputValidation
{
    public const int MinPasswordLength = 8;
    // BCrypt only uses the first 72 bytes, so a longer password would silently stop mattering past that point.
    private const int MaxPasswordBytes = 72;

    public static readonly HashSet<string> Roles = ["applicant", "scorer", "app_admin", "school_admin", "counselor"];

    /// <summary>Every place a password is set or changed goes through here.</summary>
    public static void Password(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        if (System.Text.Encoding.UTF8.GetByteCount(password) > MaxPasswordBytes)
            throw new ArgumentException($"Password must be at most {MaxPasswordBytes} characters.");
    }

    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw new ArgumentException($"{field} is required.");
        if (trimmed.Length > maxLength) throw new ArgumentException($"{field} must be at most {maxLength} characters.");
        return trimmed;
    }

    /// <summary>Empty becomes <paramref name="empty"/> (null, or "" for NOT NULL columns).</summary>
    public static string? Optional(string? value, string field, int maxLength, string? empty = null)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return empty;
        if (trimmed.Length > maxLength) throw new ArgumentException($"{field} must be at most {maxLength} characters.");
        return trimmed;
    }

    public static string Email(string? value)
    {
        var email = Required(value, "Email", 255);
        if (!MailAddress.TryCreate(email, out var parsed) || parsed.Address != email || !parsed.Host.Contains('.'))
            throw new ArgumentException("Enter a valid email address.");
        return email;
    }

    public static readonly HashSet<string> ApplicationStatuses = ["draft", "submitted", "under_review", "awarded", "rejected", "archived"];

    public static string ApplicationStatus(string? value)
    {
        if (value is null || !ApplicationStatuses.Contains(value)) throw new ArgumentException("Choose a valid status.");
        return value;
    }

    public static string Role(string? value)
    {
        if (value is null || !Roles.Contains(value)) throw new ArgumentException("Choose a valid role.");
        return value;
    }
}
