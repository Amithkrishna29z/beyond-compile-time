using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using PluginDiscovery.Contracts;

namespace PluginDiscovery.Host;

/// <summary>
/// The practical example for Part 2: a plugin architecture built from
/// interface + attribute + reflection + Activator, and then extended with
/// AssemblyLoadContext for genuinely external assemblies.
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, string> NoParameters = [];

    private static void Main()
    {
        Banner("1. IN-PROCESS DISCOVERY - plugins compiled into the host");
        DiscoverInProcess();

        Banner("2. ON-DISK DISCOVERY - assemblies the host never referenced");
        DiscoverFromDisk();

        Banner("3. TYPE IDENTITY ACROSS LOAD CONTEXTS");
        TypeIdentity();

        Banner("4. UNLOADING A COLLECTIBLE AssemblyLoadContext");
        UnloadDemo();

        Banner("5. SECURITY AND VERSIONING - what this design does NOT give you");
        SecurityNotes();
    }

    // =====================================================================
    // 1. The simplest form: scan the current assembly.
    // =====================================================================
    private static void DiscoverInProcess()
    {
        Assembly host = Assembly.GetExecutingAssembly();

        List<PluginDescriptor> descriptors = DescribePluginsIn(host);

        Console.WriteLine("Scanned " + host.GetName().Name + ", found " + descriptors.Count + " declared plugin(s):");
        foreach (PluginDescriptor d in descriptors.OrderBy(d => d.Order))
        {
            Console.WriteLine("  [order " + d.Order + "] " + d.DisplayName + " v" + d.Version + "  -> " + d.ImplementationType.Name);
        }

        Console.WriteLine();
        Console.WriteLine("Activating and running them:");
        foreach (PluginDescriptor d in descriptors.OrderBy(d => d.Order))
        {
            IReportPlugin plugin = d.Activate();
            Console.WriteLine("  --- " + plugin.Name + " ---");
            Console.WriteLine(Indent(plugin.Render(NoParameters)));
        }
    }

    // =====================================================================
    // 2. The real case: a folder of DLLs that were not known at build time.
    // =====================================================================
    private static void DiscoverFromDisk()
    {
        string pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");

        if (!Directory.Exists(pluginFolder))
        {
            Console.WriteLine("No plugins folder at " + pluginFolder);
            Console.WriteLine("Run 'dotnet build' on this project first - the build copies the plugin there.");
            return;
        }

        string[] dlls = Directory.GetFiles(pluginFolder, "*.dll");
        Console.WriteLine("Plugin folder: " + pluginFolder);
        Console.WriteLine("Candidate assemblies: " + (dlls.Length == 0 ? "(none)" : string.Join(", ", dlls.Select(Path.GetFileName))));
        Console.WriteLine();

        foreach (string dll in dlls)
        {
            // One load context per plugin assembly: isolation plus the option to unload.
            var context = new PluginLoadContext(dll);
            Assembly pluginAssembly = context.LoadFromAssemblyPath(dll);

            Console.WriteLine("Loaded " + pluginAssembly.GetName().Name + " v" + pluginAssembly.GetName().Version);
            Console.WriteLine("  into load context: " + AssemblyLoadContext.GetLoadContext(pluginAssembly)?.Name);
            Console.WriteLine("  collectible      : " + (AssemblyLoadContext.GetLoadContext(pluginAssembly)?.IsCollectible ?? false));

            // Report what was rejected and why - the host stays in control.
            foreach (Type candidate in SafeGetTypes(pluginAssembly))
            {
                if (!typeof(IReportPlugin).IsAssignableFrom(candidate) || candidate.IsAbstract || candidate.IsInterface)
                {
                    continue;
                }

                var attribute = candidate.GetCustomAttribute<ReportPluginAttribute>();
                if (attribute is null)
                {
                    Console.WriteLine("  skipped " + candidate.Name + " - implements IReportPlugin but has no [ReportPlugin]");
                    continue;
                }

                if (!attribute.Enabled)
                {
                    Console.WriteLine("  skipped " + candidate.Name + " - [ReportPlugin(Enabled = false)]");
                    continue;
                }

                Console.WriteLine("  accepted " + candidate.Name + " as \"" + attribute.DisplayName + "\" v" + attribute.Version);
            }

            Console.WriteLine();

            // Now activate and execute the accepted ones, in declared order.
            List<PluginDescriptor> descriptors = DescribePluginsIn(pluginAssembly);
            foreach (PluginDescriptor d in descriptors.OrderBy(d => d.Order))
            {
                IReportPlugin plugin = d.Activate();
                Console.WriteLine("  --- " + plugin.Name + " ---");
                Console.WriteLine(Indent(plugin.Render(new Dictionary<string, string>
                {
                    ["month"] = "2026-11",
                    ["take"] = "2"
                })));
            }
        }

        Console.WriteLine();
        Console.WriteLine("The host's code above mentions no plugin class by name. Adding a DLL to the folder");
        Console.WriteLine("adds a feature; no host rebuild, no host edit.");
    }

    // =====================================================================
    // 3. Why the shared-contract rule in PluginLoadContext.Load matters.
    // =====================================================================
    private static void TypeIdentity()
    {
        string pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
        string? dll = Directory.Exists(pluginFolder) ? Directory.GetFiles(pluginFolder, "*.dll").FirstOrDefault() : null;

        if (dll is null)
        {
            Console.WriteLine("(plugin assembly not available - skipping)");
            return;
        }

        var context = new PluginLoadContext(dll);
        Assembly pluginAssembly = context.LoadFromAssemblyPath(dll);

        Type pluginType = SafeGetTypes(pluginAssembly)
            .First(t => typeof(IReportPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);

        // The interface the PLUGIN's type says it implements:
        Type interfaceSeenByPlugin = pluginType.GetInterfaces().First(i => i.Name == nameof(IReportPlugin));

        // The interface the HOST compiled against:
        Type interfaceSeenByHost = typeof(IReportPlugin);

        Console.WriteLine("Host's   IReportPlugin is from " + interfaceSeenByHost.Assembly.GetName().Name
            + " in context " + AssemblyLoadContext.GetLoadContext(interfaceSeenByHost.Assembly)?.Name);
        Console.WriteLine("Plugin's IReportPlugin is from " + interfaceSeenByPlugin.Assembly.GetName().Name
            + " in context " + AssemblyLoadContext.GetLoadContext(interfaceSeenByPlugin.Assembly)?.Name);
        Console.WriteLine();
        Console.WriteLine("Same CLR type? " + (interfaceSeenByPlugin == interfaceSeenByHost));
        Console.WriteLine("Cast works?    " + (Activator.CreateInstance(pluginType) is IReportPlugin));

        Console.WriteLine();
        Console.WriteLine("If PluginLoadContext had loaded its own copy of the contracts assembly, both lines");
        Console.WriteLine("above would be False and the cast would throw InvalidCastException - with an error");
        Console.WriteLine("message naming the SAME type twice. That confusing message is the signature");
        Console.WriteLine("symptom of a duplicated contract assembly.");
    }

    // =====================================================================
    // 4. Unload. Only possible for collectible contexts, and only cooperative.
    // =====================================================================
    private static void UnloadDemo()
    {
        string pluginFolder = Path.Combine(AppContext.BaseDirectory, "plugins");
        string? dll = Directory.Exists(pluginFolder) ? Directory.GetFiles(pluginFolder, "*.dll").FirstOrDefault() : null;

        if (dll is null)
        {
            Console.WriteLine("(plugin assembly not available - skipping)");
            return;
        }

        // The load, use and Unload() all happen inside a method that keeps NO references.
        WeakReference contextReference = LoadUseAndRequestUnload(dll);

        Console.WriteLine("Unload() has been requested. Now the GC has to actually collect it.");

        // Unload is not synchronous: the context dies only once nothing reachable refers to
        // it, its assemblies, or any object whose type came from it.
        for (int attempt = 1; attempt <= 10 && contextReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Console.WriteLine("  collection attempt " + attempt + ": alive = " + contextReference.IsAlive);
        }

        Console.WriteLine();
        Console.WriteLine("Load context unloaded: " + !contextReference.IsAlive);
        Console.WriteLine();
        Console.WriteLine("What keeps a context alive (all of these are common mistakes):");
        Console.WriteLine("  - any live instance of a plugin type");
        Console.WriteLine("  - a cached Type, MethodInfo or Assembly from the plugin");
        Console.WriteLine("  - a delegate pointing at plugin code (an event handler left subscribed)");
        Console.WriteLine("  - a static field in the HOST holding any of the above");
        Console.WriteLine();
        Console.WriteLine("Also note: unloading frees managed state. It cannot undo side effects the plugin");
        Console.WriteLine("already caused, and a plugin that P/Invoked into a native library may pin it.");
    }

    // NoInlining matters: if the JIT inlined this, the locals could stay rooted in Main's
    // frame and the context would never be collected.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadUseAndRequestUnload(string dll)
    {
        var context = new PluginLoadContext(dll);
        var reference = new WeakReference(context, trackResurrection: true);

        Assembly assembly = context.LoadFromAssemblyPath(dll);
        PluginDescriptor descriptor = DescribePluginsIn(assembly).OrderBy(d => d.Order).First();

        IReportPlugin plugin = descriptor.Activate();
        Console.WriteLine("Ran \"" + plugin.Name + "\" inside collectible context " + context.Name);
        Console.WriteLine("  first output line: " + plugin.Render(NoParameters).Split(Environment.NewLine)[0]);

        // Drop every reference we hold, then ask the runtime to unload.
        context.Unload();
        return reference;
    }

    // =====================================================================
    // 5. The honest security section.
    // =====================================================================
    private static void SecurityNotes()
    {
        Console.WriteLine("AssemblyLoadContext is an ISOLATION boundary for TYPE IDENTITY and VERSIONS.");
        Console.WriteLine("It is NOT a security sandbox. A loaded plugin runs with the host's full privileges.");
        Console.WriteLine();
        Console.WriteLine("Once you call LoadFromAssemblyPath on an assembly, that code can:");
        Console.WriteLine("  - read and write any file the host process can");
        Console.WriteLine("  - open sockets, start processes, read environment variables and secrets");
        Console.WriteLine("  - use reflection against the host's own private state");
        Console.WriteLine("  - call Environment.Exit and take the process down");
        Console.WriteLine();
        Console.WriteLine("There is no supported in-process sandbox in modern .NET. Code Access Security (CAS)");
        Console.WriteLine("and AppDomain partial trust were .NET Framework features and are gone.");
        Console.WriteLine();
        Console.WriteLine("So: only load plugins you trust. If you must run untrusted code, isolate it at the");
        Console.WriteLine("OS or platform level - a separate process with a reduced token, a container, a VM,");
        Console.WriteLine("or WASM - not with an AssemblyLoadContext.");
        Console.WriteLine();
        Console.WriteLine("Practical hardening for a TRUSTED plugin model:");
        Console.WriteLine("  - verify a strong name or Authenticode signature before loading");
        Console.WriteLine("  - keep the contracts assembly tiny and version it deliberately");
        Console.WriteLine("  - inspect metadata with MetadataLoadContext first (no code runs) and reject early");
        Console.WriteLine("  - wrap every plugin call in try/catch; one bad plugin should not kill the host");
        Console.WriteLine("  - log the assembly path, version and hash of everything you load");
    }

    // =====================================================================
    // Shared discovery helper. This is the whole plugin system, in 20 lines.
    // =====================================================================
    private static List<PluginDescriptor> DescribePluginsIn(Assembly assembly)
    {
        var found = new List<PluginDescriptor>();

        foreach (Type type in SafeGetTypes(assembly))
        {
            // Must be a concrete implementation of the contract...
            if (type.IsAbstract || type.IsInterface || !typeof(IReportPlugin).IsAssignableFrom(type))
            {
                continue;
            }

            // ...and must opt in through metadata, and be enabled.
            var attribute = type.GetCustomAttribute<ReportPluginAttribute>();
            if (attribute is null || !attribute.Enabled)
            {
                continue;
            }

            found.Add(new PluginDescriptor(type, attribute.DisplayName, attribute.Version, attribute.Order));
        }

        return found;
    }

    /// <summary>
    /// GetTypes() throws if any type fails to load (a missing dependency, for example).
    /// ReflectionTypeLoadException.Types still contains the ones that DID load, so a host
    /// can keep working with a partially broken plugin assembly.
    /// </summary>
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Console.WriteLine("  warning: " + ex.LoaderExceptions.Length + " type(s) failed to load in "
                + assembly.GetName().Name + "; continuing with the rest.");
            return ex.Types.Where(t => t is not null).Cast<Type>();
        }
    }

    private static string Indent(string text) =>
        string.Join(Environment.NewLine, text.Split(Environment.NewLine).Select(l => "    " + l));

    private static void Banner(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }
}

