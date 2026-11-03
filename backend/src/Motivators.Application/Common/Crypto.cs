using System.Security.Cryptography;
using System.Text;

namespace Motivators.Application.Common;

public static class Crypto
{
    private const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>32 криптослучайных байта в base64url.</summary>
    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static byte[] Sha256(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));

    public static string NewPublicCode(int length = 10) => RandomNumberGenerator.GetString(Base62, length);

    public static byte[] NewSalt() => RandomNumberGenerator.GetBytes(16);

    public static byte[] RespondentHash(byte[] salt, string respondentKey)
    {
        var key = Encoding.UTF8.GetBytes(respondentKey);
        var buf = new byte[salt.Length + key.Length];
        salt.CopyTo(buf, 0);
        key.CopyTo(buf, salt.Length);
        return SHA256.HashData(buf);
    }

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
