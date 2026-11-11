using System.ComponentModel.DataAnnotations;

namespace Metaprogramming.AspNetCoreMvcDemo;

public sealed record Report(int Id, string Title, string Owner);

/// <summary>
/// The request model. Its validation attributes are read reflectively by MVC
/// before the action method is ever entered - see [ApiController].
/// </summary>
public sealed class CreateReportRequest
{
    [Required]
    [StringLength(60, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Owner { get; set; } = string.Empty;
}

public interface IReportStore
{
    IEnumerable<Report> GetAll();

    Report? Find(int id);

    IEnumerable<Report> Search(string term, int take);

    Report Add(string title, string owner);
}

public sealed class InMemoryReportStore : IReportStore
{
    private readonly List<Report> _reports =
    [
        new(1, "Monthly Sales", "asha@example.com"),
        new(2, "Inventory Ageing", "ravi@example.com"),
        new(3, "Sales by Region", "asha@example.com")
    ];

    private int _nextId = 4;

    public IEnumerable<Report> GetAll() => _reports;

    public Report? Find(int id) => _reports.FirstOrDefault(r => r.Id == id);

    public IEnumerable<Report> Search(string term, int take) =>
        _reports
            .Where(r => r.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(take);

    public Report Add(string title, string owner)
    {
        var report = new Report(_nextId++, title, owner);
        _reports.Add(report);
        return report;
    }
}
