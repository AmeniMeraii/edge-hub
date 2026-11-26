using System.Security.Cryptography;
using System.Text;

namespace EdgeHub.Security;

/// <summary>
/// The tablet's token. The PC keeps only its SHA-256 hash: the token itself lives in the tablet's cookie, so an agent
/// that reads the PC's files (Claude Code, for one) cannot answer a request in the tablet's place.
/// </summary>
public static class AccessToken
{
    /// <summary>A new token: 32 random bytes, 43 characters that fit in an address as they are.</summary>
    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>The hash kept in the configuration: SHA-256, 64 lower-case hexadecimal characters.</summary>
    public static string HashOf(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    /// <summary>Whether the candidate is the token whose hash is kept, compared in constant time.</summary>
    public static bool Matches(string? candidate, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(expectedHash);
        if (string.IsNullOrEmpty(candidate) || expectedHash.Length != 64)
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(candidate)), expected);
    }
}
