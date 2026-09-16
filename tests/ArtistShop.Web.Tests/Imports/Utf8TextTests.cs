using System.Text;
using ArtistShop.Web.Imports;

namespace ArtistShop.Web.Tests.Imports;

public sealed class Utf8TextTests
{
    private static Task<string?> Read(byte[] bytes) => Utf8Text.TryReadAsync(new MemoryStream(bytes));

    [Fact]
    public async Task ReadsUtf8()
    {
        Assert.Equal("title\nCafé", await Read(Encoding.UTF8.GetBytes("title\nCafé")));
    }

    [Fact]
    public async Task DropsTheByteOrderMark()
    {
        Assert.Equal("title", await Read([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("title")]));
    }

    // "Café" saved as Windows-1252, the encoding older spreadsheet exports use
    [Fact]
    public async Task RejectsOtherEncodings()
    {
        Assert.Null(await Read([0x43, 0x61, 0x66, 0xE9]));
    }
}
