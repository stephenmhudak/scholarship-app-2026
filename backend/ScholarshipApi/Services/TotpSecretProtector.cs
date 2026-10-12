using System.Security.Cryptography;
using System.Text;

namespace ScholarshipApi.Services;

// Encrypts authenticator secrets at rest with AES-GCM. The user id is bound in as associated data,
// so a ciphertext copied onto another user's row won't decrypt.
public class TotpSecretProtector
{
    private const string Prefix = "v1:";
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly byte[] key;

    public TotpSecretProtector(IConfiguration config)
    {
        const string help = "Generate one with `openssl rand -base64 32` and set TwoFactor__EncryptionKey (see secrets/twofactor.env.example).";
        var configured = config["TwoFactor:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException($"TwoFactor:EncryptionKey is not set. {help}");

        try { key = Convert.FromBase64String(configured); }
        catch (FormatException) { key = []; }
        if (key.Length != 32)
            throw new InvalidOperationException($"TwoFactor:EncryptionKey must be 32 random bytes, base64-encoded. {help}");
    }

    public string Protect(string secret, string userId)
    {
        var plaintext = Encoding.UTF8.GetBytes(secret);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(userId));
        return Prefix + Convert.ToBase64String([.. nonce, .. ciphertext, .. tag]);
    }

    public string Unprotect(string value, string userId)
    {
        if (!value.StartsWith(Prefix))
            throw new CryptographicException("Unrecognized two-factor secret format.");

        var data = Convert.FromBase64String(value[Prefix.Length..]);
        var nonce = data.AsSpan(0, NonceSize);
        var ciphertext = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);
        var tag = data.AsSpan(data.Length - TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, Encoding.UTF8.GetBytes(userId));
        return Encoding.UTF8.GetString(plaintext);
    }
}
