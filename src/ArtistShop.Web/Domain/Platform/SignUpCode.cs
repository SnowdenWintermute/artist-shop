namespace ArtistShop.Web.Domain.Platform;

using System.Security.Cryptography;

// What someone types to sign up for a website: 16 random bytes, written as hex in groups of four,
// such as 3F9A-0C21-77BE-…. Only its hash is stored
public sealed class SignUpCode
{
    private const int ByteCount = 16;

    private readonly byte[] _bytes;

    private SignUpCode(byte[] bytes) => _bytes = bytes;

    public static SignUpCode New() => new(RandomNumberGenerator.GetBytes(ByteCount));

    // null for anything that isn't the code's hex digits once hyphens, spaces and case are set
    // aside, since people copy and type codes carelessly
    public static SignUpCode? Read(string text)
    {
        var digits = string.Concat(text.Where(character => character != '-' && !char.IsWhiteSpace(character)));

        return digits.Length == ByteCount * 2 && digits.All(char.IsAsciiHexDigit)
            ? new SignUpCode(Convert.FromHexString(digits))
            : null;
    }

    public string Text => string.Join("-", Convert.ToHexString(_bytes).Chunk(4).Select(group => new string(group)));

    // what sign_up_codes keeps
    public byte[] Hash() => SHA256.HashData(_bytes);
}

public record SignUpCodeId(int Value);

// an unused code as the operator's list shows it; the code itself is never kept
public record SignUpCodeListing(SignUpCodeId Id, string Note, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt)
{
    public bool IsExpiredAt(DateTimeOffset moment) => ExpiresAt <= moment;
}
