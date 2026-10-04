using Microsoft.JSInterop;

namespace ArtistShop.Web.Components.Interop;

// An object made by a function in a component's script module, holding a reference back to the
// component for its callbacks. Each script object has a dispose(), which releases what it listens to
public sealed class InteropObject<TComponent> : IAsyncDisposable
    where TComponent : class
{
    private readonly IJSObjectReference _module;
    private readonly DotNetObjectReference<TComponent> _componentReference;

    // for calls whose caller handles a closed page itself, and for reading the object's files
    public IJSObjectReference Reference { get; }

    private InteropObject(IJSObjectReference module, DotNetObjectReference<TComponent> componentReference, IJSObjectReference reference)
    {
        _module = module;
        _componentReference = componentReference;
        Reference = reference;
    }

    // null when the page closed first, so there is nothing to make the object for. The arguments
    // are given the component's reference, to put where the function expects it
    public static async Task<InteropObject<TComponent>?> CreateAsync(
        IJSRuntime js,
        string modulePath,
        string factoryName,
        TComponent component,
        Func<DotNetObjectReference<TComponent>, object?[]> arguments
    )
    {
        var componentReference = DotNetObjectReference.Create(component);

        try
        {
            var module = await js.InvokeAsync<IJSObjectReference>("import", modulePath);
            var reference = await module.InvokeAsync<IJSObjectReference>(factoryName, arguments(componentReference));
            return new InteropObject<TComponent>(module, componentReference, reference);
        }
        catch (JSDisconnectedException)
        {
            componentReference.Dispose();
            return null;
        }
    }

    // does nothing once the page has closed, which already ended whatever the call was for
    public async Task InvokeIfOpenAsync(string identifier, params object?[] arguments)
    {
        try
        {
            await Reference.InvokeVoidAsync(identifier, arguments);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Reference.InvokeVoidAsync("dispose");
            await Reference.DisposeAsync();
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }

        _componentReference.Dispose();
    }
}
