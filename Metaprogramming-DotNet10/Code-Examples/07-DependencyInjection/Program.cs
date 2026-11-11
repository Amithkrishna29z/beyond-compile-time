using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Metaprogramming.DependencyInjectionDemo;

/// <summary>
/// Part 4 of the session: dependency injection as everyday metaprogramming.
/// The container inspects constructors it has never seen and builds object graphs from
/// metadata. We also build a 30-line container to show there is no magic.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. THE CONSTRUCTOR IS THE METADATA");
        ConstructorsAreMetadata();

        Banner("2. REGISTRATION AND RESOLUTION");
        RegistrationAndResolution();

        Banner("3. SERVICE LIFETIMES");
        Lifetimes();

        Banner("4. HOW THE CONTAINER PICKS A CONSTRUCTOR");
        ConstructorSelection();

        Banner("5. ActivatorUtilities - DI for types that are NOT registered");
        ActivatorUtilitiesDemo();

        Banner("6. CONVENTION-BASED REGISTRATION BY REFLECTION");
        ConventionRegistration();

        Banner("7. A 30-LINE CONTAINER, SO THERE IS NO MAGIC LEFT");
        MiniContainerDemo();

        Banner("8. WHAT THE REAL CONTAINER ACTUALLY DOES (inspected, not assumed)");
        ActivationStrategy();
    }

    // =====================================================================
    // 1. A constructor signature is a machine-readable dependency list.
    // =====================================================================
    private static void ConstructorsAreMetadata()
    {
        ConstructorInfo ctor = typeof(OrderService).GetConstructors().Single();

        Console.WriteLine("OrderService declares one constructor. Reflection reads it as a shopping list:");
        Console.WriteLine();
        foreach (ParameterInfo p in ctor.GetParameters())
        {
            Console.WriteLine($"  needs {p.ParameterType.Name,-22} (parameter '{p.Name}')");
        }

        Console.WriteLine();
        Console.WriteLine("Nobody wrote a configuration file listing these. The dependency graph IS the");
        Console.WriteLine("constructor signature, and the constructor signature is already in metadata.");
        Console.WriteLine("That is the whole insight DI is built on.");
    }

    // =====================================================================
    // 2. Register, build, resolve.
    // =====================================================================
    private static void RegistrationAndResolution()
    {
        var services = new ServiceCollection();

        // Registration maps a SERVICE type (usually an interface) to an IMPLEMENTATION type.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        services.AddScoped<IPricingPolicy, StandardPricingPolicy>();
        services.AddTransient<IOrderRepository, InMemoryOrderRepository>();
        services.AddTransient<OrderService>();

        Console.WriteLine("Registered " + services.Count + " services:");
        foreach (ServiceDescriptor descriptor in services)
        {
            Console.WriteLine($"  {descriptor.Lifetime,-9} {descriptor.ServiceType.Name,-20} -> {descriptor.ImplementationType?.Name}");
        }

        using ServiceProvider provider = services.BuildServiceProvider();

        // One request. The container resolves the entire graph transitively.
        var orderService = provider.GetRequiredService<OrderService>();

        Console.WriteLine();
        Console.WriteLine("provider.GetRequiredService<OrderService>() produced a full object graph:");
        Console.WriteLine(orderService.DescribeGraph());

        Console.WriteLine();
        Console.WriteLine("Result of using it:");
        Console.WriteLine("  " + orderService.PlaceOrder("Mechanical Keyboard", 2, 4999.00m));

        Console.WriteLine();
        Console.WriteLine("GetService vs GetRequiredService:");
        Console.WriteLine("  GetService<IMissingService>()         = "
            + (provider.GetService<IMissingService>()?.ToString() ?? "null"));
        try
        {
            provider.GetRequiredService<IMissingService>();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine("  GetRequiredService<IMissingService>() threw InvalidOperationException:");
            Console.WriteLine("    " + ex.Message);
        }
    }

    // =====================================================================
    // 3. Lifetimes, demonstrated by instance identity.
    // =====================================================================
    private static void Lifetimes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SingletonCounter>();
        services.AddScoped<ScopedCounter>();
        services.AddTransient<TransientCounter>();

        using ServiceProvider provider = services.BuildServiceProvider();

        Console.WriteLine("Resolving each service twice, in two different scopes.");
        Console.WriteLine("The number is a per-instance id, so repeats mean 'same object'.");
        Console.WriteLine();

        for (int scopeNumber = 1; scopeNumber <= 2; scopeNumber++)
        {
            using IServiceScope scope = provider.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            Console.WriteLine("  scope " + scopeNumber + ":");
            Console.WriteLine("    singleton: " + sp.GetRequiredService<SingletonCounter>().Id
                + ", " + sp.GetRequiredService<SingletonCounter>().Id);
            Console.WriteLine("    scoped   : " + sp.GetRequiredService<ScopedCounter>().Id
                + ", " + sp.GetRequiredService<ScopedCounter>().Id);
            Console.WriteLine("    transient: " + sp.GetRequiredService<TransientCounter>().Id
                + ", " + sp.GetRequiredService<TransientCounter>().Id);
        }

        Console.WriteLine();
        Console.WriteLine("  Singleton - one instance for the whole provider.");
        Console.WriteLine("  Scoped    - one instance per scope. In ASP.NET Core, one scope per HTTP request.");
        Console.WriteLine("  Transient - a new instance every single time it is asked for.");
        Console.WriteLine();
        Console.WriteLine("The classic bug: a SINGLETON that depends on a SCOPED service. The scoped");
        Console.WriteLine("instance gets captured for the life of the process. BuildServiceProvider with");
        Console.WriteLine("ValidateScopes = true turns that into an exception instead of a slow leak:");

        var bad = new ServiceCollection();
        bad.AddScoped<ScopedCounter>();
        bad.AddSingleton<CaptiveDependencyService>();

        using ServiceProvider validating = bad.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        try
        {
            using IServiceScope scope = validating.CreateScope();
            scope.ServiceProvider.GetRequiredService<CaptiveDependencyService>();
            Console.WriteLine("  (no error reported)");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine("  " + ex.Message);
        }
    }

    // =====================================================================
    // 4. Constructor selection rules.
    // =====================================================================
    private static void ConstructorSelection()
    {
        Console.WriteLine("ReportBuilder has three constructors:");
        foreach (ConstructorInfo c in typeof(ReportBuilder).GetConstructors())
        {
            Console.WriteLine("  .ctor(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)) + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Case A: only IClock is registered.");
        var a = new ServiceCollection();
        a.AddSingleton<IClock, SystemClock>();
        a.AddTransient<ReportBuilder>();
        using (ServiceProvider provider = a.BuildServiceProvider())
        {
            Console.WriteLine("  chosen -> " + provider.GetRequiredService<ReportBuilder>().ChosenConstructor);
        }

        Console.WriteLine();
        Console.WriteLine("Case B: IClock AND IAuditLog are registered.");
        var b = new ServiceCollection();
        b.AddSingleton<IClock, SystemClock>();
        b.AddSingleton<IAuditLog, InMemoryAuditLog>();
        b.AddTransient<ReportBuilder>();
        using (ServiceProvider provider = b.BuildServiceProvider())
        {
            Console.WriteLine("  chosen -> " + provider.GetRequiredService<ReportBuilder>().ChosenConstructor);
        }

        Console.WriteLine();
        Console.WriteLine("The rule in Microsoft.Extensions.DependencyInjection: pick the constructor with");
        Console.WriteLine("the MOST parameters that the container can satisfy completely. Adding a");
        Console.WriteLine("registration can therefore change which constructor runs - as it just did.");
        Console.WriteLine();
        Console.WriteLine("Ambiguity is an error, not a guess. Two satisfiable constructors where neither");
        Console.WriteLine("parameter set contains the other:");

        var caseC = new ServiceCollection();
        caseC.AddSingleton<IClock, SystemClock>();
        caseC.AddSingleton<IAuditLog, InMemoryAuditLog>();
        caseC.AddTransient<AmbiguousService>();
        using (ServiceProvider provider = caseC.BuildServiceProvider())
        {
            try
            {
                provider.GetRequiredService<AmbiguousService>();
                Console.WriteLine("  (no error reported)");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("  InvalidOperationException: " + ex.Message);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Fix it by removing a constructor, or by marking one with");
        Console.WriteLine("[ActivatorUtilitiesConstructor] so the choice is explicit.");
    }

    // =====================================================================
    // 5. ActivatorUtilities: mix container services with your own arguments.
    // =====================================================================
    private static void ActivatorUtilitiesDemo()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
        using ServiceProvider provider = services.BuildServiceProvider();

        // InvoiceGenerator is NOT registered, and it needs a runtime value (the invoice
        // number) that the container cannot possibly know.
        Console.WriteLine("InvoiceGenerator needs IClock (from DI) and a string (from us).");
        Console.WriteLine("Activator.CreateInstance cannot help - it knows nothing about IClock.");
        Console.WriteLine();

        var generator = ActivatorUtilities.CreateInstance<InvoiceGenerator>(provider, "INV-2026-0042");
        Console.WriteLine("ActivatorUtilities.CreateInstance<InvoiceGenerator>(provider, \"INV-2026-0042\"):");
        Console.WriteLine("  " + generator.Generate(2_500m));

        // CreateFactory does the reflection ONCE and returns a reusable factory.
        // This is the pattern ASP.NET Core uses for controllers.
        ObjectFactory factory = ActivatorUtilities.CreateFactory(
            typeof(InvoiceGenerator), [typeof(string)]);

        Console.WriteLine();
        Console.WriteLine("ActivatorUtilities.CreateFactory - resolve the plan once, reuse it:");
        foreach (string number in new[] { "INV-0001", "INV-0002", "INV-0003" })
        {
            var invoice = (InvoiceGenerator)factory(provider, [number]);
            Console.WriteLine("  " + invoice.Generate(1_000m));
        }

        Console.WriteLine();
        Console.WriteLine("Rule of thumb:");
        Console.WriteLine("  GetRequiredService            - the type is registered.");
        Console.WriteLine("  ActivatorUtilities.CreateInstance - it is not registered, or needs extra arguments.");
        Console.WriteLine("  ActivatorUtilities.CreateFactory  - same, but on a hot path. Cache the factory.");
        Console.WriteLine();
        Console.WriteLine("This is exactly how a plugin host should activate plugins that have dependencies,");
        Console.WriteLine("instead of the bare Activator.CreateInstance used in example 04.");
    }

    // =====================================================================
    // 6. Registering by convention - reflection at startup.
    // =====================================================================
    private static void ConventionRegistration()
    {
        var services = new ServiceCollection();

        // The handlers themselves have dependencies, so the usual services go in first.
        services.AddSingleton<IClock, SystemClock>();

        Assembly assembly = Assembly.GetExecutingAssembly();

        Console.WriteLine("Convention: every concrete class named *Handler that implements IEventHandler");
        Console.WriteLine("is registered as a transient IEventHandler. No per-class registration lines.");
        Console.WriteLine();

        foreach (Type implementation in assembly.GetTypes())
        {
            if (implementation.IsAbstract || implementation.IsInterface)
            {
                continue;
            }

            if (!typeof(IEventHandler).IsAssignableFrom(implementation))
            {
                continue;
            }

            if (!implementation.Name.EndsWith("Handler", StringComparison.Ordinal))
            {
                Console.WriteLine("  skipped " + implementation.Name + " - does not match the naming convention");
                continue;
            }

            services.AddTransient(typeof(IEventHandler), implementation);
            Console.WriteLine("  registered " + implementation.Name);
        }

        using ServiceProvider provider = services.BuildServiceProvider();

        // Resolving IEnumerable<T> gives every registration for that service type.
        IEnumerable<IEventHandler> handlers = provider.GetServices<IEventHandler>();

        Console.WriteLine();
        Console.WriteLine("Dispatching one event to all discovered handlers:");
        foreach (IEventHandler handler in handlers)
        {
            Console.WriteLine("  " + handler.Handle("OrderPlaced"));
        }

        Console.WriteLine();
        Console.WriteLine("This reflection happens ONCE at startup. Nothing scans types per request.");
        Console.WriteLine("Scanning libraries such as Scrutor package exactly this pattern.");
    }

    // =====================================================================
    // 7. The container, demystified.
    // =====================================================================
    private static void MiniContainerDemo()
    {
        var container = new MiniContainer();
        container.Register<IClock, SystemClock>();
        container.Register<IAuditLog, InMemoryAuditLog>();
        container.Register<IPricingPolicy, StandardPricingPolicy>();
        container.Register<IOrderRepository, InMemoryOrderRepository>();
        container.Register<OrderService, OrderService>();

        var service = (OrderService)container.Resolve(typeof(OrderService));

        Console.WriteLine("Resolved by MiniContainer (60 lines, at the bottom of this file):");
        Console.WriteLine(service.DescribeGraph());
        Console.WriteLine();
        Console.WriteLine("  " + service.PlaceOrder("Desk Lamp", 1, 1299.00m));

        Console.WriteLine();
        Console.WriteLine("MiniContainer is three ideas:");
        Console.WriteLine("  1. a dictionary from service type to implementation type;");
        Console.WriteLine("  2. GetConstructors() to find out what an implementation needs;");
        Console.WriteLine("  3. recursion, then Activator.CreateInstance with the resolved arguments.");
        Console.WriteLine();
        Console.WriteLine("A production container adds lifetimes, scopes, disposal, thread safety,");
        Console.WriteLine("open generics, cycle detection and a compiled fast path. The CORE IDEA is this.");
    }

    // =====================================================================
    // 8. Do not over-claim: look at what the real container does.
    // =====================================================================
    private static void ActivationStrategy()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, SystemClock>();
        services.AddTransient<ReportBuilder>();
        using ServiceProvider provider = services.BuildServiceProvider();

        // Force at least one resolution so the engine is created and warmed.
        _ = provider.GetRequiredService<ReportBuilder>();

        // Read the provider's private engine field to see which strategy is in use.
        FieldInfo? engineField = typeof(ServiceProvider)
            .GetField("_engine", BindingFlags.NonPublic | BindingFlags.Instance);

        object? engine = engineField?.GetValue(provider);

        Console.WriteLine("Package: Microsoft.Extensions.DependencyInjection "
            + typeof(ServiceProvider).Assembly.GetName().Version);
        Console.WriteLine("RuntimeFeature.IsDynamicCodeCompiled = "
            + System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled);
        Console.WriteLine();
        Console.WriteLine("ServiceProvider's internal engine on THIS runtime: "
            + (engine?.GetType().Name ?? "(could not read - internals have changed)"));

        Console.WriteLine();
        Console.WriteLine("Why that matters for how we describe DI:");
        Console.WriteLine("  It is accurate to say the container uses REFLECTION to read constructors.");
        Console.WriteLine("  It is NOT accurate to say every container uses Reflection.Emit to call them.");
        Console.WriteLine();
        Console.WriteLine("Microsoft.Extensions.DependencyInjection chooses an activation strategy at run time:");
        Console.WriteLine("  - a reflection-based engine, used immediately and always available;");
        Console.WriteLine("  - a faster engine built from compiled expression trees or emitted IL, which it");
        Console.WriteLine("    switches to in the background once a service has been resolved enough times;");
        Console.WriteLine("  - the reflection-only engine when RuntimeFeature.IsDynamicCodeCompiled is false,");
        Console.WriteLine("    which is the Native AOT case.");
        Console.WriteLine();
        Console.WriteLine("Other containers differ: some compile expression trees, some emit IL, some do");
        Console.WriteLine("neither. 'DI uses reflection' is the safe claim. Anything more specific depends");
        Console.WriteLine("on the container, its version and the platform - so check, as we just did.");
        Console.WriteLine();
        Console.WriteLine("Also worth saying out loud: constructor injection does not REQUIRE reflection.");
        Console.WriteLine("You can wire an entire application by hand with 'new'. DI containers use");
        Console.WriteLine("reflection to spare you that wiring, not because the pattern demands it.");
    }

    private static void Banner(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(title);
        Console.WriteLine(new string('=', 78));
    }
}

