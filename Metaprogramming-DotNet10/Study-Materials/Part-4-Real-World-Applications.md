# Part 4 — Real-World Applications

**Session time: 9 minutes (slides 24–27)**
**Runnable examples: [`07-DependencyInjection`](../Code-Examples/07-DependencyInjection),
[`08-ExpressionTrees`](../Code-Examples/08-ExpressionTrees),
[`09-Serialization`](../Code-Examples/09-Serialization),
[`10-ASPNetCoreMVC`](../Code-Examples/10-ASPNetCoreMVC),
[`11-EntityFrameworkCore`](../Code-Examples/11-EntityFrameworkCore),
[`12-DynamicProxies`](../Code-Examples/12-DynamicProxies)**

> **The one idea that unifies this entire part:**
> **DISCOVER ONCE, CACHE, THEN EXECUTE COMPILED CODE.**
> Every framework below reflects at **startup** or **first use**, builds a fast plan,
> caches it, and never reflects again on the hot path. This is the answer to "isn't
> reflection slow?" and it is the single most transferable design lesson in the session.

---

# 1. ASP.NET Core MVC

## 1.1 What the framework does for you

You write this, and never register it anywhere:

```csharp
[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<Report>> GetAll() => Ok(store.GetAll());

    [HttpGet("{id:int}")]
    public ActionResult<Report> GetById(int id) { /* ... */ }

    [HttpGet("search")]
    public ActionResult<IEnumerable<Report>> Search([FromQuery] string term, [FromQuery] int take = 5)
        => Ok(store.Search(term, take));

    [HttpPost]
    public ActionResult<Report> Create([FromBody] CreateReportRequest request) { /* ... */ }

    [NonAction]
    public string InternalHelper() => "not routable";
}
```

`Program.cs` contains one relevant line, which names no controller:

```csharp
builder.Services.AddControllers();
```

## 1.2 Application parts — the search path

Verified (`10-ASPNetCoreMVC`):

```text
ApplicationPartManager.ApplicationParts:
  AspNetCoreMvcDemo            (AssemblyPart)
```

An **application part** is a source of controllers. By default the list holds the entry
assembly plus any assembly marked with `[ApplicationPart]`. Add a part and MVC will find
controllers in another DLL — which is how a plugin can contribute endpoints, joining
Part 2 §9 to this section.

## 1.3 Controller discovery

MVC's own mechanism, used directly in the example:

```csharp
var controllerFeature = new ControllerFeature();
partManager.PopulateFeature(controllerFeature);
```

Verified:

```text
Controller types found by reflecting over the application parts:
  Metaprogramming.AspNetCoreMvcDemo.Controllers.MetadataController
  Metaprogramming.AspNetCoreMvcDemo.Controllers.ReportsController
```

**The rule MVC applies to a public class, in order:**

1. skip it if it is abstract, generic, or marked `[NonController]`;
2. accept it if it carries `[Controller]` or `[ApiController]`;
3. otherwise accept it if its **name ends with `"Controller"`**.

> **Inheriting from `ControllerBase` is NOT what makes a class a controller.** The name
> suffix or the attribute is. `ControllerBase` only gives you helpers like `Ok()` and
> `NotFound()`. This surprises people, and you can demonstrate it by renaming a class.

## 1.4 Action discovery and the `ActionDescriptor` table

```csharp
var actionProvider = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
```

Verified:

```text
IActionDescriptorCollectionProvider gives the routing table, version 0:

  METHOD  ROUTE TEMPLATE             CONTROLLER ACTION     PARAMETERS
  --------------------------------------------------------------------------------------
  GET     api/metadata/actions       Metadata   Actions
  GET     api/reports                Reports    GetAll
  POST    api/reports                Reports    Create     CreateReportRequest request
  GET     api/reports/search         Reports    Search     String term, Int32 take
  GET     api/reports/{id:int}       Reports    GetById    Int32 id
```

Where each column came from:

| Column | Source |
|---|---|
| METHOD | the `[HttpGet]` / `[HttpPost]` attribute |
| ROUTE | `[Route]` on the class combined with the template on the method |
| CONTROLLER | the type name, minus the `Controller` suffix |
| ACTION | the reflected `MethodInfo.Name` |
| PARAMETERS | reflected `ParameterInfo`, including the `[FromQuery]`/`[FromBody]` binding source |

Note `InternalHelper` is **absent** — `[NonAction]` excluded it. A public method on a
controller is an action **by default**; opting out is explicit.

The table is a **cached data structure**. The `Version` property exists so that
components can detect changes if parts are added at run time.

## 1.5 Startup-time vs request-time — the point people get wrong

**This distinction is the heart of slide 24.**

| Done **ONCE**, at startup | Done **PER REQUEST** |
|---|---|
| scan application parts for controller types ← *reflection* | match the URL against the prepared route table — **no type scanning** |
| read routing, filter and binding attributes ← *reflection* | create the controller through DI |
| build the `ActionDescriptor` collection and route table | bind parameters using the prepared binders |
| build a cached activator and model binders per action | invoke the action through a **cached delegate**, not `MethodInfo.Invoke` |

> So **"MVC uses reflection, therefore MVC is slow per request" is wrong.** The
> reflection is amortised into startup.

It is also why **startup time** matters for serverless and why minimal APIs plus source
generation exist: to move even that startup reflection to build time.

## 1.6 The payoff — an app that describes itself

Because the API surface was *discovered* from metadata, the app can report it:

```csharp
[HttpGet("actions")]
public ActionResult<IEnumerable<object>> Actions() =>
    Ok(provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>().Select(a => new
    {
        controller = a.ControllerName,
        action = a.ActionName,
        route = a.AttributeRouteInfo?.Template ?? "(conventional)",
        signature = a.MethodInfo.Name + "(" + string.Join(", ",
            a.MethodInfo.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")"
    }));
```

Verified over real HTTP:

```text
GET /api/metadata/actions
  -> 200 OK
  -> [{"controller":"Metadata","action":"Actions","route":"api/metadata/actions",
       "httpMethods":["GET"],"signature":"Actions()", ...}, ...]
```

**This is exactly how Swagger / OpenAPI generation works.** No separate spec file; the
spec is derived from the code.

## 1.7 Request-time behaviour, verified

```text
GET /api/reports/2                      -> 200  {id:int} bound "2" to an int
GET /api/reports/999                    -> 404  the ACTION ran and chose NotFound
GET /api/reports/not-an-int             -> 404  the ROUTE CONSTRAINT rejected it; no action ran
GET /api/reports/search?term=sales&take=2 -> 200  [FromQuery] binding + default value
POST /api/reports  (valid body)         -> 201  Location: http://127.0.0.1:5199/api/reports/4
POST /api/reports  (invalid body)       -> 400  ValidationProblemDetails
```

Three of those are worth dwelling on:

- **`/999` vs `/not-an-int`** are both 404 but for completely different reasons. The
  first reached the action; the second never selected one, because the `:int` constraint
  failed during routing. Distinguishing these two is a real debugging skill.
- **`CreatedAtAction(nameof(GetById), ...)`** produced a `Location` header by looking the
  route up **by action name** in the same descriptor table. Metadata read back for URL
  generation, not just dispatch.
- **The 400** needed no validation code from us:

```text
{"title":"One or more validation errors occurred.","status":400,
 "errors":{"Owner":["The Owner field is not a valid e-mail address."],
           "Title":["The field Title must be a string with a minimum length of 3 and a maximum length of 60."]}}
```

`[ApiController]` reflected over `[Required]`, `[StringLength]` and `[EmailAddress]` on
the request model and short-circuited the request.

## 1.8 Advantages and limitations

**Advantages:** no registration to forget; routes live next to the code they route to;
tooling (Swagger, analyzers, route debugging) all works off the same metadata.

**Limitations:** convention-based magic is invisible to newcomers; a typo in a route
template is a run-time 404, not a build error; startup cost grows with assembly size;
"find all usages" of an action returns nothing, because nothing calls it directly.

## 1.9 Audience questions

**Q: Does MVC use `MethodInfo.Invoke` for every request?**
No. It builds and caches a faster invoker per action at startup. The reflection is in
the discovery phase.

**Q: Can I add a controller at run time?**
You can add an `ApplicationPart` and notify the change provider, and there are APIs for
it — but it is advanced and fragile. The common approach is to register all candidate
assemblies at startup.

**Q: Why do minimal APIs exist if MVC is this flexible?**
Less startup reflection, less ceremony, and they are far friendlier to AOT and source
generation. MVC's convention-based discovery is exactly the part AOT dislikes.

