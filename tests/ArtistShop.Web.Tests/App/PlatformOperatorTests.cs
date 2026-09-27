using ArtistShop.Web.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace ArtistShop.Web.Tests.App;

// Against the app's own identity database, which its startup has already synced once
[Collection(TestAppCollection.Name)]
public sealed class PlatformOperatorTests(TestApp app)
{
    [Fact]
    public async Task StartupMakesTheOperatorsAccountWithItsEmailConfirmed()
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var account = await userManager.FindByEmailAsync(TestApp.OperatorEmail);

        Assert.NotNull(account);
        Assert.True(account.EmailConfirmed);
        Assert.True(await userManager.IsInRoleAsync(account, PlatformOperator.RoleName));
    }

    // the setting is the only say in who the operator is
    [Fact]
    public async Task SyncingTakesTheRoleFromAnyOtherAccount()
    {
        using var scope = app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = await app.MakeAccountAsync();
        var other = await userManager.FindByEmailAsync(email) ?? throw new InvalidOperationException("No account.");
        SeededAccounts.ThrowIfFailed(await userManager.AddToRoleAsync(other, PlatformOperator.RoleName), "Adding the role");
        var client = await app.SignedInClientAsync(TestApp.PlatformHost, email);

        await PlatformOperator.SyncAsync(scope.ServiceProvider);

        Assert.False(await userManager.IsInRoleAsync(other, PlatformOperator.RoleName));
        // a sign-in's claims hold its roles, so one that kept going would keep the role
        var signedOut = await client.GetAsync("/sites", TestContext.Current.CancellationToken);
        Assert.Equal("/Account/Login", signedOut.Headers.Location?.AbsolutePath);
        var operatorAccount = await userManager.FindByEmailAsync(TestApp.OperatorEmail);
        Assert.NotNull(operatorAccount);
        Assert.True(await userManager.IsInRoleAsync(operatorAccount, PlatformOperator.RoleName));
    }
}
