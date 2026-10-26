using System.Security.Cryptography;
using System.Text;

namespace Anitec.Platform.Devices.Application.Internal;

/// <summary>
///     Hashes and compares device API keys. A key is 256 random bits, so a fast SHA-256 is enough to protect it
///     at rest (unlike a user password, it cannot be guessed) and it keeps the check on every telemetry request
///     cheap. The comparison runs in constant time.
/// </summary>
public static class DeviceApiKeyHasher
{
    public static string Hash(string apiKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));
    }

    public static bool Matches(string apiKey, string storedHash)
    {
        var candidate = Encoding.ASCII.GetBytes(Hash(apiKey));
        var stored = Encoding.ASCII.GetBytes(storedHash.ToUpperInvariant());
        return CryptographicOperations.FixedTimeEquals(candidate, stored);
    }
}