// =========================================================================
// Services and contracts.
// =========================================================================

internal interface IClock
{
    DateTimeOffset UtcNow { get; }
}

internal sealed class SystemClock : IClock
{
    // Fixed so the demo output is deterministic.
    public DateTimeOffset UtcNow { get; } = new(2026, 11, 10, 9, 30, 0, TimeSpan.Zero);
}

internal interface IAuditLog
{
    void Record(string message);

    int Count { get; }
}

internal sealed class InMemoryAuditLog : IAuditLog
{
    private readonly List<string> _entries = [];

    public int Count => _entries.Count;

    public void Record(string message) => _entries.Add(message);
}

internal interface IPricingPolicy
{
    decimal Total(int quantity, decimal unitPrice);
}

internal sealed class StandardPricingPolicy : IPricingPolicy
{
    private const decimal TaxRate = 0.18m;

    public decimal Total(int quantity, decimal unitPrice) =>
        Math.Round(quantity * unitPrice * (1 + TaxRate), 2);
}

internal interface IOrderRepository
{
    int Save(string description, decimal total);
}

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    private static int _nextId = 1000;

    public int Save(string description, decimal total) => Interlocked.Increment(ref _nextId);
}

internal interface IMissingService;

/// <summary>The service whose constructor is the running example.</summary>
internal sealed class OrderService(
    IOrderRepository repository,
    IPricingPolicy pricingPolicy,
    IAuditLog auditLog,
    IClock clock)
{
    public string PlaceOrder(string description, int quantity, decimal unitPrice)
    {
        decimal total = pricingPolicy.Total(quantity, unitPrice);
        int id = repository.Save(description, total);
        auditLog.Record("order " + id + " placed at " + clock.UtcNow);

        return "Order " + id + ": " + quantity + " x " + description
            + " = " + total.ToString("N2") + " (audit entries: " + auditLog.Count + ")";
    }

    public string DescribeGraph() =>
        "  OrderService" + Environment.NewLine
        + "    +-- " + repository.GetType().Name + Environment.NewLine
        + "    +-- " + pricingPolicy.GetType().Name + Environment.NewLine
        + "    +-- " + auditLog.GetType().Name + Environment.NewLine
        + "    +-- " + clock.GetType().Name;
}

