using System.Security.Cryptography;
using System.Text;

namespace EdgeHub.Security;

public static class AccessToken
{
    /// <summary>
    /// Compares a token in constant time. Both sides are hashed first, so the comparison does not reveal the expected
    /// token's length either.
    /// </summary>
    public static bool Matches(string? candidate, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(candidate)),
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }
}
