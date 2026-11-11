using System.Reflection;
using Metaprogramming.AspNetCoreMvcDemo;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

// =========================================================================
// Part 4 of the session: ASP.NET Core MVC as a large-scale metaprogramming system.
//
// Run modes:
//   dotnet run                -> starts the server and keeps serving (Ctrl+C to stop)
//   dotnet run -- --selftest  -> starts, prints the discovery report, calls itself
//                                over HTTP, prints the responses, then exits
// =========================================================================

bool selfTest = args.Contains("--selftest");

var builder = WebApplication.CreateBuilder(args);

// Quieten the framework's own logging so the report is readable.
builder.Logging.ClearProviders();
if (!selfTest)
{
    builder.Logging.AddConsole();
}

// THE one line that starts the discovery machinery. It does not name any controller.
builder.Services.AddControllers();

builder.Services.AddSingleton<IReportStore, InMemoryReportStore>();

// A fixed port so the self-test knows where to call.
builder.WebHost.UseUrls("http://127.0.0.1:5199");

WebApplication app = builder.Build();

app.MapControllers();

// =========================================================================
// STARTUP-TIME discovery report. Everything below happens ONCE, before any
// request is served. This is the distinction the session needs to make clear:
// reflection happens at STARTUP; requests are then served from a cached table.
// =========================================================================

Banner("1. APPLICATION PARTS - where MVC looks for controllers");

var partManager = app.Services.GetRequiredService<ApplicationPartManager>();

Console.WriteLine("ApplicationPartManager.ApplicationParts:");
foreach (ApplicationPart part in partManager.ApplicationParts)
{
    string kind = part is AssemblyPart ? "AssemblyPart" : part.GetType().Name;
    Console.WriteLine($"  {part.Name,-28} ({kind})");
}

Console.WriteLine();
Console.WriteLine("Application parts are the SEARCH PATH. By default it holds the entry assembly plus");
Console.WriteLine("any assembly marked with [ApplicationPartAttribute]. Add a part and MVC will find");
Console.WriteLine("controllers in another DLL - which is how a plugin can contribute endpoints.");

Banner("2. CONTROLLER DISCOVERY - the ControllerFeature");

// This is literally the mechanism MVC uses: a feature provider walks the parts
// and populates a list of controller types.
var controllerFeature = new ControllerFeature();
partManager.PopulateFeature(controllerFeature);

Console.WriteLine("Controller types found by reflecting over the application parts:");
foreach (TypeInfo controller in controllerFeature.Controllers)
{
    Console.WriteLine("  " + controller.FullName);
}

Console.WriteLine();
Console.WriteLine("The rule MVC applies to a public class, in order:");
Console.WriteLine("  - skip it if it is abstract, generic, or marked [NonController]");
Console.WriteLine("  - accept it if it carries [Controller] or [ApiController]");
Console.WriteLine("  - otherwise accept it if its NAME ends with \"Controller\"");
Console.WriteLine();
Console.WriteLine("Note that inheriting from ControllerBase is NOT what makes a class a controller.");
Console.WriteLine("The name suffix or the attribute is. ControllerBase just gives you helpers like Ok().");

Banner("3. ACTION DISCOVERY - the ActionDescriptor table");

var actionProvider = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>();

Console.WriteLine("IActionDescriptorCollectionProvider gives the routing table, version "
    + actionProvider.ActionDescriptors.Version + ":");
Console.WriteLine();
Console.WriteLine($"  {"METHOD",-7} {"ROUTE TEMPLATE",-26} {"CONTROLLER",-10} {"ACTION",-10} PARAMETERS");
Console.WriteLine("  " + new string('-', 86));

foreach (ControllerActionDescriptor descriptor in actionProvider.ActionDescriptors.Items
             .OfType<ControllerActionDescriptor>()
             .OrderBy(d => d.AttributeRouteInfo?.Template))
{
    string[] httpMethods = descriptor.ActionConstraints?
        .OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
        .SelectMany(c => c.HttpMethods)
        .ToArray() ?? [];

    string verb = httpMethods.Length == 0 ? "ANY" : string.Join(",", httpMethods);

    string parameters = string.Join(", ", descriptor.Parameters
        .Select(p => p.ParameterType.Name + " " + p.Name));

    Console.WriteLine($"  {verb,-7} {descriptor.AttributeRouteInfo?.Template ?? "(conventional)",-26} "
        + $"{descriptor.ControllerName,-10} {descriptor.ActionName,-10} {parameters}");
}

Console.WriteLine();
Console.WriteLine("Where each column came from:");
Console.WriteLine("  METHOD    the [HttpGet] / [HttpPost] attribute on the method");
Console.WriteLine("  ROUTE     [Route] on the class combined with the template on the method");
Console.WriteLine("  ACTION    the reflected MethodInfo.Name");
Console.WriteLine("  PARAMS    reflected ParameterInfo, including [FromQuery] / [FromBody] binding source");
Console.WriteLine();
Console.WriteLine("Note that ReportsController.InternalHelper is absent: [NonAction] excluded it.");

Banner("4. STARTUP-TIME vs REQUEST-TIME - the point people get wrong");