## 1.10 Documentation

- Routing to controller actions — <https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing>
- Application parts — <https://learn.microsoft.com/en-us/aspnet/core/mvc/advanced/app-parts>

---

# 2. Dependency Injection

## 2.1 The core insight

> **A constructor signature *is* a machine-readable dependency list.**

```csharp
internal sealed class OrderService(
    IOrderRepository repository,
    IPricingPolicy pricingPolicy,
    IAuditLog auditLog,
    IClock clock)
```

Verified:

```text
OrderService declares one constructor. Reflection reads it as a shopping list:

  needs IOrderRepository       (parameter 'repository')
  needs IPricingPolicy         (parameter 'pricingPolicy')
  needs IAuditLog              (parameter 'auditLog')
  needs IClock                 (parameter 'clock')
```

Nobody wrote a configuration file listing those. The dependency graph **is** the
constructor signature, and the signature is already in metadata. That is the whole
insight DI is built on.

## 2.2 Registration and resolution

```csharp
var services = new ServiceCollection();
services.AddSingleton<IClock, SystemClock>();
services.AddSingleton<IAuditLog, InMemoryAuditLog>();
services.AddScoped<IPricingPolicy, StandardPricingPolicy>();
services.AddTransient<IOrderRepository, InMemoryOrderRepository>();
services.AddTransient<OrderService>();

using ServiceProvider provider = services.BuildServiceProvider();
var orderService = provider.GetRequiredService<OrderService>();
```

One request resolves the entire graph transitively:

```text
provider.GetRequiredService<OrderService>() produced a full object graph:
  OrderService
    +-- InMemoryOrderRepository
    +-- StandardPricingPolicy
    +-- InMemoryAuditLog
    +-- SystemClock
```

**`GetService` vs `GetRequiredService`:**

```text
  GetService<IMissingService>()         = null
  GetRequiredService<IMissingService>() threw InvalidOperationException:
    No service for type 'Metaprogramming.DependencyInjectionDemo.IMissingService' has been registered.
```

Prefer `GetRequiredService`. A null that becomes a `NullReferenceException` later is
strictly worse than an immediate, named exception — the same argument as
`Type.GetType(throwOnError: true)` in Part 2 §4.2.

## 2.3 Service lifetimes

```text
  scope 1:
    singleton: 1, 1
    scoped   : 1, 1
    transient: 1, 2
  scope 2:
    singleton: 1, 1
    scoped   : 2, 2
    transient: 3, 4
```

| Lifetime | Instances | In ASP.NET Core |
|---|---|---|
| **Singleton** | one per provider | one for the whole application |
| **Scoped** | one per scope | **one per HTTP request** |
| **Transient** | a new one every resolution | every injection point |

**The classic bug: a singleton depending on a scoped service.** The scoped instance gets
captured for the life of the process — a *captive dependency*. If that scoped service is
a `DbContext`, you now have a non-thread-safe object shared across requests, which is a
genuinely nasty production failure.

`ValidateScopes` turns it into an exception instead of a slow leak:

```csharp
services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
```

Verified:

```text
  Cannot consume scoped service 'ScopedCounter' from singleton 'CaptiveDependencyService'.
```

This is on by default in the ASP.NET Core Development environment. Also consider
`ValidateOnBuild = true`, which surfaces missing registrations at startup rather than on
the first request that needs them.

## 2.4 How the container picks a constructor

```text
ReportBuilder has three constructors:
  .ctor()
  .ctor(IClock)
  .ctor(IClock, IAuditLog)

Case A: only IClock is registered.
  chosen -> .ctor(IClock)

Case B: IClock AND IAuditLog are registered.
  chosen -> .ctor(IClock, IAuditLog)
```

> **The rule: pick the constructor with the MOST parameters that the container can
> satisfy completely.**

Note the consequence demonstrated above: **adding a registration changed which
constructor runs.** A registration somewhere else in the application can silently alter
how your class is built. This is a real source of confusion and an argument for having
exactly one constructor.

**Ambiguity is an error, not a guess:**

```text
  InvalidOperationException: Unable to activate type 'AmbiguousService'. The following constructors are ambiguous:
    Void .ctor(IClock)
    Void .ctor(IAuditLog)
```

Two satisfiable constructors where neither parameter set contains the other. Fix by
removing one, or by marking the intended one `[ActivatorUtilitiesConstructor]`.

## 2.5 `ActivatorUtilities` — DI for unregistered types

`InvoiceGenerator` needs one service from DI **and** a runtime value the container
cannot know:

```csharp
internal sealed class InvoiceGenerator(IClock clock, string invoiceNumber);
```

`Activator.CreateInstance` cannot help — it knows nothing about `IClock`.

```csharp
var generator = ActivatorUtilities.CreateInstance<InvoiceGenerator>(provider, "INV-2026-0042");
```

Verified:

```text
ActivatorUtilities.CreateInstance<InvoiceGenerator>(provider, "INV-2026-0042"):
  Invoice INV-2026-0042 dated 2026-11-10 for 2,500.00
```

And for a hot path, resolve the *plan* once:

```csharp
ObjectFactory factory = ActivatorUtilities.CreateFactory(typeof(InvoiceGenerator), [typeof(string)]);
var invoice = (InvoiceGenerator)factory(provider, ["INV-0001"]);
```

```text
ActivatorUtilities.CreateFactory - resolve the plan once, reuse it:
  Invoice INV-0001 dated 2026-11-10 for 1,000.00
  Invoice INV-0002 dated 2026-11-10 for 1,000.00
  Invoice INV-0003 dated 2026-11-10 for 1,000.00
```

**When to use which:**

| API | Use when |
|---|---|
| `GetRequiredService<T>()` | the type is registered |
| `ActivatorUtilities.CreateInstance` | not registered, or needs extra arguments |
| `ActivatorUtilities.CreateFactory` | same, on a hot path — **cache the factory** |

This is how ASP.NET Core activates controllers, and it is what a plugin host *should*
use instead of the bare `Activator.CreateInstance` in Part 2 §9 — so plugins can take
constructor dependencies.

## 2.6 Convention-based registration

```csharp
foreach (Type implementation in assembly.GetTypes())
{
    if (implementation.IsAbstract || implementation.IsInterface) continue;
    if (!typeof(IEventHandler).IsAssignableFrom(implementation)) continue;
    if (!implementation.Name.EndsWith("Handler", StringComparison.Ordinal)) continue;

    services.AddTransient(typeof(IEventHandler), implementation);
}
```

Verified:

```text
  registered EmailNotificationHandler
  registered InventoryReservationHandler
  skipped LegacyEventListener - does not match the naming convention

Dispatching one event to all discovered handlers:
  EmailNotificationHandler queued mail for OrderPlaced at 09:30
  InventoryReservationHandler reserved stock for OrderPlaced
```

Resolving `IEnumerable<IEventHandler>` returns **every** registration for that service
type — the standard way to implement a dispatcher. This reflection happens **once at
startup**; nothing scans types per request. Scanning libraries such as **Scrutor**
package exactly this pattern.

## 2.7 A container in 60 lines — no magic left

```csharp
private object Resolve(Type serviceType, HashSet<Type> resolving)
{
    if (_instances.TryGetValue(serviceType, out object? cached)) return cached;

    if (!_registrations.TryGetValue(serviceType, out Type? implementationType))
        throw new InvalidOperationException("No registration for " + serviceType.Name + ".");

    if (!resolving.Add(serviceType))
        throw new InvalidOperationException("Dependency cycle involving " + serviceType.Name + ".");

    // Step 1: read the constructor. This is the reflection.
    ConstructorInfo constructor = implementationType.GetConstructors()
        .OrderByDescending(c => c.GetParameters().Length).First();

    // Step 2: resolve each parameter recursively.
    object[] arguments = constructor.GetParameters()
        .Select(p => Resolve(p.ParameterType, resolving)).ToArray();

    // Step 3: construct.
    object instance = constructor.Invoke(arguments);

    resolving.Remove(serviceType);
    _instances[serviceType] = instance;
    return instance;
}
```

Verified — it resolves the same graph as the real container:

```text
Resolved by MiniContainer (60 lines, at the bottom of this file):
  OrderService
    +-- InMemoryOrderRepository
    +-- StandardPricingPolicy
    +-- InMemoryAuditLog
    +-- SystemClock
```

**Three ideas:** a dictionary from service type to implementation type;
`GetConstructors()` to find out what an implementation needs; recursion, then construct.

