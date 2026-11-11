using System.Reflection;
using PluginDiscovery.Contracts;

namespace PluginDiscovery.Plugins.Sales;

/// <summary>
/// An "external" plugin. The host never references this project at compile time -
/// it is discovered on disk and loaded into an AssemblyLoadContext at runtime.
/// </summary>
[ReportPlugin("Monthly Sales Summary", Version = "2.1.0", Order = 10)]
public sealed class MonthlySalesPlugin : IReportPlugin
{
    public string Name => "monthly-sales";

    public string Render(IReadOnlyDictionary<string, string> parameters)
    {
        string month = parameters.GetValueOrDefault("month", "2026-11");

        // Deterministic fake data - the point is the loading mechanism, not the arithmetic.
        (string Region, decimal Amount)[] rows =
        [
            ("South", 418_500m),
            ("West", 302_750m),
            ("North", 211_000m)
        ];

        decimal total = rows.Sum(r => r.Amount);

        var lines = new List<string> { "Monthly sales for " + month };
        lines.AddRange(rows.Select(r => "  " + r.Region.PadRight(6) + r.Amount.ToString("N2")));
        lines.Add("  TOTAL " + total.ToString("N2"));

        // Proof of which assembly and which load context actually executed this.
        Assembly self = typeof(MonthlySalesPlugin).Assembly;
        lines.Add("  (rendered by " + self.GetName().Name + " v" + self.GetName().Version + ")");

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>A second plugin in the same assembly, to show per-type discovery.</summary>
[ReportPlugin("Top Customers", Version = "2.1.0", Order = 20)]
public sealed class TopCustomersPlugin : IReportPlugin
{
    public string Name => "top-customers";

    public string Render(IReadOnlyDictionary<string, string> parameters)
    {
        int take = int.TryParse(parameters.GetValueOrDefault("take"), out int n) ? n : 3;

        string[] customers = ["Nila Traders", "Kochi Hardware", "Calicut Spices", "Trivandrum Books"];

        return "Top " + take + " customers:" + Environment.NewLine
            + string.Join(Environment.NewLine, customers.Take(take).Select((c, i) => "  " + (i + 1) + ". " + c));
    }
}

/// <summary>
/// Present in the assembly but switched off through metadata. The host must read the
/// attribute and skip it WITHOUT constructing the type.
/// </summary>
[ReportPlugin("Experimental Forecast", Version = "0.1.0-alpha", Order = 5, Enabled = false)]
public sealed class ExperimentalForecastPlugin : IReportPlugin
{
    public string Name => "forecast";

    public string Render(IReadOnlyDictionary<string, string> parameters) => "not ready";
}

/// <summary>
/// Implements the contract but carries no [ReportPlugin] attribute. Shows the difference
/// between "assignable to the interface" and "declared as a plugin".
/// </summary>
public sealed class UnannotatedHelperPlugin : IReportPlugin
{
    public string Name => "unannotated";

    public string Render(IReadOnlyDictionary<string, string> parameters) => "should not be offered";
}
