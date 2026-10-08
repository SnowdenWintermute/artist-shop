using ArtistShop.Web.Database.Repositories;
using ArtistShop.Web.Domain.Website;

namespace ArtistShop.Web.Imports;

public enum WordingImportOutcome
{
    // the download's wording is already this website's
    Same,

    // this website is still on the default wording, so it takes the download's
    Set,

    // this website chose its own words, which the import leaves alone, as it only adds elsewhere
    Kept,
}

public record WordingImportPlan(SiteWording Wording, WordingImportOutcome Outcome);

public static class WordingImportPlanner
{
    public static WordingImportPlan Plan(SiteWording downloaded, SiteWording current) =>
        new(
            downloaded,
            downloaded == current ? WordingImportOutcome.Same
            : current == SiteWording.Default ? WordingImportOutcome.Set
            : WordingImportOutcome.Kept
        );

    // planned again against the website as it is now, like each catalog stage
    public static async Task ImportAsync(SiteWording downloaded, WordingRepository wordingRepository)
    {
        if (Plan(downloaded, await wordingRepository.GetAsync()).Outcome is WordingImportOutcome.Set)
        {
            await wordingRepository.UpdateAsync(downloaded);
        }
    }
}
