using System.Diagnostics;
using System.Reflection;

namespace Metaprogramming.DynamicProxiesDemo;

/// <summary>
/// Base class for the interceptors in this example. Holds the target and the
/// exception-unwrapping that every DispatchProxy needs and that is easy to get wrong.
/// </summary>
public abstract class InterceptingProxy : DispatchProxy
{
    /// <summary>The real object the calls are forwarded to.</summary>
    protected object? Target { get; private set; }

    protected void SetTarget(object target) => Target = target;

    /// <summary>
    /// Forwards the call to the target and unwraps the reflection wrapper so that
    /// callers see the exception the target actually threw.
    /// </summary>
    protected object? InvokeTarget(MethodInfo targetMethod, object?[]? args)
    {
        try
        {
            return targetMethod.Invoke(Target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Without this, every exception from the target arrives as
            // TargetInvocationException and the caller's catch blocks stop matching.
            // ExceptionDispatchInfo preserves the original stack trace.
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(ex.InnerException)
                .Throw();
            throw; // unreachable; keeps the compiler happy
        }
    }

    /// <summary>
    /// Creates a proxy of type <typeparamref name="TProxy"/> that implements
    /// <typeparamref name="TInterface"/> and forwards to <paramref name="target"/>.
    /// </summary>
    public static TInterface Wrap<TInterface, TProxy>(TInterface target, Action<TProxy>? configure = null)
        where TProxy : InterceptingProxy
    {
        ArgumentNullException.ThrowIfNull(target);

        // DispatchProxy.Create generates a TYPE at runtime that implements TInterface and
        // derives from TProxy, then creates an instance of it. Reflection.Emit under the hood.
        object proxy = Create<TInterface, TProxy>()
            ?? throw new InvalidOperationException("DispatchProxy.Create returned null.");

        var intercepting = (TProxy)proxy;
        intercepting.SetTarget(target);
        configure?.Invoke(intercepting);

        return (TInterface)proxy;
    }
}

/// <summary>
/// Writes a line before and after every call, with the elapsed time.
/// <para>
/// NOT sealed, and that is a requirement rather than an oversight: DispatchProxy.Create
/// generates a type that DERIVES from this one, so a sealed proxy base fails at run time
/// with ArgumentException ("The base type ... cannot be sealed").
/// </para>
/// </summary>
public class LoggingProxy : InterceptingProxy
{
    public List<string> Log { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        string arguments = args is null or { Length: 0 }
            ? string.Empty
            : string.Join(", ", args.Select(a => a?.ToString() ?? "null"));

        Log.Add("--> " + targetMethod.Name + "(" + arguments + ")");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            object? result = InvokeTarget(targetMethod, args);
            stopwatch.Stop();

            Log.Add("<-- " + targetMethod.Name + " returned " + (result?.ToString() ?? "void")
                + " in " + stopwatch.Elapsed.TotalMilliseconds.ToString("N2") + " ms");

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log.Add("!!! " + targetMethod.Name + " threw " + ex.GetType().Name + ": " + ex.Message);
            throw;
        }
    }
}

/// <summary>Caches results per method plus argument list.</summary>
public class CachingProxy : InterceptingProxy
{
    private readonly Dictionary<string, object?> _cache = [];

    public int Hits { get; private set; }

    public int Misses { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        // Only cache methods that actually return something and are marked as cacheable.
        bool cacheable = targetMethod.ReturnType != typeof(void)
            && targetMethod.GetCustomAttribute<CacheableAttribute>() is not null;

        if (!cacheable)
        {
            return InvokeTarget(targetMethod, args);
        }

        string key = targetMethod.Name + "(" + string.Join(",", (args ?? []).Select(a => a?.ToString())) + ")";

        if (_cache.TryGetValue(key, out object? cached))
        {
            Hits++;
            return cached;
        }

        Misses++;
        object? result = InvokeTarget(targetMethod, args);
        _cache[key] = result;
        return result;
    }
}

/// <summary>Retries a failing call a fixed number of times.</summary>
public class RetryProxy : InterceptingProxy
{
    public int MaxAttempts { get; set; } = 3;

    public List<string> Attempts { get; } = [];

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                object? result = InvokeTarget(targetMethod, args);
                Attempts.Add("attempt " + attempt + ": succeeded");
                return result;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                Attempts.Add("attempt " + attempt + ": " + ex.GetType().Name + " - retrying");
            }
        }
    }
}

/// <summary>Marks a method whose result may be cached by <see cref="CachingProxy"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class CacheableAttribute : Attribute;
