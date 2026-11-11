using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Metaprogramming.AspNetCoreMvcDemo.Controllers;

/// <summary>
/// Exposes the framework's own routing metadata over HTTP.
/// <para>
/// This is the demo's punchline: the application can describe its own API surface,
/// because that surface was discovered from metadata in the first place. Swagger /
/// OpenAPI generation is this same idea with a nicer output format.
/// </para>
/// </summary>
[ApiController]
[Route("api/metadata")]
public sealed class MetadataController(IActionDescriptorCollectionProvider provider) : ControllerBase
{
    /// <summary>GET /api/metadata/actions</summary>
    [HttpGet("actions")]
    public ActionResult<IEnumerable<object>> Actions()
    {
        var actions = provider.ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .OrderBy(a => a.ControllerName)
            .ThenBy(a => a.ActionName)
            .Select(a => new
            {
                controller = a.ControllerName,
                action = a.ActionName,
                route = a.AttributeRouteInfo?.Template ?? "(conventional)",

                // The HTTP methods come from the [HttpGet]/[HttpPost] attributes,
                // read by the framework and stored on the descriptor.
                httpMethods = a.ActionConstraints?
                    .OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
                    .SelectMany(c => c.HttpMethods)
                    .ToArray() ?? [],

                // The reflected method signature, straight out of System.Reflection.
                signature = a.MethodInfo.Name + "("
                    + string.Join(", ", a.MethodInfo.GetParameters()
                        .Select(p => p.ParameterType.Name + " " + p.Name))
                    + ")",

                declaringType = a.ControllerTypeInfo.FullName
            });

        return Ok(actions);
    }
}
