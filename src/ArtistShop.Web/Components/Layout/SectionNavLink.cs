namespace ArtistShop.Web.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

// a NavLink that is active anywhere under ActivePath, even when its own href points
// somewhere deeper, like a vocabulary's /edit page
public class SectionNavLink : NavLink
{
    [Parameter, EditorRequired]
    public string ActivePath { get; set; } = default!;

    protected override bool ShouldMatch(string uriAbsolute)
    {
        var currentPath = new Uri(uriAbsolute).AbsolutePath;

        // the "/" stops /my-route-name/1 from matching /my-route-name/12
        return currentPath == ActivePath || currentPath.StartsWith($"{ActivePath}/");
    }
}