A production container adds lifetimes, scopes, disposal, thread safety, open generics,
`IEnumerable<T>` resolution, and a compiled fast path. **The core idea is this.**

## 2.8 Do not over-claim: what the real container actually does

The syllabus specifically asks for this clarification, and it is worth getting right.

Verified by reading the provider's private `_engine` field:

```text
Package: Microsoft.Extensions.DependencyInjection 10.0.0.0
RuntimeFeature.IsDynamicCodeCompiled = True

ServiceProvider's internal engine on THIS runtime: DynamicServiceProviderEngine
```

> **It is accurate to say the container uses REFLECTION to read constructors.**
> **It is NOT accurate to say every container uses Reflection.Emit to call them.**

`Microsoft.Extensions.DependencyInjection` chooses an activation strategy at run time:

- a **reflection-based** engine, used immediately and always available;
- a **faster engine** built from compiled expression trees or emitted IL, which it
  switches to in the background once a service has been resolved enough times;
- the **reflection-only** engine when `RuntimeFeature.IsDynamicCodeCompiled` is
  `false` — the Native AOT case.

Other containers differ: some compile expression trees, some emit IL, some do neither.
**"DI uses reflection" is the safe claim.** Anything more specific depends on the
container, its version and the platform — so check, as the example does.

And one more thing worth saying out loud: **constructor injection does not *require*
reflection.** You can wire an entire application by hand with `new`. Containers use
reflection to spare you that wiring, not because the pattern demands it.

## 2.9 Common mistakes

1. Singleton depending on scoped (captive dependency) — enable `ValidateScopes`.
2. Multiple constructors, then surprise at which one runs.
3. Using the service locator pattern (`provider.GetService` everywhere) instead of
   constructor injection.
4. Registering a type both as itself and as an interface and expecting one instance —
   each registration is distinct. Register the concrete type and forward the interface
   to it.
5. Resolving scoped services from the root provider.
6. Not disposing scopes.

## 2.10 Audience questions

**Q: Does dependency injection always use reflection?**
No. The *pattern* needs none — hand-wiring is pure constructor calls. The *containers*
use reflection to read constructors, and some then generate code to call them.
Compile-time DI containers exist that use no reflection at all.

**Q: Is DI slow because of reflection?**
Resolution cost is dominated by graph size and lifetime bookkeeping, not reflection,
because the plan is cached after first use. Transient services in a deep graph cost more
than the reflection ever did.

**Q: Why does it pick the greediest constructor?**
So that optional dependencies are supplied when available. The cost is the surprise in
§2.4. One constructor per class avoids the whole question.

## 2.11 Documentation

- Dependency injection in .NET — <https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection>
- DI guidelines — <https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines>
- `ActivatorUtilities` — <https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.activatorutilities>

---

# 3. Entity Framework Core 10

## 3.1 Entity discovery

```csharp
public class BloggingContext(DbContextOptions<BloggingContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Author> Authors => Set<Author>();
}
```

EF reflects over those `DbSet<T>` properties to discover the root entity types.
Verified:

```text
The DbSet<T> properties declared on BloggingContext, read with reflection:
  DbSet<Blog> Blogs
  DbSet<Post> Posts
  DbSet<Author> Authors

The entity types EF actually put in the model:
  Author   -> table "authors", key = AuthorKey
  Blog     -> table "Blogs", key = Id
  Post     -> table "blog_posts", key = Id
```

**Three ways a type enters the model:**

1. a `DbSet<T>` property on the context (the usual way);
2. `modelBuilder.Entity<T>()` in `OnModelCreating`;
3. **being reachable by navigation** from a type already in the model.

That third rule catches people out: adding a navigation property can pull a whole new
type into the model and change your schema, with no `DbSet` in sight.

The model is built **once**, on first use, and cached for the application's lifetime —
the same pattern as MVC in §1.5.

## 3.2 EF Core model metadata vs CLR metadata

**The syllabus asks for this distinction explicitly, and the example makes it concrete.**

```text
CLR metadata for Post - what the C# compiler emitted:
  Int32       Id
  String      Title
  String      Content
  DateTime    PublishedOn
  Int32       Rating
  Int32       WordCount
  Int32       BlogId
  Blog        Blog
  Author      Author
  Int32?      AuthorId

EF Core model metadata for Post - what EF decided to map:
  Int32       Id              column "post_id"
  Int32?      AuthorId        column "AuthorId"
  Int32       BlogId          column "BlogId"
  String      Content         column "Content"
  DateTime    LastUpdatedUtc  column "last_updated_utc"  <- SHADOW: no CLR property exists
  DateTime    PublishedOn     column "PublishedOn"
  Int32       Rating          column "Rating"
  String      Title           column "Title"
```

The lists are **different**, in three distinct ways:

| Member | CLR | EF model | Why |
|---|---|---|---|
| `WordCount` | yes | **no** | `[NotMapped]` |
| `Blog`, `Author` | yes | **not as properties** | they are **navigations**, tracked separately |
| `LastUpdatedUtc` | **no** | yes | a **shadow property** |

> **The EF Core model is a SEPARATE model that EF BUILDS BY READING CLR metadata**, then
> augments with its own conventions and your configuration. It is not a view over
> reflection, and it is not limited to what reflection can see. **Reflection is the
> input; `IModel` is the output, and the output is richer.**

A **shadow property** is the cleanest proof. Declared in the model only:

```csharp
modelBuilder.Entity<Post>()
    .Property<DateTime>("LastUpdatedUtc")
    .HasColumnName("last_updated_utc");
```

There is no `post.LastUpdatedUtc` to assign, so it is reached by name through the change
tracker:

```csharp
entry.Property<DateTime>("LastUpdatedUtc").CurrentValue = ...;
```

## 3.3 Conventions, annotations, fluent API

The example configures three entities three different ways, deliberately.

**Convention** (`Blog`) — nothing configured, EF infers everything: a property named
`Id` becomes the key; a collection navigation makes a one-to-many; `Post.BlogId` is
inferred as the foreign key; the table is named after the `DbSet`.

**Data annotations** (`Post`) — explicit metadata on the type:

```csharp
[Table("blog_posts")]
public class Post
{
    [Key] [Column("post_id")] public int Id { get; set; }
    [Required] [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [NotMapped] public int WordCount => /* ... */;
}
```

**Fluent API** (`Author`) — no attributes on the class at all:

```csharp
modelBuilder.Entity<Author>(author =>
{
    author.ToTable("authors");
    author.HasKey(a => a.AuthorKey);          // convention would NOT find this key
    author.Property(a => a.Name).HasColumnName("full_name").HasMaxLength(120).IsRequired();
    author.HasIndex(a => a.EmailAddress).IsUnique();
});
```

> **Precedence, lowest to highest: CONVENTION < DATA ANNOTATION < FLUENT API.**

Why both exist: attributes are discoverable and sit next to the member; the fluent API
keeps EF types out of a domain class, supports configuration attributes cannot express
(attribute arguments must be compile-time constants — Part 2 §8.4), and can vary by
environment.

Note the parallel with Part 1 §5: **convention is implicit metadata inferred from the
declaration; annotations and fluent calls are explicit metadata you supplied.**

## 3.4 The schema EF generated

Verified, read back from SQLite itself:

```sql
CREATE TABLE "Blogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Blogs" PRIMARY KEY AUTOINCREMENT,
    "Title" TEXT NOT NULL,
    "Url" TEXT NULL,
    "CreatedOn" TEXT NOT NULL
)

CREATE TABLE "authors" (
    "AuthorKey" INTEGER NOT NULL CONSTRAINT "PK_authors" PRIMARY KEY AUTOINCREMENT,
    "full_name" TEXT NOT NULL,
    "email" TEXT NOT NULL
)

CREATE TABLE "blog_posts" (
    "post_id" INTEGER NOT NULL CONSTRAINT "PK_blog_posts" PRIMARY KEY AUTOINCREMENT,
    "Title" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "PublishedOn" TEXT NOT NULL,
    "Rating" INTEGER NOT NULL,
    "BlogId" INTEGER NOT NULL,
    "AuthorId" INTEGER NULL,
    "last_updated_utc" TEXT NOT NULL,
    CONSTRAINT "FK_blog_posts_Blogs_BlogId" FOREIGN KEY ("BlogId") REFERENCES "Blogs" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_blog_posts_authors_AuthorId" FOREIGN KEY ("AuthorId") REFERENCES "authors" ("AuthorKey") ON DELETE SET NULL
)

CREATE UNIQUE INDEX "IX_authors_email" ON "authors" ("email")
```

