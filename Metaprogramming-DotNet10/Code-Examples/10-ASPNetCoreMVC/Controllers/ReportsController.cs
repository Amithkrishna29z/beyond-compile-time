using Microsoft.AspNetCore.Mvc;

namespace Metaprogramming.AspNetCoreMvcDemo.Controllers;

/// <summary>
/// An ordinary API controller.
/// <para>
/// Nothing in Program.cs mentions this class. It is found because:
/// 1. its name ends in "Controller" (or it carries [ApiController]/[Controller]), and
/// 2. ASP.NET Core reflects over this assembly at startup looking for exactly that.
/// </para>
/// </summary>
[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(IReportStore store) : ControllerBase
{
    /// <summary>GET /api/reports</summary>
    [HttpGet]
    public ActionResult<IEnumerable<Report>> GetAll() => Ok(store.GetAll());

    /// <summary>GET /api/reports/42 - the route constraint ':int' is metadata too.</summary>
    [HttpGet("{id:int}")]
    public ActionResult<Report> GetById(int id)
    {
        Report? report = store.Find(id);
        return report is null ? NotFound(new { message = "No report " + id }) : Ok(report);
    }

    /// <summary>GET /api/reports/search?term=sales&amp;take=2</summary>
    [HttpGet("search")]
    public ActionResult<IEnumerable<Report>> Search([FromQuery] string term, [FromQuery] int take = 5)
        => Ok(store.Search(term, take));

    /// <summary>POST /api/reports</summary>
    [HttpPost]
    public ActionResult<Report> Create([FromBody] CreateReportRequest request)
    {
        // [ApiController] means model validation already ran, by reflecting over the
        // request type's validation attributes. We never called a validator ourselves.
        Report created = store.Add(request.Title, request.Owner);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>A public method that is deliberately NOT an action.</summary>
    [NonAction]
    public string InternalHelper() => "not routable";
}
