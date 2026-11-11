using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Metaprogramming.DynamicProxiesDemo;

/// <summary>
/// Part 4 of the session: dynamic proxies and cross-cutting concerns.
/// DispatchProxy generates a type at runtime that implements an interface and routes
/// every call through one method, which is where logging, caching and retries go.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. THE PROBLEM - cross-cutting concerns tangled into business code");
        TheProblem();

        Banner("2. DispatchProxy - ONE method intercepts EVERY call");
        LoggingInterception();

        Banner("3. WHAT DispatchProxy ACTUALLY CREATED");
        InspectTheProxy();

        Banner("4. CACHING, DRIVEN BY AN ATTRIBUTE");
        CachingInterception();

        Banner("5. RETRIES, AND CORRECT EXCEPTION HANDLING");
        RetryInterception();

        Banner("6. STACKING PROXIES");
        StackedProxies();

        Banner("7. REGISTERING A PROXY IN THE DI CONTAINER");
        DependencyInjectionIntegration();

        Banner("8. LIMITATIONS OF DispatchProxy");
        Limitations();

        Banner("9. MIDDLEWARE vs FILTERS vs PROXIES - picking the right seam");
        ChoosingTheSeam();
    }

    // =====================================================================
    // 1. Why anyone bothers.
    // =====================================================================
    private static void TheProblem()
    {
        Console.WriteLine("Here is the service we want: nothing but business logic.");
        Console.WriteLine();
        Console.WriteLine("    public decimal GetBalance(string account)");
        Console.WriteLine("    {");
        Console.WriteLine("        return _balances[account];");
        Console.WriteLine("    }");
        Console.WriteLine();
        Console.WriteLine("Here is what it usually becomes once the operational requirements land:");
        Console.WriteLine();
        Console.WriteLine("    public decimal GetBalance(string account)");
        Console.WriteLine("    {");
        Console.WriteLine("        _logger.LogInformation(\"GetBalance {Account}\", account);   // logging");
        Console.WriteLine("        var sw = Stopwatch.StartNew();                           // metrics");
        Console.WriteLine("        if (_cache.TryGetValue(account, out var hit)) return hit; // caching");
        Console.WriteLine("        try {");
        Console.WriteLine("            var result = _balances[account];                     // the actual work");
        Console.WriteLine("            _cache[account] = result;");
        Console.WriteLine("            return result;");
        Console.WriteLine("        }");
        Console.WriteLine("        catch (Exception ex) { _logger.LogError(ex, \"...\"); throw; }");
        Console.WriteLine("        finally { _metrics.Record(sw.Elapsed); }                 // metrics");
        Console.WriteLine("    }");
        Console.WriteLine();
        Console.WriteLine("One line of business logic, ten lines of plumbing - repeated in every method of");
        Console.WriteLine("every service. These are CROSS-CUTTING CONCERNS: orthogonal to the business");
        Console.WriteLine("logic, yet physically tangled into it.");
        Console.WriteLine();
        Console.WriteLine("A proxy moves that plumbing OUT, into one place, without touching the service.");
    }

    // =====================================================================
    // 2. The core demo.
    // =====================================================================
    private static void LoggingInterception()
    {
        IAccountService real = new AccountService();

        // The proxy IS an IAccountService as far as the type system is concerned.
        LoggingProxy? interceptor = null;
        IAccountService proxied = InterceptingProxy.Wrap<IAccountService, LoggingProxy>(
            real, p => interceptor = p);

        Console.WriteLine("Calls made through the proxy (ordinary C#, no reflection at the call site):");
        Console.WriteLine();
        Console.WriteLine("  GetBalance(\"SB-1001\")  = " + proxied.GetBalance("SB-1001"));
        Console.WriteLine("  Deposit(\"SB-1001\", 500) = " + proxied.Deposit("SB-1001", 500m));
        Console.WriteLine("  GetAccountCount()       = " + proxied.GetAccountCount());

        Console.WriteLine();
        Console.WriteLine("What the interceptor recorded, without AccountService knowing it exists:");
        Console.WriteLine();
        foreach (string line in interceptor!.Log)
        {
            Console.WriteLine("  " + line);
        }

        Console.WriteLine();
        Console.WriteLine("AccountService.cs contains no logging code at all. Look at it and check.");
        Console.WriteLine("The interception happened because every call to the generated type lands in");
        Console.WriteLine("LoggingProxy.Invoke, which decides what to do before and after forwarding.");
    }

    // =====================================================================
    // 3. The generated type is real, and reflection can show it.
    // =====================================================================
    private static void InspectTheProxy()
    {
        IAccountService real = new AccountService();
        IAccountService proxied = InterceptingProxy.Wrap<IAccountService, LoggingProxy>(real);

        Type proxyType = proxied.GetType();

        Console.WriteLine("The object we are holding:");
        Console.WriteLine("  type name      : " + proxyType.Name);
        Console.WriteLine("  full name      : " + proxyType.FullName);
        Console.WriteLine("  base type      : " + proxyType.BaseType?.Name + "  <- our LoggingProxy");
        Console.WriteLine("  implements     : " + string.Join(", ", proxyType.GetInterfaces().Select(i => i.Name)));
        Console.WriteLine("  assembly       : " + proxyType.Assembly.GetName().Name);
        Console.WriteLine("  IsDynamic      : " + proxyType.Assembly.IsDynamic + "  <- generated at run time");

        Console.WriteLine();
        Console.WriteLine("It is NOT an AccountService:");
        Console.WriteLine("  proxied is AccountService  = " + (proxied is AccountService));
        Console.WriteLine("  proxied is IAccountService = " + (proxied is IAccountService));
        Console.WriteLine("  proxied is DispatchProxy   = " + (proxied is DispatchProxy));

        Console.WriteLine();
        Console.WriteLine("So DispatchProxy did exactly what example 06 did by hand:");
        Console.WriteLine("  defined a type at run time, made it implement a known interface, and created");
        Console.WriteLine("  an instance. The difference is that it wrote the TypeBuilder code for us,");
        Console.WriteLine("  and every interface method it generated simply calls our Invoke override.");
    }

    // =====================================================================
    // 4. Attribute-driven caching: metadata deciding behaviour.
    // =====================================================================
    private static void CachingInterception()
    {
        var real = new AccountService();
        CachingProxy? cache = null;
        IAccountService proxied = InterceptingProxy.Wrap<IAccountService, CachingProxy>(real, p => cache = p);

        Console.WriteLine("IAccountService marks only GetBalance with [Cacheable]:");
        foreach (MethodInfo m in typeof(IAccountService).GetMethods())
        {
            bool cacheable = m.GetCustomAttribute<CacheableAttribute>() is not null;
            Console.WriteLine($"  {m.Name,-18} {(cacheable ? "[Cacheable]" : "-")}");
        }

        Console.WriteLine();
        Console.WriteLine("Calling GetBalance three times with the same argument:");
        for (int i = 1; i <= 3; i++)
        {
            Console.WriteLine("  call " + i + " -> " + proxied.GetBalance("SB-1001")
                + "   (service hit count: " + real.ReadCount + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Calling GetAccountCount three times - NOT marked cacheable:");
        for (int i = 1; i <= 3; i++)
        {
            Console.WriteLine("  call " + i + " -> " + proxied.GetAccountCount());
        }

        Console.WriteLine();
        Console.WriteLine("  cache hits   : " + cache!.Hits);
        Console.WriteLine("  cache misses : " + cache.Misses);
        Console.WriteLine("  the real service was read " + real.ReadCount + " time(s) for GetBalance");

        Console.WriteLine();
        Console.WriteLine("The attribute is the whole policy. Marking a new method [Cacheable] changes");
        Console.WriteLine("behaviour with no change to the proxy and no change to the service.");
        Console.WriteLine();
        Console.WriteLine("Worth stating clearly: metaprogramming provided the SEAM, not the policy.");
        Console.WriteLine("This cache has no expiry, no size limit, no invalidation and no thread safety.");
        Console.WriteLine("A real one needs all four - use HybridCache or IMemoryCache, and think about");
        Console.WriteLine("correctness. The same applies to authorization: a proxy can be the place you");
        Console.WriteLine("enforce a policy, but it does not supply the policy or make it correct.");
    }

    // =====================================================================
    // 5. Retries, plus the exception-unwrapping detail.
    // =====================================================================
    private static void RetryInterception()
    {
        var flaky = new FlakyGateway(failuresBeforeSuccess: 2);
        RetryProxy? retry = null;
        IPaymentGateway proxied = InterceptingProxy.Wrap<IPaymentGateway, RetryProxy>(flaky, p =>
        {
            p.MaxAttempts = 4;
            retry = p;
        });

        Console.WriteLine("FlakyGateway fails twice, then succeeds. MaxAttempts = 4.");
        Console.WriteLine();
        Console.WriteLine("  result: " + proxied.Charge("CARD-9", 1_250m));
        Console.WriteLine();
        foreach (string attempt in retry!.Attempts)
        {
            Console.WriteLine("  " + attempt);
        }

        Console.WriteLine();
        Console.WriteLine("Now one that fails more times than we allow retries:");

        var hopeless = new FlakyGateway(failuresBeforeSuccess: 99);
        IPaymentGateway doomed = InterceptingProxy.Wrap<IPaymentGateway, RetryProxy>(hopeless,
            p => p.MaxAttempts = 2);

        try
        {
            doomed.Charge("CARD-1", 10m);
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine("  caught " + ex.GetType().Name + ": " + ex.Message);
            Console.WriteLine();
            Console.WriteLine("  Note that we caught TimeoutException, NOT TargetInvocationException.");
            Console.WriteLine("  Inside Invoke we called targetMethod.Invoke(...), which wraps any");
            Console.WriteLine("  exception from the target in TargetInvocationException. If a proxy lets");
            Console.WriteLine("  that escape, every 'catch (TimeoutException)' upstream silently stops");
            Console.WriteLine("  matching - a nasty, hard-to-find regression.");
            Console.WriteLine("  InterceptingProxy.InvokeTarget unwraps it with ExceptionDispatchInfo,");
            Console.WriteLine("  which also preserves the original stack trace.");
        }
    }

    // =====================================================================
    // 6. Composition: a proxy can wrap a proxy.
    // =====================================================================
    private static void StackedProxies()
    {
        var real = new AccountService();

        // Inner: caching. Outer: logging. The logger therefore sees the calls that
        // the cache let through, which is usually what you want for diagnostics.
        CachingProxy? cache = null;
        IAccountService cached = InterceptingProxy.Wrap<IAccountService, CachingProxy>(real, p => cache = p);

        LoggingProxy? logger = null;
        IAccountService logged = InterceptingProxy.Wrap<IAccountService, LoggingProxy>(cached, p => logger = p);

        Console.WriteLine("Chain: caller -> LoggingProxy -> CachingProxy -> AccountService");
        Console.WriteLine();

        for (int i = 1; i <= 3; i++)
        {
            Console.WriteLine("  GetBalance(\"SB-1001\") = " + logged.GetBalance("SB-1001"));
        }

        Console.WriteLine();
        Console.WriteLine("Log from the outer proxy:");
        foreach (string line in logger!.Log)
        {
            Console.WriteLine("  " + line);
        }

        Console.WriteLine();
        Console.WriteLine("  cache hits = " + cache!.Hits + ", misses = " + cache.Misses
            + ", real service reads = " + real.ReadCount);
        Console.WriteLine();
        Console.WriteLine("Because each proxy is just another IAccountService, they compose freely.");
        Console.WriteLine("Order matters: logging outside caching logs every call; logging inside caching");
        Console.WriteLine("would log only the cache misses.");
    }

    // =====================================================================
    // 7. The way you would actually ship this.
    // =====================================================================
    private static void DependencyInjectionIntegration()
    {
        var services = new ServiceCollection();

        // Register the real implementation under its own concrete type...
        services.AddSingleton<AccountService>();

        // ...and register the INTERFACE as a factory that wraps it in a proxy.
        // Consumers ask for IAccountService and never learn that it is proxied.
        services.AddSingleton<IAccountService>(provider =>
        {
            var inner = provider.GetRequiredService<AccountService>();
            return InterceptingProxy.Wrap<IAccountService, LoggingProxy>(inner);
        });

        // A consumer with a perfectly ordinary constructor dependency.
        services.AddTransient<MonthEndReport>();

        using ServiceProvider sp = services.BuildServiceProvider();

        var report = sp.GetRequiredService<MonthEndReport>();

        Console.WriteLine("MonthEndReport asked for IAccountService and got a proxy:");
        Console.WriteLine("  injected type: " + report.ServiceTypeName);
        Console.WriteLine();
        Console.WriteLine(report.Run());

        Console.WriteLine();
        Console.WriteLine("This is the decorator pattern, with the decorator generated rather than written.");
        Console.WriteLine("Scrutor's Decorate<TService, TDecorator>() packages exactly this registration.");
        Console.WriteLine();
        Console.WriteLine("In production most teams use Castle DynamicProxy (via Autofac interceptors) or");
        Console.WriteLine("a source-generated interceptor instead of DispatchProxy, because those handle");
        Console.WriteLine("virtual class methods and async more comfortably. DispatchProxy's advantage is");
        Console.WriteLine("that it is built in - no package, no licence question.");
    }

    // =====================================================================
    // 8. Honest limits.
    // =====================================================================
    private static void Limitations()
    {
        Console.WriteLine("A. INTERFACES ONLY. DispatchProxy cannot proxy a class, sealed or not.");
        Console.WriteLine("   Shown with the non-generic overload available on .NET 10,");
        Console.WriteLine("   DispatchProxy.Create(Type interfaceType, Type proxyType):");
        try
        {
            // AccountService is a class, not an interface - so this must fail.
            _ = DispatchProxy.Create(typeof(AccountService), typeof(LoggingProxy));
            Console.WriteLine("   (unexpectedly succeeded)");
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("   " + ex.GetType().Name + ": " + ex.Message);
        }

        // The same overload succeeds for an interface, and is handy when the interface
        // type is only known at run time (a plugin's contract, say).
        object dynamicallyTyped = DispatchProxy.Create(typeof(IAccountService), typeof(LoggingProxy));
        Console.WriteLine("   with an interface instead: created a "
            + dynamicallyTyped.GetType().Name + " implementing "
            + string.Join(", ", dynamicallyTyped.GetType().GetInterfaces().Select(i => i.Name)));

        Console.WriteLine();
        Console.WriteLine("   Consequence: your services must be behind interfaces for this to be an option.");
        Console.WriteLine("   Castle DynamicProxy can also subclass a class, but only intercepts VIRTUAL");
        Console.WriteLine("   members - a non-virtual method can never be intercepted by subclassing.");
        Console.WriteLine();

        Console.WriteLine("B. THE PROXY BASE CLASS MUST NOT BE SEALED.");
        Console.WriteLine("   DispatchProxy.Create generates a type that derives from your proxy class,");
        Console.WriteLine("   so 'public sealed class LoggingProxy : DispatchProxy' throws at run time:");
        Console.WriteLine("     ArgumentException: The base type '...' cannot be sealed. (Parameter 'TProxy')");
        Console.WriteLine("   It must also have an accessible parameterless constructor, which is why");
        Console.WriteLine("   configuration happens after creation (SetTarget) rather than in a constructor.");
        Console.WriteLine();

        Console.WriteLine("C. ONLY CALLS THROUGH THE INTERFACE ARE INTERCEPTED.");
        var real = new AccountService();
        IAccountService proxied = InterceptingProxy.Wrap<IAccountService, LoggingProxy>(real);
        Console.WriteLine("   proxied.GetBalance(...)  -> intercepted");
        Console.WriteLine("   real.GetBalance(...)     -> NOT intercepted (you are holding the real object)");
        Console.WriteLine("   A call from one method of AccountService to another is also NOT intercepted,");
        Console.WriteLine("   because 'this' inside the service is the real object, not the proxy.");
        Console.WriteLine("   That self-call blind spot surprises people and breaks naive caching.");
        Console.WriteLine();

        Console.WriteLine("D. ASYNC NEEDS DELIBERATE HANDLING.");
        Console.WriteLine("   Invoke returns object?. For an async method that object is a Task, and it is");
        Console.WriteLine("   returned the instant the method starts - so a naive stopwatch around");
        Console.WriteLine("   InvokeTarget measures only how long it took to CREATE the Task.");
        Console.WriteLine("   To time or catch properly you must await the returned Task, which means");
        Console.WriteLine("   inspecting the return type and handling Task, Task<T> and ValueTask<T>");
        Console.WriteLine("   separately. Section 2's timings are honest here only because IAccountService");
        Console.WriteLine("   is entirely synchronous.");
        Console.WriteLine();

        Console.WriteLine("E. PER-CALL COST. Every call goes through targetMethod.Invoke, which is the");
        Console.WriteLine("   slow reflective path measured in example 05. For a hot inner loop, cache a");
        Console.WriteLine("   delegate per MethodInfo, or do not use a proxy.");
        Console.WriteLine();

        Console.WriteLine("F. NATIVE AOT. DispatchProxy generates a type at run time, so it needs dynamic");
        Console.WriteLine("   code support and does not work under Native AOT.");
        Console.WriteLine("   IsDynamicCodeSupported here = "
            + System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported);
        Console.WriteLine();

        Console.WriteLine("G. DEBUGGING AND CLARITY. A stack trace now runs through a generated type, and");
        Console.WriteLine("   'go to definition' on an interface no longer reaches the code that runs.");
        Console.WriteLine("   Interception is powerful precisely because it is invisible, and that is also");
        Console.WriteLine("   its main cost to the next person reading the code.");
    }

    // =====================================================================
    // 9. The practical decision.
    // =====================================================================
    private static void ChoosingTheSeam()
    {
        Console.WriteLine("  seam              scope                        reaches");
        Console.WriteLine("  ------------------------------------------------------------------------");
        Console.WriteLine("  Middleware        every HTTP request           the whole pipeline");
        Console.WriteLine("  MVC filters       controller actions           per action/controller");
        Console.WriteLine("  Dynamic proxies   any interface call           any layer, incl. services");
        Console.WriteLine("  Source generators compile time                 anything, with zero runtime cost");
        Console.WriteLine();
        Console.WriteLine("Choose by where the concern belongs:");
        Console.WriteLine();
        Console.WriteLine("  Authentication, CORS, HTTPS redirect, request logging");
        Console.WriteLine("    -> MIDDLEWARE. It is about the HTTP request, not about any one method.");
        Console.WriteLine();
        Console.WriteLine("  Model validation, authorization on an action, action result shaping");
        Console.WriteLine("    -> FILTERS. They know about MVC concepts such as model state and results.");
        Console.WriteLine();
        Console.WriteLine("  Retry on a flaky dependency, caching a repository, auditing domain operations");
        Console.WriteLine("    -> PROXIES or hand-written DECORATORS. The concern is about a service");
        Console.WriteLine("       interface and applies however that service is called, HTTP or not.");
        Console.WriteLine();
        Console.WriteLine("  High-volume logging, serialization, mapping");
        Console.WriteLine("    -> SOURCE GENERATORS, when the shape is known at build time.");
        Console.WriteLine();
        Console.WriteLine("And the answer that is right more often than any of them: a hand-written");
        Console.WriteLine("decorator class. Twenty lines of ordinary C#, fully debuggable, trimmable,");
        Console.WriteLine("AOT-safe, and obvious to the next reader. Reach for a generated proxy when the");
        Console.WriteLine("number of interfaces makes hand-writing them genuinely impractical.");
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
// The business interfaces and implementations. Note: NO cross-cutting code.
// =========================================================================

public interface IAccountService
{
    [Cacheable]
    decimal GetBalance(string accountNumber);

    decimal Deposit(string accountNumber, decimal amount);

    int GetAccountCount();
}

/// <summary>
/// Pure business logic. No logging, no caching, no retries, no metrics.
/// It does not know, and cannot know, that it is being proxied.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly Dictionary<string, decimal> _balances = new()
    {
        ["SB-1001"] = 25_400.00m,
        ["SB-1002"] = 118_250.75m
    };

    /// <summary>Counts real reads, so the caching demo can prove the cache worked.</summary>
    public int ReadCount { get; private set; }

    public decimal GetBalance(string accountNumber)
    {
        ReadCount++;

        if (!_balances.TryGetValue(accountNumber, out decimal balance))
        {
            throw new KeyNotFoundException("No such account: " + accountNumber);
        }

        return balance;
    }

    public decimal Deposit(string accountNumber, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Deposit must be positive.");
        }

        decimal current = _balances.TryGetValue(accountNumber, out decimal existing) ? existing : 0m;
        decimal updated = current + amount;
        _balances[accountNumber] = updated;
        return updated;
    }

    public int GetAccountCount() => _balances.Count;
}

public interface IPaymentGateway
{
    string Charge(string cardToken, decimal amount);
}

/// <summary>Fails a set number of times before succeeding, to exercise the retry proxy.</summary>
public sealed class FlakyGateway(int failuresBeforeSuccess) : IPaymentGateway
{
    private int _calls;

    public string Charge(string cardToken, decimal amount)
    {
        _calls++;

        if (_calls <= failuresBeforeSuccess)
        {
            throw new TimeoutException("Gateway timed out (call " + _calls + ").");
        }

        return "charged " + amount.ToString("N2") + " to " + cardToken + " on call " + _calls;
    }
}

/// <summary>An ordinary consumer. It has no idea its dependency is a generated proxy.</summary>
internal sealed class MonthEndReport(IAccountService accounts)
{
    public string ServiceTypeName => accounts.GetType().Name;

    public string Run() =>
        "  accounts tracked : " + accounts.GetAccountCount() + Environment.NewLine
        + "  SB-1001 balance  : " + accounts.GetBalance("SB-1001").ToString("N2") + Environment.NewLine
        + "  SB-1002 balance  : " + accounts.GetBalance("SB-1002").ToString("N2");
}