Every name in that DDL traces back to a piece of metadata: `blog_posts`/`post_id` from
attributes; `authors`/`full_name`/`email` from the fluent API; `Blogs` from convention;
`last_updated_utc` from the shadow property; no `WordCount` column because of
`[NotMapped]`; the unique index from `HasIndex(...).IsUnique()`.

## 3.5 Expression trees becoming SQL

This is where Part 4 §8 and EF Core meet, and it is the most impressive demo in the
session.

```csharp
IQueryable<Post> highRated = context.Posts.Where(p => p.Rating >= 4);
```

The tree EF is handed:

```text
[Microsoft.EntityFrameworkCore.Query.EntityQueryRootExpression].Where(p => (p.Rating >= 4))
```

The SQL it produces (`ToQueryString()` does **not** execute):

```sql
SELECT "b"."post_id", "b"."AuthorId", "b"."BlogId", "b"."Content", "b"."last_updated_utc", "b"."PublishedOn", "b"."Rating", "b"."Title"
FROM "blog_posts" AS "b"
WHERE "b"."Rating" >= 0 AND "b"."Rating" >= 4
```

Note the extra `Rating >= 0`: that is the **global query filter** declared with
`HasQueryFilter` in `OnModelCreating`. It is stored in the **model** as an expression
tree and combined into every query over `Post`.

A projection with navigations:

```csharp
context.Posts
    .Where(p => p.Rating >= 4 && p.Blog!.Title.Contains(".NET"))
    .OrderByDescending(p => p.PublishedOn)
    .Select(p => new { p.Title, Blog = p.Blog!.Title, Author = p.Author!.Name });
```

```sql
SELECT "b"."Title", "b0"."Title" AS "Blog", "a"."full_name" AS "Author"
FROM "blog_posts" AS "b"
INNER JOIN "Blogs" AS "b0" ON "b"."BlogId" = "b0"."Id"
LEFT JOIN "authors" AS "a" ON "b"."AuthorId" = "a"."AuthorKey"
WHERE "b"."Rating" >= 0 AND "b"."Rating" >= 4 AND instr("b0"."Title", '.NET') > 0
ORDER BY "b"."PublishedOn" DESC
```

EF translated the navigation into an `INNER JOIN`, the nullable navigation into a
`LEFT JOIN`, and the anonymous type into a `SELECT` list — so it never loaded a whole
`Post` entity.

> **Look at how `string.Contains` was rendered: `instr("b0"."Title", '.NET') > 0`.**
> That is a **SQLite** function, not `LIKE`. The **provider** chooses the translation,
> and the SQL Server provider emits something different for the same C#. This is why
> you should read the SQL your provider actually produces rather than assume —
> `ToQueryString()` exists for precisely that.

And a reusable, composable predicate — the real reason this matters:

```csharp
Expression<Func<Post, bool>> isRecommended = p => p.Rating >= 4;
IQueryable<Post> recommended = context.Posts.Where(isRecommended);
```

The **identical object** could instead be `.Compile()`d and run over a `List<Post>` in
memory (Part 4 §8). One definition of "recommended", two execution engines. That is
only possible because it is **data**, not a delegate.

## 3.6 The classic performance bug, made visible

```text
GOOD: context.Posts.Where(p => p.Rating >= 4).ToList()
  -> WHERE "b"."Rating" >= 0 AND "b"."Rating" >= 4     ← filtering in SQL

BAD : context.Posts.ToList().Where(p => p.Rating >= 4)
  -> WHERE "b"."Rating" >= 0                            ← every row transferred
```

Both produce the same answer. On a large table only one is usable.

The cause is a **type** difference that is invisible in the source:
`Where(Func<Post,bool>)` on `IEnumerable` and `Where(Expression<Func<Post,bool>>)` on
`IQueryable` look identical. An accidental `.AsEnumerable()` or `.ToList()` before
`.Where()` switches you from the second to the first, and filtering silently moves from
the database into your process — after downloading every row.

## 3.7 When translation fails

```csharp
context.Posts.Where(p => IsProofRead(p.Content)).ToList()
```

Verified:

```text
  InvalidOperationException:
    The LINQ expression 'DbSet<Post>() .Where(p => p.Rating >= 0)
    .Where(p => Program.IsProofRead(p.Content))' could not be translated
```

EF Core **refuses** rather than silently downloading the table and filtering in memory.
That refusal is deliberate: the silent version was the behaviour of older EF Core
releases and caused serious production surprises.

**Three fixes, in order of preference:**

1. **Rewrite** the predicate using constructs EF can translate.
2. **Narrow in SQL first, then finish in memory, explicitly:**

```csharp
context.Posts
    .Where(p => p.Rating >= 3)           // translated, runs in SQL
    .AsEnumerable()                       // the boundary, stated out loud
    .Where(p => IsProofRead(p.Content))   // ordinary LINQ to Objects
    .ToList();
```

3. **Push the logic into the database** as a function and map it with
   `modelBuilder.HasDbFunction`.

Also: what *is* translatable changes between EF versions and between providers. A query
that works on SQL Server may fail on SQLite. **The provider decides.**

## 3.8 Change tracking

```text
Loaded: "Reflection in practice" (rating 5)
  tracked state: Unchanged

After 'post.Rating = 3;' - we touched a plain C# property, nothing else:
  tracked state: Modified

  Modified properties EF detected by comparing against its snapshot:
    Rating: "5" -> "3"
```

EF kept the **original values** so it can generate a minimal `UPDATE`, obtained by
reading the entity's properties when it materialised the object.

And on how values get in and out of your objects: **EF Core does not call
`PropertyInfo.GetValue` per property per row.** It builds accessor delegates once per
property — from expression trees on a JIT runtime — and reuses them. Same lesson as
Part 3 §2.3: reflect once, then run compiled code.

## 3.9 The whole pipeline

```text
  stage                     mechanism                         when
  --------------------------------------------------------------------------
  find DbSet<T> properties  System.Reflection                 startup (once)
  read annotations          GetCustomAttributes               startup (once)
  apply conventions         EF's convention set               startup (once)
  apply fluent config       your OnModelCreating              startup (once)
  build IModel              EF model builder                  startup (once)
  build accessors           expression trees -> delegates     first use, cached
  translate a query         ExpressionVisitor chain -> SQL    per query shape, then cached
  materialise rows          cached accessor delegates         per row
```

Every technique in this session appears in that table: Part 2 reflection, Part 3 code
generation, Part 4 expression trees.

## 3.10 Audience questions

**Q: How does EF Core translate LINQ into SQL?**
The query arrives as an **expression tree** because `IQueryable<T>.Where` takes
`Expression<Func<T,bool>>`, not `Func<T,bool>`. EF runs a chain of `ExpressionVisitor`s
over it: identify the query root, bind member accesses to mapped properties and
navigations, convert navigations to joins, translate method calls to provider-specific
SQL functions, then print SQL. The translation is cached per query *shape*, so a loop
executing the same query with different parameter values translates once.

**Q: Does EF Core use reflection per row?**
No. Accessor delegates are compiled once per property and cached.

**Q: Is the EF model the same as my classes?**
No — §3.2. It is a richer, separate model built *from* your classes.

**Q: Why does the same LINQ work on SQL Server but not SQLite?**
Translation is provider-specific. Each provider supports a different set of functions.
Always check with `ToQueryString()`.

## 3.11 Documentation

- EF Core — <https://learn.microsoft.com/en-us/ef/core/>
- Creating and configuring a model — <https://learn.microsoft.com/en-us/ef/core/modeling/>
- How queries work — <https://learn.microsoft.com/en-us/ef/core/querying/how-query-works>
- Client vs server evaluation — <https://learn.microsoft.com/en-us/ef/core/querying/client-eval>

---

# 4. Serialization

## 4.1 What a serializer must figure out

To turn an arbitrary object into JSON, a library must answer at run time: which members
to write; what name each gets; each member's type; how to **read** each value; and, on
the way back, which constructor or setters to use.

Every answer is in metadata, so the Part 2 API suffices:

```text
  Int32              InvoiceId       -> "invoice_id"
  String             CustomerName
  DateOnly           IssuedOn
  Decimal            AmountDue
  InvoiceStatus      Status
  List`1             Lines
  String             InternalNotes   [JsonIgnore]
