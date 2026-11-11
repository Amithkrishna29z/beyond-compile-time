using System.Reflection;
using System.Runtime.Loader;

namespace PluginDiscovery.Host;

/// <summary>
/// An <see cref="AssemblyLoadContext"/> for one plugin folder.
/// <para>
/// Two jobs:
/// 1. Isolate the plugin's private dependencies so they cannot collide with the host's.
/// 2. Deliberately NOT isolate the shared contract assembly, so that the plugin's
///    IReportPlugin and the host's IReportPlugin are the same CLR type.
/// </para>
/// <para>
/// isCollectible: true is what makes <see cref="AssemblyLoadContext.Unload"/> possible.
/// It is not free - collectible contexts cannot be used for some scenarios and the
/// unload is cooperative, not immediate. See the host's unload demonstration.
/// </para>
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>Assembly simple names that must come from the DEFAULT context, never be duplicated.</summary>
    private static readonly string[] SharedAssemblies = ["PluginDiscovery.Contracts"];

    public PluginLoadContext(string pluginPath)
        : base(name: "PluginALC:" + Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
    {
        // AssemblyDependencyResolver reads the plugin's .deps.json to work out where its
        // private dependencies live. Without it you would have to probe folders by hand.
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// Called by the runtime whenever code inside this context needs an assembly.
    /// Returning null means "I decline - fall back to the Default context".
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share the contract. If we loaded our own copy, the plugin would implement a
        // DIFFERENT IReportPlugin and every cast in the host would fail with a confusing
        // "unable to cast object of type X to type X" message.
        if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
        {
            return null;
        }

        // Everything else: resolve privately so two plugins can use two versions of the
        // same library without fighting.
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    /// <summary>Same idea for native libraries the plugin may carry.</summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
