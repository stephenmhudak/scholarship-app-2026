using System.Security.Cryptography;
using System.Text;

namespace ScholarshipApi.Services;

public static class TokenHash
{
    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLower();
}