```

**This is why serialization is the canonical metaprogramming example:** the library
author cannot know your types, so the library must ask the types themselves.

## 4.2 A 20-line serializer, to remove the magic

```csharp
private static void WriteObject(StringBuilder builder, object instance)
{
    PropertyInfo[] properties = instance.GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance);

    builder.Append('{');
    bool first = true;
    foreach (PropertyInfo property in properties)
    {
        if (!property.CanRead) continue;
        if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null) continue;

        string name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? char.ToLowerInvariant(property.Name[0]) + property.Name[1..];

        if (!first) builder.Append(',');
        builder.Append('"').Append(name).Append("\":");
        WriteValue(builder, property.GetValue(instance));
        first = false;
    }
    builder.Append('}');
}
```

`GetProperties()` → `GetValue()` → format → write a pair. That is the whole idea. What a
real serializer adds on top is escaping, cycles, numbers, culture, nulls, polymorphism,
dictionaries, streaming, async and speed. **Use `System.Text.Json`. Write `MiniJson`
once, to understand it.**

## 4.3 Attributes that steer serialization

```csharp
internal sealed class Invoice
{
    [JsonPropertyName("invoice_id")] public int InvoiceId { get; set; }
    [JsonPropertyOrder(-1)] public string CustomerName { get; set; } = string.Empty;
    [JsonConverter(typeof(IsoDateOnlyConverter))] public DateOnly IssuedOn { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter<InvoiceStatus>))] public InvoiceStatus Status { get; set; }
    [JsonIgnore] public string? InternalNotes { get; set; }
}
```

Two observations worth making in the talk:

- **`[JsonPropertyName]` beats `PropertyNamingPolicy`.** An explicit attribute always
  wins over a global convention — the same "explicit overrides implicit" rule as
  Part 1 §5 and EF Core §3.3.
- **`InternalNotes` exists on the object but never reaches the JSON.** One attribute
  keeps internal data out of an API response, enforced by the serializer rather than by
  every caller.

## 4.4 Reflection mode vs source-generated mode

```csharp
// RUNTIME: reflects over Invoice on first use, then caches the plan.
string a = JsonSerializer.Serialize(invoice, options);

// COMPILE-TIME: the plan was generated during the build.
string b = JsonSerializer.Serialize(invoice, InvoiceJsonContextCompact.Default.Invoice);
```

The source-generated context has **no body we wrote**:

```csharp
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Invoice))]
internal partial class InvoiceJsonContextCompact : JsonSerializerContext;
```

Proof the generator filled it in, read from the compiled assembly:

```text
  type     : Metaprogramming.SerializationDemo.InvoiceJsonContext
  base     : JsonSerializerContext
  generated properties:
    JsonTypeInfo`1 Decimal
    JsonTypeInfo`1 Invoice
    JsonTypeInfo`1 InvoiceLine
    JsonTypeInfo`1 InvoiceStatus
    JsonTypeInfo`1 ListInvoiceLine
```

We declared `[JsonSerializable(typeof(Invoice))]` — **one** type. The generator produced
**five** `JsonTypeInfo` properties, because it walked the whole object graph
transitively: `Invoice` needs `List<InvoiceLine>`, which needs `InvoiceLine`, which
needs `decimal`, and `Invoice` needs `InvoiceStatus`. That transitive closure is exactly
why source generation can replace run-time discovery — and also why adding a property
of a brand-new type requires a rebuild.

And the outputs:

```text
Byte-for-byte identical: True
```

| aspect | reflection mode | source-generated mode |
|---|---|---|
| when the plan is built | first call, at run time | during the build |
| mechanism | `System.Reflection`, then generated accessors | Roslyn generator emits C# |
| startup cost | pay on first use per type | none |
| trimming | unsafe: members may be trimmed | safe: all usage is visible |
| Native AOT | not supported | **supported** |
| new type support | automatic | add `[JsonSerializable]` and rebuild |
| polymorphism, dynamic shapes | more flexible | more restricted |

**Same goal, same output. The only real difference is WHEN the metaprogramming
happened** — Part 1 §4 made concrete.

## 4.5 The caching claim, measured honestly

The standard advice is "create `JsonSerializerOptions` once and reuse it", usually
justified with "otherwise it re-reflects every call". That justification is only half
right on current .NET. Measured three ways:

```text
  5,000 serializations of the same object:
    one reused options instance          14 ms   1.0x
    new options, identical settings      19 ms   1.4x
    new options, DIFFERENT settings     101 ms   7.2x
```

- Reusing one instance is fastest, as expected.
- **New-but-identical options is only moderately slower.** `System.Text.Json` keeps a
  process-wide cache of serialization metadata that *equivalent* options instances
  share, so the expensive reflection is not repeated. You still pay for the allocation
  and the cache lookup.
- **New-and-different options is dramatically slower.** Nothing can be shared, so the
  type really is analysed again for every distinct configuration.

> **The correct statement: reuse your options instance, and above all do not vary its
> settings per call.** The second mistake is the expensive one.

## 4.6 Deserialization is harder

```text
  InternalNotes : (null)  <- [JsonIgnore] means it was never written, so it cannot come back
```

Why the inbound direction is harder:

- Writing needs only a getter. Reading must **create** the object first.
- It must choose a constructor, then match JSON names to parameters.
- For `init`-only or `readonly` members it must use the constructor, not setters.
- Missing members, extra members and nulls all need a policy.

**This is the same constructor-selection problem** the DI container solves (§2.4) and
that `Activator.CreateInstance` faces (Part 2 §7). Three unrelated-looking features, one
underlying metadata question — a good point to make on slide 25.

Two run-time behaviours worth knowing:

```text
  JsonException: The JSON value could not be converted to System.Int32.

And an UNKNOWN property is silently ignored by default, which surprises people:
  deserialized InvoiceId = 99, unknown member discarded
```

Set `JsonSerializerOptions.UnmappedMemberHandling = Disallow` to make unknown members an
error.

## 4.7 Guidance

**Source generation when:** publishing Native AOT or trimmed; startup time matters
(serverless, CLI); the set of types is known at build time (the usual case).

**Reflection mode when:** types are genuinely unknown until run time (a plugin's own
models); you need broad polymorphic support; you are prototyping.

**Where else this exact pattern appears:** the regex source generator vs
`RegexOptions.Compiled`; the logging source generator (`[LoggerMessage]`); the
configuration binder generator; minimal API request-delegate generation. **The direction
of travel in .NET is consistently from run time to build time.**

## 4.8 Documentation

- `System.Text.Json` overview — <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview>
- Source generation — <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation>

---

# 5. Plugin Architecture

Fully covered in **[Part 2 §9](Part-2-Runtime-Introspection.md)** — the contract design,
`AssemblyLoadContext`, type identity across contexts, unloading and its limits, version
compatibility, and the security analysis.

The three points to repeat on slide 26:

1. **Interface + attribute + reflection + activation = a plugin system in 20 lines.**
2. **Unloading is cooperative.** `Unload()` is a request; one cached `Type` or
   subscribed delegate keeps the context alive forever.
3. **`AssemblyLoadContext` is not a security sandbox.** It isolates **type identity**,
   not **privileges**. There is no supported in-process sandbox in modern .NET; use a
   process, container or VM boundary for untrusted code.

The one upgrade to make here: a real host should activate plugins with
**`ActivatorUtilities`** (§2.5) rather than bare `Activator.CreateInstance`, so plugins
can declare constructor dependencies the host controls.

---

# 6. Dynamic Proxies

## 6.1 The problem

The service you want:

```csharp
public decimal GetBalance(string account) => _balances[account];
```

What it becomes once the operational requirements land:

```csharp
public decimal GetBalance(string account)
{
    _logger.LogInformation("GetBalance {Account}", account);      // logging
    var sw = Stopwatch.StartNew();                                // metrics
    if (_cache.TryGetValue(account, out var hit)) return hit;     // caching
    try
    {
        var result = _balances[account];                          // the actual work
        _cache[account] = result;
        return result;
    }
    catch (Exception ex) { _logger.LogError(ex, "..."); throw; }
    finally { _metrics.Record(sw.Elapsed); }                      // metrics
}
```

One line of business logic, ten lines of plumbing — **repeated in every method of every
service**. These are **cross-cutting concerns**: orthogonal to the business logic, yet
physically tangled into it.

## 6.2 `DispatchProxy`

`DispatchProxy` generates a type at run time that implements an interface and routes
**every** call through one method you override.

