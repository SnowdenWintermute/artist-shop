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
        var email = $"{Guid.NewGuid():n}@example.com";
        var other = new ApplicationUser { UserName = email, Email = email };
        SeededAccounts.ThrowIfFailed(await userManager.CreateAsync(other, TestApp.Password), "Creating the account");
        SeededAccounts.ThrowIfFailed(await userManager.AddToRoleAsync(other, PlatformOperator.RoleName), "Adding the role");

        await PlatformOperator.SyncAsync(scope.ServiceProvider);

        Assert.False(await userManager.IsInRoleAsync(other, PlatformOperator.RoleName));
        var operatorAccount = await userManager.FindByEmailAsync(TestApp.OperatorEmail);
        Assert.NotNull(operatorAccount);
        Assert.True(await userManager.IsInRoleAsync(operatorAccount, PlatformOperator.RoleName));
    }
}
