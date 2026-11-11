namespace PluginDiscovery.Contracts;

/// <summary>
/// The contract every plugin implements. This assembly is the ONLY thing the host and the
/// plugins share at compile time, which is what makes them independently deployable.
/// </summary>
public interface IReportPlugin
{
    /// <summary>Human-readable name used in the host's menu.</summary>
    string Name { get; }

    /// <summary>Produces the report body.</summary>
    string Render(IReadOnlyDictionary<string, string> parameters);
}

/// <summary>
/// Declarative metadata about a plugin. The host reads this WITHOUT constructing the
/// plugin, so it can list, filter and order plugins before deciding which to activate.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReportPluginAttribute(string displayName) : Attribute
{
    /// <summary>Name shown to the operator.</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>Plugin version, independent of the host's version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Lower numbers are offered first.</summary>
    public int Order { get; set; } = 100;

    /// <summary>Set false to ship a plugin that is present but not offered.</summary>
    public bool Enabled { get; set; } = true;
}