```csharp
public class LoggingProxy : InterceptingProxy
{
    public List<string> Log { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        Log.Add("--> " + targetMethod!.Name + "(" + Format(args) + ")");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            object? result = InvokeTarget(targetMethod, args);
            stopwatch.Stop();
            Log.Add("<-- " + targetMethod.Name + " returned " + result
                + " in " + stopwatch.Elapsed.TotalMilliseconds.ToString("N2") + " ms");
            return result;
        }
        catch (Exception ex)
        {
            Log.Add("!!! " + targetMethod.Name + " threw " + ex.GetType().Name);
            throw;
        }
    }
}
```

Created with:

```csharp
object proxy = DispatchProxy.Create<TInterface, TProxy>()!;
```

Verified:

```text
Calls made through the proxy (ordinary C#, no reflection at the call site):

  GetBalance("SB-1001")  = 25400.00
  Deposit("SB-1001", 500) = 25900.00
  GetAccountCount()       = 2

What the interceptor recorded, without AccountService knowing it exists:

  --> GetBalance(SB-1001)
  <-- GetBalance returned 25400.00 in 0.73 ms
  --> Deposit(SB-1001, 500)
  <-- Deposit returned 25900.00 in 0.26 ms
  --> GetAccountCount()
  <-- GetAccountCount returned 2 in 0.11 ms
```

**`AccountService.cs` contains no logging code at all.**

## 6.3 What was actually created

```text
The object we are holding:
  type name      : generatedProxy_1
  full name      : generatedProxy_1
  base type      : LoggingProxy  <- our LoggingProxy
  implements     : IAccountService
  assembly       : ProxyBuilder
  IsDynamic      : True  <- generated at run time

It is NOT an AccountService:
  proxied is AccountService  = False
  proxied is IAccountService = True
  proxied is DispatchProxy   = True
```

**`DispatchProxy` did exactly what Part 3 §5 did by hand** — defined a type at run time,
made it implement a known interface, created an instance. It wrote the `TypeBuilder`
code for us, and every interface method it generated calls our `Invoke` override.

This is the bridge between Part 3 and Part 4: the generated-type pattern, packaged.

## 6.4 Attribute-driven caching

```csharp
public interface IAccountService
{
    [Cacheable] decimal GetBalance(string accountNumber);
    decimal Deposit(string accountNumber, decimal amount);
    int GetAccountCount();
}
```

Verified:

```text
Calling GetBalance three times with the same argument:
  call 1 -> 25400.00   (service hit count: 1)
  call 2 -> 25400.00   (service hit count: 1)
  call 3 -> 25400.00   (service hit count: 1)

  cache hits   : 2
  cache misses : 1
  the real service was read 1 time(s) for GetBalance
```

The attribute is the whole policy. Marking a new method `[Cacheable]` changes behaviour
with no change to the proxy and none to the service.

> **Worth stating clearly: metaprogramming provided the SEAM, not the POLICY.**
> This cache has no expiry, no size limit, no invalidation and no thread safety. A real
> one needs all four — use `HybridCache` or `IMemoryCache` and think about correctness.
> The same applies to **authorization**: a proxy can be *where* you enforce a policy,
> but it does not supply the policy or make it correct.

The syllabus asks for exactly this clarification, and it is the honest framing.

## 6.5 Correct exception handling — the detail that bites

`targetMethod.Invoke(...)` wraps any exception from the target in
`TargetInvocationException`. If a proxy lets that escape, **every
`catch (TimeoutException)` upstream silently stops matching.**

```csharp
protected object? InvokeTarget(MethodInfo targetMethod, object?[]? args)
{
    try
    {
        return targetMethod.Invoke(Target, args);
    }
    catch (TargetInvocationException ex) when (ex.InnerException is not null)
    {
        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        throw; // unreachable
    }
}
```

Verified — the caller catches the *real* exception type:

```text
  caught TimeoutException: Gateway timed out (call 2).
```

`ExceptionDispatchInfo` rather than `throw ex.InnerException`, because the latter resets
the stack trace.

## 6.6 Composition

```text
Chain: caller -> LoggingProxy -> CachingProxy -> AccountService

Log from the outer proxy:
  --> GetBalance(SB-1001)
  <-- GetBalance returned 25400.00 in 0.03 ms
  --> GetBalance(SB-1001)
  <-- GetBalance returned 25400.00 in 0.00 ms
  --> GetBalance(SB-1001)
  <-- GetBalance returned 25400.00 in 0.01 ms

  cache hits = 2, misses = 1, real service reads = 1
```

Each proxy is just another `IAccountService`, so they compose freely. **Order matters:**
logging outside caching logs every call; logging inside caching would log only the
misses.

## 6.7 Registering in DI

```csharp
services.AddSingleton<AccountService>();
services.AddSingleton<IAccountService>(provider =>
{
    var inner = provider.GetRequiredService<AccountService>();
    return InterceptingProxy.Wrap<IAccountService, LoggingProxy>(inner);
});
```

Verified — the consumer has an ordinary constructor dependency and no idea:

```text
MonthEndReport asked for IAccountService and got a proxy:
  injected type: generatedProxy_1
```

This is the **decorator pattern with a generated decorator**. Scrutor's
`Decorate<TService, TDecorator>()` packages this registration.

## 6.8 Limitations — all verified

**A. Interfaces only.** `DispatchProxy` cannot proxy a class, sealed or not:

```text
   ArgumentException: The type 'AccountService' must be an interface, not a class. (Parameter 'interfaceType')
```

Consequence: your services must be behind interfaces. Castle DynamicProxy can also
subclass a class, but then only intercepts **virtual** members — a non-virtual method
can never be intercepted by subclassing.

(.NET 10 also has a non-generic `DispatchProxy.Create(Type, Type)`, useful when the
interface type is only known at run time.)

**B. The proxy base class must not be sealed.** The generated type derives from it:

```text
     ArgumentException: The base type '...' cannot be sealed. (Parameter 'TProxy')
```

It must also have an accessible parameterless constructor, which is why configuration
happens after creation (`SetTarget`) rather than in a constructor.

**C. Only calls through the interface are intercepted.** Holding the real object
bypasses the proxy entirely — and a call from one method of the service to another is
**also not intercepted**, because `this` inside the service is the real object, not the
proxy. **That self-call blind spot surprises people and breaks naive caching.**

**D. Async needs deliberate handling.** `Invoke` returns `object?`. For an `async`
method that object is a `Task`, returned the instant the method starts — so a naive
stopwatch around `InvokeTarget` measures only how long it took to *create* the `Task`.
To time or catch properly you must await the returned task, which means inspecting the
return type and handling `Task`, `Task<T>` and `ValueTask<T>` separately. (The timings in
§6.2 are honest only because `IAccountService` is entirely synchronous.)

**E. Per-call cost.** Every call goes through `targetMethod.Invoke` — the slow reflective
path measured in Part 3 §2.3. For a hot inner loop, cache a delegate per `MethodInfo`,
or do not use a proxy.

**F. Native AOT.** `DispatchProxy` generates a type at run time, so it needs dynamic
code support and does not work under Native AOT.

**G. Debugging and clarity.** A stack trace now runs through a generated type, and "go
to definition" on an interface no longer reaches the code that runs. **Interception is
powerful precisely because it is invisible, and that is also its main cost to the next
person reading the code.**

## 6.9 Audience questions

**Q: What are the limitations of dynamic proxies?**
§6.8, and the first three matter most: interfaces only, self-calls are not intercepted,
and async needs explicit handling.

**Q: `DispatchProxy` or Castle DynamicProxy?**
`DispatchProxy` is built in — no package, no licence question — but interface-only and
minimal. Castle handles virtual class members, has a richer interceptor pipeline, and
better async support. Most production AOP in .NET uses Castle, usually via Autofac
interceptors.

**Q: Is this AOP?**
It is one implementation of AOP's interception idea. Classical AOP (AspectJ) weaves at
compile time; .NET IL weavers do it post-compile; proxies do it at run time with a
wrapper object.

## 6.10 Documentation

- `DispatchProxy` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy>

---

# 7. Cross-Cutting Concerns — Choosing the Right Seam

## 7.1 The four seams

```text
  seam              scope                        reaches
  ------------------------------------------------------------------------
  Middleware        every HTTP request           the whole pipeline
  MVC filters       controller actions           per action/controller
  Dynamic proxies   any interface call           any layer, incl. services
  Source generators compile time                 anything, with zero runtime cost
```

## 7.2 Choosing by where the concern belongs

