using System.Security.Cryptography;

namespace ScholarshipApi.Services;

/// <summary>The 10-character code in a reference link (/r/CODE): uppercase letters and digits, about 52 bits of randomness.</summary>
public static class ReferenceCode
{
    public const int Length = 10;
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string Generate() =>
        string.Create(Length, 0, (chars, _) =>
        {
            for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });

    /// <summary>Trimmed and upper-cased, or null when it can't be a code (so lookups skip the database).</summary>
    public static string? Normalize(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        return normalized is { Length: Length } && normalized.All(c => Alphabet.Contains(c)) ? normalized : null;
    }

    public static string Hash(string normalizedCode) => TokenHash.Sha256(normalizedCode);

    // Codes are encrypted with the two-factor key; the request id binds each ciphertext to its row
    public static string AssociatedData(string requestId) => $"reference:{requestId}";
}