/// <summary>
/// What the host knows about a plugin before deciding to create it: the Type plus the
/// metadata read off its attribute. Activation is deferred until it is actually needed.
/// </summary>
internal sealed record PluginDescriptor(Type ImplementationType, string DisplayName, string Version, int Order)
{
    public IReportPlugin Activate()
    {
        // Activator.CreateInstance is the bridge from "a Type I discovered" to "a live object".
        // A real host would prefer ActivatorUtilities so plugins can take constructor
        // dependencies - see example 07-DependencyInjection.
        object? instance = Activator.CreateInstance(ImplementationType);

        return instance as IReportPlugin
            ?? throw new InvalidOperationException(ImplementationType.FullName + " could not be created as IReportPlugin.");
    }
}

// =========================================================================
// Two plugins compiled directly into the host, for the in-process demo.
// =========================================================================

[ReportPlugin("Host Health Check", Version = "1.0.0", Order = 1)]
internal sealed class HealthCheckPlugin : IReportPlugin
{
    public string Name => "health";

    public string Render(IReadOnlyDictionary<string, string> parameters) =>
        "Runtime     : " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + Environment.NewLine
        + "Process bits: " + (Environment.Is64BitProcess ? "64" : "32") + Environment.NewLine
        + "Load contexts: " + AssemblyLoadContext.All.Count();
}

[ReportPlugin("Loaded Assemblies", Version = "1.0.0", Order = 2)]
internal sealed class AssemblyListPlugin : IReportPlugin
{
    public string Name => "assemblies";

    public string Render(IReadOnlyDictionary<string, string> parameters)
    {
        string[] names = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Select(a => a.GetName().Name!)
            .Where(n => n.StartsWith("PluginDiscovery", StringComparison.Ordinal))
            .Order()
            .ToArray();

        return "PluginDiscovery assemblies currently loaded:" + Environment.NewLine
            + string.Join(Environment.NewLine, names.Select(n => "  " + n));
    }
}