| Concern | Seam | Why |
|---|---|---|
| Authentication, CORS, HTTPS redirect, request logging | **Middleware** | it is about the HTTP request, not about any one method |
| Model validation, authorization on an action, result shaping | **Filters** | they know MVC concepts: model state, action results |
| Retry on a flaky dependency, caching a repository, auditing domain operations | **Proxies / decorators** | the concern is about a service interface and applies however it is called, HTTP or not |
| High-volume logging, serialization, mapping | **Source generators** | the shape is known at build time; zero run-time cost |

**The test:** would this concern still apply if the call arrived from a background job
instead of HTTP? If yes, it belongs at the service seam (proxy or decorator), not in
middleware.

## 7.3 Logging — the worked example

§6.2 is the logging example: `LoggingProxy` records entry, exit, elapsed time and
exceptions for every method of any interface, with no change to the service.

## 7.4 Caching and authorization need real policies

Repeating §6.4 because the syllabus asks for it explicitly, and because it is the most
important caveat in this section:

> **Metaprogramming gives you the interception point. It does not give you a correct
> caching strategy or a correct authorization model.**

- **Caching** needs expiry, size limits, invalidation, stampede protection and thread
  safety. The demo cache in example 12 has none of these. Use `HybridCache` or
  `IMemoryCache`, and think hardest about *invalidation*.
- **Authorization** needs a policy: who the caller is, what they may do, and what
  happens when they may not. ASP.NET Core's authorization system supplies that. A proxy
  can be *where* the check runs; it is not the check.

An interception mechanism that silently applies a wrong policy is worse than no
interception, because the wrongness is now invisible.

## 7.5 The answer that is right more often than any of them

**A hand-written decorator class.**

```csharp
public sealed class CachingAccountService(IAccountService inner, IMemoryCache cache) : IAccountService
{
    public decimal GetBalance(string account) =>
        cache.GetOrCreate(account, _ => inner.GetBalance(account));

    public decimal Deposit(string account, decimal amount) => inner.Deposit(account, amount);
    public int GetAccountCount() => inner.GetAccountCount();
}
```

Twenty lines of ordinary C#: fully debuggable, trimmable, AOT-safe, obvious to the next
reader, and no reflection per call. **Reach for a generated proxy when the number of
interfaces makes hand-writing them genuinely impractical** — not before.

## 7.6 AOP concepts, briefly

| Term | Meaning |
|---|---|
| **Cross-cutting concern** | a requirement that spans many modules (logging, security) |
| **Aspect** | the encapsulation of one such concern |
| **Join point** | a place where an aspect could apply (a method call) |
| **Pointcut** | the rule selecting join points (`[Cacheable]`, or "all methods of IFoo") |
| **Advice** | the code that runs (before / after / around) |
| **Weaving** | combining aspects with the code — at compile time, post-compile (IL weaving), or run time (proxies) |

In .NET: `[Cacheable]` is a pointcut, `CachingProxy.Invoke` is around-advice, and
`DispatchProxy.Create` is run-time weaving.

---

# 8. Expression Trees

## 8.1 The same lambda, two completely different things

```csharp
Func<int, bool> asDelegate = n => n > 18;             // compiled IL — you can CALL it
Expression<Func<int, bool>> asExpression = n => n > 18; // an object GRAPH — you can READ it
```

Verified:

```text
Source text is identical in both cases: n => n > 18

Func<int, bool>:
  runtime type : Func`2
  can call it  : asDelegate(21) = True
  can read it  : no - the body is compiled IL
  Method.Name  : <DelegateVersusExpression>b__1_0  (a compiler-generated method)

Expression<Func<int, bool>>:
  runtime type : Expression1`1
  NodeType     : Lambda
  Body         : (n > 18)
  Body.NodeType: GreaterThan
  Parameters   : Int32 n
  ToString()   : n => (n > 18)
  can call it  : not directly - Compile() first
```

> **The difference is decided entirely by the TYPE you assign the lambda to.**
> `Func<...>` → compile the body to IL.
> `Expression<...>` → compile code that **builds a description** of the body.

**This is the one place where C# itself does compile-time metaprogramming on your
behalf:** it hands you your own source code as inspectable data.

## 8.2 The tree really is a tree

```csharp
Expression<Func<Customer, bool>> predicate = c => c.Age > 18 && c.Country == "IN";
```

Verified:

```text
Structure:
  AndAlso   (BinaryExpression, Boolean)
    GreaterThan   (BinaryExpression, Boolean)
      MemberAccess  -> Age : Int32
        Parameter     -> c : Customer
      Constant      -> 18 : Int32
    Equal   (BinaryExpression, Boolean)
      MemberAccess  -> Country : String
        Parameter     -> c : Customer
      Constant      -> IN : String
```

Every node is a strongly typed object:

```text
  and.NodeType            = AndAlso
  left.NodeType           = GreaterThan
  member.Member.Name      = Age   (a Property on Customer)
  constant.Value          = 18 (Int32)
```

Because every node is an object, a library can ask *"which property is being compared,
and to what?"* — **precisely the question a SQL translator must answer.**

## 8.3 `Compile()` — back to executable code

```csharp
Func<Customer, bool> compiled = predicate.Compile();
```

```text
predicate.Compile() returned a Func`2

  Asha   age 29 IN  -> True
  Ravi   age 16 IN  -> False
  Mia    age 34 US  -> False
```

**`Compile()` is expensive** — it generates and JITs code, using the same lightweight
code generation as `DynamicMethod`. Compile once, cache the delegate, call it many
times. Compiling inside a loop or per web request is the most common performance mistake
with this API.

And a precision point: **`Compile()` needs runtime code generation, so it is unavailable
under Native AOT. Expression trees can still be BUILT and READ there** — which is why EF
Core can translate queries to SQL under AOT, because translation only *reads* the tree.
It is `Compile()` that needs the JIT.

## 8.4 Building a tree by hand

```csharp
ParameterExpression a = Expression.Parameter(typeof(int), "a");
ParameterExpression b = Expression.Parameter(typeof(int), "b");
BinaryExpression sum = Expression.Add(a, b);
ConstantExpression two = Expression.Constant(2, typeof(int));
BinaryExpression doubled = Expression.Multiply(sum, two);
Expression<Func<int, int, int>> lambda = Expression.Lambda<Func<int, int, int>>(doubled, a, b);
Func<int, int, int> fn = lambda.Compile();
```

Verified:

```text
  Expression.Add(a, b)                     -> (a + b)
  Expression.Multiply(sum, two)            -> ((a + b) * 2)
  Expression.Lambda<Func<int,int,int>>(..)  -> (a, b) => ((a + b) * 2)

  fn(3, 4)   = 14
  fn(10, 11) = 42
```

**Compare with Part 3, which built the same function in IL:**

| | Reflection.Emit | Expression tree |
|---|---|---|
| Code | `ldarg.0 / ldarg.1 / add / ldc.i4.2 / mul / ret` | `Add(a,b)` → `Multiply(…, 2)` → `Lambda` |
| Type-checked as you build | **no** | **yes — `Expression.Add` throws immediately on a type mismatch** |
| Printable | no | yes, via `ToString()` |
| Speed after compilation | same | same |
| Can create a **type** | yes | **no** |

Recall Part 3 §8.2, where `add` on an `int` and a string **silently returned garbage**.
`Expression.Add` would have thrown on the spot.

> **Prefer expression trees over Reflection.Emit whenever you need a FUNCTION.**
> **Reach for Emit only when you need a TYPE, which `Expression` cannot create.**

## 8.5 A dynamic filter from runtime input

Filters that could have come from a query string (`?minAge=21&country=IN`):

```csharp
Expression<Func<T, bool>> Build<T>(IReadOnlyDictionary<string, string> filters)
{
    ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
    Expression? body = null;

    foreach ((string propertyName, string filter) in filters)
    {
        PropertyInfo? property = typeof(T).GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null) throw new ArgumentException(/* ... */);

        MemberExpression member = Expression.Property(parameter, property);
        object typedValue = Convert.ChangeType(filter[1..], property.PropertyType);
        ConstantExpression constant = Expression.Constant(typedValue, property.PropertyType);

        Expression comparison = filter[..1] switch
        {
            ">" => Expression.GreaterThan(member, constant),
            "<" => Expression.LessThan(member, constant),
            "=" => Expression.Equal(member, constant),
            _ => throw new ArgumentException(/* ... */)
        };

        body = body is null ? comparison : Expression.AndAlso(body, comparison);
    }

    body ??= Expression.Constant(true);
    return Expression.Lambda<Func<T, bool>>(body, parameter);
}
```

Verified:

```text
Filters supplied at runtime: Age>21, Country=IN