/// <summary>Three constructors, to demonstrate greediest-satisfiable selection.</summary>
internal sealed class ReportBuilder
{
    public ReportBuilder()
        => ChosenConstructor = ".ctor()";

    public ReportBuilder(IClock clock)
        => ChosenConstructor = ".ctor(IClock)";

    public ReportBuilder(IClock clock, IAuditLog auditLog)
        => ChosenConstructor = ".ctor(IClock, IAuditLog)";

    public string ChosenConstructor { get; }
}

/// <summary>Two satisfiable constructors, neither a superset of the other.</summary>
internal sealed class AmbiguousService
{
    public AmbiguousService(IClock clock)
    {
    }

    public AmbiguousService(IAuditLog auditLog)
    {
    }
}

/// <summary>A singleton that wrongly depends on a scoped service.</summary>
internal sealed class CaptiveDependencyService(ScopedCounter scoped)
{
    public int CapturedId => scoped.Id;
}

internal sealed class SingletonCounter
{
    private static int _next;

    public int Id { get; } = Interlocked.Increment(ref _next);
}

internal sealed class ScopedCounter
{
    private static int _next;

    public int Id { get; } = Interlocked.Increment(ref _next);
}

internal sealed class TransientCounter
{
    private static int _next;

    public int Id { get; } = Interlocked.Increment(ref _next);
}

