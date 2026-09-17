using ArtistShop.Web.Images;
using ArtistShop.Web.Utilities;
using Microsoft.Extensions.Configuration;

namespace ArtistShop.Web.Tests.Utilities;

public sealed class ValidatedSettingsTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void ReadsTheSectionIntoTheSettings()
    {
        var settings = ValidatedSettings.Read<OrphanedImageSweepSettings>(
            Configuration(
                new() { ["OrphanedImageSweep:GracePeriod"] = "7.00:00:00", ["OrphanedImageSweep:Interval"] = "01:00:00" }
            ),
            "OrphanedImageSweep"
        );

        Assert.Equal(TimeSpan.FromDays(7), settings.GracePeriod);
        Assert.Equal(TimeSpan.FromHours(1), settings.Interval);
    }

    [Fact]
    public void RejectsAMissingValue()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ValidatedSettings.Read<OrphanedImageSweepSettings>(
                Configuration(new() { ["OrphanedImageSweep:GracePeriod"] = "7.00:00:00" }),
                "OrphanedImageSweep"
            )
        );

        Assert.Contains("Interval", exception.Message);
    }

    [Fact]
    public void RejectsAMisspelledKey()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ValidatedSettings.Read<OrphanedImageSweepSettings>(
                Configuration(
                    new()
                    {
                        ["OrphanedImageSweep:GracePeriod"] = "7.00:00:00",
                        ["OrphanedImageSweep:Interval"] = "01:00:00",
                        ["OrphanedImageSweep:Intervall"] = "01:00:00",
                    }
                ),
                "OrphanedImageSweep"
            )
        );
    }

    [Fact]
    public void RejectsAMissingSection()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ValidatedSettings.Read<OrphanedImageSweepSettings>(Configuration([]), "OrphanedImageSweep")
        );
    }

    // the real file, so a typo or a missing key there fails a test instead of the next startup
    [Fact]
    public void AcceptsTheAppSettingsFile()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();

        ValidatedSettings.Read<OrphanedImageSweepSettings>(configuration, "OrphanedImageSweep");
        ValidatedSettings.Read<ImageProcessingSettings>(configuration, "ImageProcessing");
    }
}