Expression built from those filters:
  x => ((x.Age > 21) AndAlso (x.Country == "IN"))

Matches:
  Asha (29, IN)
  Deepa (41, IN)
```

Note `Convert.ChangeType` then `Expression.Constant(typedValue, property.PropertyType)`:
typing the constant correctly is what keeps the comparison valid. An `int` property
compared against a `string` constant throws when you build the node — immediately, with
a clear message.

**Why an `Expression` and not a `Func`?** Because the same object can either be compiled
and run in memory, or handed to EF Core and turned into a SQL `WHERE` clause (§3.5). A
`Func` could only ever do the first.

## 8.6 Rewriting with `ExpressionVisitor`

Trees are **immutable**. A visitor does not edit; it produces a new tree.

```csharp
internal sealed class ConstantRaiser(int delta) : ExpressionVisitor
{
    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Type == typeof(int) && node.Value is int value)
        {
            return Expression.Constant(value + delta, typeof(int));
        }

        return base.VisitConstant(node);
    }
}
```

Verified:

```text
Original : c => ((c.Age > 18) AndAlso (c.Country == "IN"))
Rewritten: c => ((c.Age > 21) AndAlso (c.Country == "IN"))   (every int constant raised by 3)

Nithya is 20 and in IN:
  original (Age > 18) -> True
  rewritten (Age > 21)-> False
```

A read-only visitor is just as useful:

```text
  node census of the original tree:
    Parameter x 3
    MemberAccess x 2
    Constant x 2
    Lambda x 1
    AndAlso x 1
    GreaterThan x 1
    Equal x 1
```

`Parameter x 3` for a lambda with one parameter looks wrong but is correct: the single
`ParameterExpression` object appears three times in the traversal — once in the lambda's
parameter list and once inside each of the two `MemberAccess` nodes (`c.Age`,
`c.Country`). A tree is a **graph with shared nodes**, not a strict tree, and a visitor
walks each edge. Worth knowing before you write a visitor that counts or mutates.

**`ExpressionVisitor` is the standard way to process trees. EF Core's query pipeline is
a long chain of visitors, each rewriting the tree a little closer to something that can
be printed as SQL.**

## 8.7 `IQueryable` — why LINQ providers need expressions

```csharp
// IEnumerable<T>.Where takes Func<T, bool>  — LINQ to Objects just runs it.
IEnumerable<Customer> asEnumerable = customers.Where(c => c.Age > 18 && c.Country == "IN");

// IQueryable<T>.Where takes Expression<Func<T, bool>> — the provider gets the TREE.
IQueryable<Customer> asQueryable = customers.AsQueryable()
    .Where(c => c.Age > 18 && c.Country == "IN")
    .OrderBy(c => c.Name);
```

Verified:

```text
IQueryable<Customer>.Where(...).OrderBy(...)
  Where's parameter type: Expression<Func<Customer, bool>>
  nothing has executed yet. What exists is an expression tree:

  Metaprogramming.ExpressionTreesDemo.Customer[]
    .Where(c => ((c.Age > 18) AndAlso (c.Country == "IN")))
    .OrderBy(c => c.Name)

  provider: EnumerableQuery`1
  element : Customer
  result (on enumeration): Asha, Deepa
```

**Swap the provider and the same tree has a different destiny:**

| Provider | What it does with the tree |
|---|---|
| `EnumerableQuery` (LINQ to Objects) | `Compile()` it and run it in memory |
| **EF Core** | translate it to **SQL**, run it in the database |
| an OData / HTTP provider | translate it to a **URL query string** |

**That substitutability is the entire reason LINQ providers can exist, and it works only
because the query arrived as DATA rather than as compiled code.**

## 8.8 The limits — two different failures, often confused

**A. Not every C# construct can appear in an `Expression<TDelegate>`.** The compiler
**rejects** these at build time inside an expression lambda:

- statement bodies: `x => { var y = x * 2; return y; }`
- assignment: `x => x = 5`
- `await`
- `ref`/`out` arguments, unsafe code, lambdas using `dynamic`
- null-propagating `?.` and some pattern-matching forms

These are **compile errors**, not runtime failures. Note the precision: the underlying
`System.Linq.Expressions` **API** *can* represent blocks and assignment
(`Expression.Block`, `Expression.Assign`) — it is the **C# lambda syntax** that cannot.

**B. Not every valid expression tree can be translated by a given provider.** A separate,
later failure, surfacing at **run time**:

```text
     customers.AsQueryable().Where(c => IsVipCustomer(c.Name)) ->
       LINQ to Objects ran it happily: 1 match(es).
       EF Core would throw InvalidOperationException: 'could not be translated'.
```

And EF Core really does (§3.7):

```text
    The LINQ expression '...Where(p => Program.IsProofRead(p.Content))' could not be translated
```

> **The distinction worth teaching:**
> **"cannot be EXPRESSED as a tree" → a C# compile error.**
> **"cannot be TRANSLATED to SQL" → a provider exception at run time.**
> Different failures, different fixes — and easy to confuse, because both get described
> as "expression trees do not support that".

**C.** `Compile()` is slow and needs dynamic code support.
**D.** A tree is immutable: every "change" allocates a new tree.
**E.** Debugging a compiled expression gives you no source to step through.

## 8.9 Audience questions

**Q: What is the difference between reflection and expression trees?**
Reflection inspects **declarations** — what members exist. Expression trees represent
**code** — what an operation does. Reflection tells you `Customer` has an `Age`
property; an expression tree tells you someone wrote `c.Age > 18`. They are often used
together: §8.5 uses reflection (`GetProperty`) to build the tree.

**Q: Expression trees or Reflection.Emit?**
Expression trees, unless you need a *type*. Same speed class, type-checked as you build,
printable, and they fail loudly rather than silently (Part 3 §8).

**Q: Why is `IQueryable`'s `Where` different from `IEnumerable`'s?**
Only in the parameter type: `Expression<Func<T,bool>>` vs `Func<T,bool>`. That single
difference is what lets a provider translate instead of execute — and why an accidental
`.ToList()` changes where your filtering happens (§3.6).

**Q: Can I convert a `Func` back into an `Expression`?**
No. The delegate is compiled IL; the structure is gone. This is why EF Core requires
`Expression<Func<...>>` and why `Compile()` is a one-way door.

## 8.10 Documentation

- Expression trees (C# guide) — <https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/>
- `Expression<TDelegate>` — <https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expression-1>
- `ExpressionVisitor` — <https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressionvisitor>
- `IQueryable` — <https://learn.microsoft.com/en-us/dotnet/api/system.linq.iqueryable>

---

## Part 4 summary

1. **ASP.NET Core MVC** discovers controllers and actions by reflection at **startup**,
   then serves requests from a cached route table and cached invokers.
2. **DI** reads constructor signatures as dependency lists. Lifetimes matter; the
   greediest *satisfiable* constructor wins; `ActivatorUtilities` bridges DI and
   runtime arguments. **"DI uses reflection" is the safe claim** — the activation
   strategy varies by container, version and platform.
3. **EF Core** builds a **separate, richer model** by reading CLR metadata, then
   translates **expression trees** into provider-specific SQL. Shadow properties prove
   the model is not just a view over reflection.
4. **Serialization** is the canonical case: reflection mode and source-generated mode
   produce **byte-identical** output; the difference is *when* the work happened.
5. **Plugins** (Part 2 §9): `AssemblyLoadContext` isolates **type identity**, not
   **privileges**.
6. **`DispatchProxy`** packages Part 3's generated-type pattern. Interfaces only;
   self-calls are not intercepted; async needs care.
7. **Cross-cutting concerns**: choose the seam by where the concern belongs. A
   hand-written decorator is right more often than a generated proxy. **Metaprogramming
   supplies the seam, never the policy.**
8. **Expression trees** are code as data. Readable, type-checked, translatable — and the
   reason `IQueryable` can target a database. Prefer them to IL whenever you need a
   function.

**And the thread running through all eight: DISCOVER ONCE, CACHE, THEN EXECUTE COMPILED
CODE.**

**Back to:** [Part 1](Part-1-Metaprogramming.md) ·
[Part 2](Part-2-Runtime-Introspection.md) ·
[Part 3](Part-3-Runtime-Code-Generation.md) ·
[Quick Revision](Quick-Revision-Notes.md) ·
[Interview Questions](Interview-Questions.md)