/// <summary>Needs one service from DI and one value the caller supplies.</summary>
internal sealed class InvoiceGenerator(IClock clock, string invoiceNumber)
{
    public string Generate(decimal amount) =>
        "Invoice " + invoiceNumber + " dated " + clock.UtcNow.ToString("yyyy-MM-dd")
        + " for " + amount.ToString("N2");
}

internal interface IEventHandler
{
    string Handle(string eventName);
}

internal sealed class EmailNotificationHandler(IClock clock) : IEventHandler
{
    public string Handle(string eventName) =>
        "EmailNotificationHandler queued mail for " + eventName + " at " + clock.UtcNow.ToString("HH:mm");
}

internal sealed class InventoryReservationHandler : IEventHandler
{
    public string Handle(string eventName) => "InventoryReservationHandler reserved stock for " + eventName;
}

/// <summary>Implements the interface but breaks the naming convention, so it is skipped.</summary>
internal sealed class LegacyEventListener : IEventHandler
{
    public string Handle(string eventName) => "should not be registered";
}

// =========================================================================
// A minimal container: the same idea as the real one, without the engineering.
// =========================================================================

internal sealed class MiniContainer
{
    private readonly Dictionary<Type, Type> _registrations = [];
    private readonly Dictionary<Type, object> _instances = [];