Console.WriteLine("Done ONCE, at startup (what sections 1-3 just showed):");
Console.WriteLine("  - scan application parts for controller types          <- reflection");
Console.WriteLine("  - read routing, filter and binding attributes          <- reflection");
Console.WriteLine("  - build the ActionDescriptor collection and route table");
Console.WriteLine("  - build a compiled/cached activator and model binders per action");
Console.WriteLine();
Console.WriteLine("Done PER REQUEST:");
Console.WriteLine("  - match the URL against the prepared route table       <- NO type scanning");
Console.WriteLine("  - create the controller through DI");
Console.WriteLine("  - bind parameters using the prepared binders");
Console.WriteLine("  - invoke the action through a cached delegate, not MethodInfo.Invoke");
Console.WriteLine();
Console.WriteLine("So 'MVC uses reflection, therefore MVC is slow per request' is wrong. The");
Console.WriteLine("reflection is amortised into startup, and this is the single most important");
Console.WriteLine("design lesson to take from the whole session: DISCOVER ONCE, EXECUTE MANY TIMES.");
Console.WriteLine();
Console.WriteLine("It is also why startup-time matters for serverless, and why minimal APIs plus");
Console.WriteLine("source generation exist: to move even that startup reflection to build time.");

if (!selfTest)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine("Server listening on http://127.0.0.1:5199");
    Console.WriteLine("Try:");
    Console.WriteLine("  http://127.0.0.1:5199/api/reports");
    Console.WriteLine("  http://127.0.0.1:5199/api/reports/2");
    Console.WriteLine("  http://127.0.0.1:5199/api/reports/search?term=sales&take=2");
    Console.WriteLine("  http://127.0.0.1:5199/api/metadata/actions");
    Console.WriteLine("Press Ctrl+C to stop.");
    Console.WriteLine(new string('=', 78));

    app.Run();
    return;
}

// =========================================================================
// SELF-TEST: start the server, call it, print the responses, shut down.
// =========================================================================

await app.StartAsync();

Banner("5. REQUEST-TIME - the discovered routes actually serving traffic");

using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5199") };

await ShowAsync(http, "GET", "/api/reports");
await ShowAsync(http, "GET", "/api/reports/2");
await ShowAsync(http, "GET", "/api/reports/999");
await ShowAsync(http, "GET", "/api/reports/search?term=sales&take=2");
await ShowAsync(http, "GET", "/api/reports/not-an-int");
await ShowAsync(http, "POST", "/api/reports", """{"title":"Quarterly Tax","owner":"deepa@example.com"}""");
await ShowAsync(http, "POST", "/api/reports", """{"title":"x","owner":"not-an-email"}""");
await ShowAsync(http, "GET", "/api/metadata/actions");

Banner("6. WHAT THE RESPONSES DEMONSTRATED");

Console.WriteLine("  /api/reports/2            route template {id:int} bound \"2\" to an int parameter");
Console.WriteLine("  /api/reports/999          the action ran and chose NotFound - routing succeeded");
Console.WriteLine("  /api/reports/not-an-int   404 BEFORE the action ran: the :int route CONSTRAINT");
Console.WriteLine("                            rejected it, so no action was selected at all");
Console.WriteLine("  /api/reports/search       [FromQuery] bound term and take, and applied take's");
Console.WriteLine("                            default value of 5 from the method signature");
Console.WriteLine("  POST with a valid body    [FromBody] deserialized JSON into CreateReportRequest");
Console.WriteLine("                            using System.Text.Json (example 09), then returned 201");
Console.WriteLine("                            with a Location header built by CreatedAtAction, which");
Console.WriteLine("                            looked the route up BY ACTION NAME in the same table");
Console.WriteLine("  POST with an invalid body 400 with a ValidationProblemDetails body. We wrote no");
Console.WriteLine("                            validation code: [ApiController] reflected over the");
Console.WriteLine("                            [Required]/[StringLength]/[EmailAddress] attributes");
Console.WriteLine("  /api/metadata/actions     the app describing its own routing table over HTTP");
Console.WriteLine();
Console.WriteLine("Every one of those behaviours was configured by DECLARING something - an attribute,");
Console.WriteLine("a parameter type, a method name - and never by writing dispatch logic.");
Console.WriteLine("That is the practical value of metadata-driven frameworks.");

await app.StopAsync();

Console.WriteLine();
Console.WriteLine("Self-test complete; server stopped.");

return;

// =========================================================================
// Helpers
// =========================================================================

static async Task ShowAsync(HttpClient http, string method, string path, string? json = null)
{
    using var request = new HttpRequestMessage(new HttpMethod(method), path);

    if (json is not null)
    {
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    using HttpResponseMessage response = await http.SendAsync(request);
    string body = await response.Content.ReadAsStringAsync();

    Console.WriteLine();
    Console.WriteLine(method + " " + path + (json is null ? "" : "   body: " + json));
    Console.WriteLine("  -> " + (int)response.StatusCode + " " + response.ReasonPhrase);

    if (response.Headers.Location is not null)
    {
        Console.WriteLine("  -> Location: " + response.Headers.Location);
    }

    Console.WriteLine("  -> " + Truncate(body, 300));
}

static string Truncate(string text, int max)
{
    string collapsed = text.Replace("\r", "").Replace("\n", " ");
    return collapsed.Length <= max ? collapsed : collapsed[..max] + " ...";
}

static void Banner(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 78));
}
