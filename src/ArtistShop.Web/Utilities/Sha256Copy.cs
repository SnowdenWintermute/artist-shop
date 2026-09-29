using System.Security.Cryptography;

namespace ArtistShop.Web.Utilities;

public static class Sha256Copy
{
    // copies source to destination and returns the SHA-256 of what was copied, in lower case hex,
    // worked out on the way so the source is read once
    public static async Task<string> CopyAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81_920];
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            sha256.AppendData(buffer, 0, read);
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return Convert.ToHexStringLower(sha256.GetHashAndReset());
    }
}