    public void Register<TService, TImplementation>()
        where TImplementation : TService
        => _registrations[typeof(TService)] = typeof(TImplementation);

    public object Resolve(Type serviceType) => Resolve(serviceType, []);

    private object Resolve(Type serviceType, HashSet<Type> resolving)
    {
        // Singleton-only, to keep the example honest about its own scope.
        if (_instances.TryGetValue(serviceType, out object? cached))
        {
            return cached;
        }

        if (!_registrations.TryGetValue(serviceType, out Type? implementationType))
        {
            throw new InvalidOperationException("No registration for " + serviceType.Name + ".");
        }

        // Cycle detection: A -> B -> A would otherwise recurse until the stack overflows.
        if (!resolving.Add(serviceType))
        {
            throw new InvalidOperationException("Dependency cycle involving " + serviceType.Name + ".");
        }

        // Step 1: read the constructor. This is the reflection.
        ConstructorInfo constructor = implementationType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        // Step 2: resolve each parameter recursively.
        object[] arguments = constructor
            .GetParameters()
            .Select(p => Resolve(p.ParameterType, resolving))
            .ToArray();

        // Step 3: construct.
        object instance = constructor.Invoke(arguments);

        resolving.Remove(serviceType);
        _instances[serviceType] = instance;
        return instance;
    }
}
