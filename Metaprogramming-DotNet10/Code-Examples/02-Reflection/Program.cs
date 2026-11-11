using System.Reflection;

namespace Metaprogramming.ReflectionDemo;

/// <summary>
/// Part 2 of the session: runtime introspection with the Reflection API.
/// Walks the metadata hierarchy Assembly -> Module -> Type -> Member, shows BindingFlags,
/// and finishes with Activator.CreateInstance.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. THE ASSEMBLY - the unit that carries metadata");
        InspectAssembly();

        Banner("2. FOUR WAYS TO GET A Type");
        FourWaysToGetAType();

        Banner("3. WHY Type.GetType CAN RETURN null");
        WhyGetTypeReturnsNull();

        Banner("4. TYPE DISCOVERY - every type in this assembly");
        DiscoverTypes();

        Banner("5. INSPECTING MEMBERS - methods, properties, constructors, fields");
        InspectMembers();

        Banner("6. BindingFlags - public vs non-public, instance vs static");
        BindingFlagsDemo();

        Banner("7. READING AND WRITING THROUGH REFLECTION");
        ReadWriteThroughReflection();

        Banner("8. INVOKING A METHOD BY NAME");
        InvokeByName();

        Banner("9. Activator.CreateInstance - creating objects from a Type");
        ActivatorDemo();

        Banner("10. Activator FAILURE MODES (each exception triggered on purpose)");
        ActivatorFailureModes();
    }

    // =====================================================================
    // 1. Assembly: name, location, modules, manifest-level attributes.
    // =====================================================================
    private static void InspectAssembly()
    {
        // GetExecutingAssembly returns the assembly containing the code that is running.
        Assembly current = Assembly.GetExecutingAssembly();
        AssemblyName name = current.GetName();

        Console.WriteLine("Simple name      : " + name.Name);
        Console.WriteLine("Version          : " + name.Version);
        Console.WriteLine("FullName         : " + current.FullName);
        Console.WriteLine("Location on disk : " + Path.GetFileName(current.Location));
        Console.WriteLine("IsDynamic        : " + current.IsDynamic);
        Console.WriteLine("EntryPoint       : " + current.EntryPoint?.DeclaringType?.FullName + "." + current.EntryPoint?.Name);

        // A .NET assembly is a container of one or more modules. Single-module is the norm.
        Module[] modules = current.GetModules();
        Console.WriteLine("Modules          : " + string.Join(", ", modules.Select(m => m.Name)));

        // The manifest also records which assemblies this one depends on.
        AssemblyName[] references = current.GetReferencedAssemblies();
        Console.WriteLine("Referenced       : " + references.Length + " assemblies, e.g. "
            + string.Join(", ", references.Take(3).Select(r => r.Name)));

        Console.WriteLine();
        Console.WriteLine("Everything above was read from metadata tables the compiler wrote into the DLL.");
        Console.WriteLine("No source code was consulted; the assembly describes itself.");
    }

    // =====================================================================
    // 2. Four ways to obtain a Type, and what distinguishes them.
    // =====================================================================
    private static void FourWaysToGetAType()
    {
        // (a) typeof - compile-time. The C# compiler emits a metadata token. Fastest, type-checked.
        Type a = typeof(BankAccount);
        Console.WriteLine("(a) typeof(BankAccount)            -> " + a.FullName);

        // (b) instance.GetType() - runtime type of an actual object.
        object instance = new SavingsAccount("SB-1", 1000m, 0.04m);
        Type b = instance.GetType();
        Console.WriteLine("(b) instance.GetType()             -> " + b.FullName + "   (note: NOT BankAccount)");
        Console.WriteLine("    static type of the variable is object, runtime type is " + b.Name);

        // (c) Type.GetType(string) - resolved at runtime from a name. The plugin/config case.
        Type? c = Type.GetType("Metaprogramming.ReflectionDemo.BankAccount");
        Console.WriteLine("(c) Type.GetType(\"...BankAccount\") -> " + (c?.FullName ?? "null"));

        // (d) Assembly.GetType(string) - look inside a specific assembly.
        Type? d = Assembly.GetExecutingAssembly().GetType("Metaprogramming.ReflectionDemo.SavingsAccount");
        Console.WriteLine("(d) assembly.GetType(\"...\")        -> " + (d?.FullName ?? "null"));

        Console.WriteLine();
        Console.WriteLine("All four produce the same kind of object: a System.Type describing the type.");
        Console.WriteLine("They differ only in WHEN and WHERE the lookup happens.");
    }

    // =====================================================================
    // 3. The single most common reflection surprise.
    // =====================================================================
    private static void WhyGetTypeReturnsNull()
    {
        // Type.GetType searches only: the calling assembly, and System.Private.CoreLib (mscorlib).
        // A type from any OTHER assembly must be named with an assembly-qualified name.
        Type? unqualified = Type.GetType("System.Text.Json.JsonSerializerOptions");
        Console.WriteLine("Unqualified name from another assembly:");
        Console.WriteLine("  Type.GetType(\"System.Text.Json.JsonSerializerOptions\") = "
            + (unqualified?.Name ?? "null  <- this is the trap"));

        // The assembly-qualified form is "Namespace.TypeName, AssemblySimpleName".
        Type? qualified = Type.GetType("System.Text.Json.JsonSerializerOptions, System.Text.Json");
        Console.WriteLine("Assembly-qualified name:");
        Console.WriteLine("  Type.GetType(\"...JsonSerializerOptions, System.Text.Json\") = " + (qualified?.Name ?? "null"));

        // AssemblyQualifiedName shows you the exact string that always works.
        Console.WriteLine();
        Console.WriteLine("The canonical string is on the Type itself:");
        Console.WriteLine("  typeof(JsonSerializerOptions).AssemblyQualifiedName =");
        Console.WriteLine("    " + typeof(System.Text.Json.JsonSerializerOptions).AssemblyQualifiedName);

        // throwOnError:true converts the silent null into a diagnosable exception.
        Console.WriteLine();
        Console.WriteLine("Prefer throwOnError:true when a missing type is a bug rather than a condition:");
        try
        {
            Type.GetType("Totally.Missing.Type", throwOnError: true);
        }
        catch (TypeLoadException ex)
        {
            Console.WriteLine("  TypeLoadException: " + ex.Message.Split('.')[0] + ".");
        }

        Console.WriteLine();
        Console.WriteLine("Three reasons GetType returns null:");
        Console.WriteLine("  1. The name is not assembly-qualified and the type lives elsewhere.");
        Console.WriteLine("  2. The containing assembly has not been loaded yet (nothing triggers a load).");
        Console.WriteLine("  3. A typo, or a nested type written with '.' instead of '+' (Outer+Inner).");
    }

    // =====================================================================
    // 4. Enumerating the types in an assembly.
    // =====================================================================
    private static void DiscoverTypes()
    {
        Assembly current = Assembly.GetExecutingAssembly();

        // GetTypes returns every type, including compiler-generated ones. Filter for teaching.
        Type[] all = current.GetTypes();
        Console.WriteLine("assembly.GetTypes() returned " + all.Length + " types (compiler-generated ones included).");

        Console.WriteLine();
        Console.WriteLine("The types we actually declared:");
        IEnumerable<Type> declared = all
            .Where(t => !t.Name.StartsWith('<'))          // skip <>c closures / iterators
            .Where(t => t.Namespace == "Metaprogramming.ReflectionDemo")
            .OrderBy(t => t.Name);

        foreach (Type t in declared)
        {
            string kind = t.IsInterface ? "interface"
                : t.IsEnum ? "enum"
                : t.IsAbstract && t.IsSealed ? "static class"
                : t.IsAbstract ? "abstract class"
                : t.IsValueType ? "struct"
                : "class";

            Console.WriteLine($"  {t.Name,-22} {kind,-15} base={t.BaseType?.Name ?? "-"}");
        }

        Console.WriteLine();
        Console.WriteLine("Finding all implementations of an interface - the core of plugin discovery:");
        foreach (Type t in all.Where(t => typeof(IInterestBearing).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract))
        {
            Console.WriteLine("  " + t.Name + " implements IInterestBearing");
        }

        Console.WriteLine();
        Console.WriteLine("Note: use IsAssignableFrom, not equality - it accounts for inheritance.");
        Console.WriteLine("Note: GetTypes() throws ReflectionTypeLoadException if a dependency is missing;");
        Console.WriteLine("      production discovery code catches it and reads ex.Types for what did load.");
    }

    // =====================================================================
    // 5. The four member-info families.
    // =====================================================================
    private static void InspectMembers()
    {
        Type t = typeof(SavingsAccount);

        // --- Constructors ---
        Console.WriteLine("CONSTRUCTORS (ConstructorInfo):");
        foreach (ConstructorInfo ctor in t.GetConstructors())
        {
            string parameters = string.Join(", ", ctor.GetParameters()
                .Select(p => p.ParameterType.Name + " " + p.Name + (p.HasDefaultValue ? " = " + p.DefaultValue : "")));
            Console.WriteLine("  .ctor(" + parameters + ")");
        }

        // --- Properties ---
        Console.WriteLine();
        Console.WriteLine("PROPERTIES (PropertyInfo):");
        foreach (PropertyInfo p in t.GetProperties())
        {
            string access = (p.CanRead ? "get" : "") + (p.CanWrite ? ";set" : "");
            Console.WriteLine($"  {p.PropertyType.Name,-10} {p.Name,-14} {{{access}}}   declared on {p.DeclaringType?.Name}");
        }

        // --- Methods (declared only, so we skip the 4 object members) ---
        Console.WriteLine();
        Console.WriteLine("METHODS (MethodInfo) - DeclaredOnly to hide inherited object members:");
        foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            // Property getters/setters are real methods too; they are flagged IsSpecialName.
            string flag = m.IsSpecialName ? "  (property accessor)" : "";
            string parameters = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            Console.WriteLine($"  {m.ReturnType.Name,-10} {m.Name}({parameters}){flag}");
        }

        // --- Fields ---
        Console.WriteLine();
        Console.WriteLine("FIELDS (FieldInfo) - including the compiler's backing fields:");
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            string visibility = f.IsPublic ? "public" : f.IsPrivate ? "private" : "other";
            Console.WriteLine($"  {visibility,-8} {f.FieldType.Name,-10} {f.Name}");
        }

        Console.WriteLine();
        Console.WriteLine("All five *Info types derive from MemberInfo, which is why they share Name,");
        Console.WriteLine("DeclaringType and GetCustomAttributes.");
    }

    // =====================================================================
    // 6. BindingFlags decide WHICH members you get back.
    // =====================================================================
    private static void BindingFlagsDemo()
    {
        Type t = typeof(SavingsAccount);

        Console.WriteLine("GetMethods() with no arguments means Public | Instance | Static:");
        Console.WriteLine("  count = " + t.GetMethods().Length + " (includes inherited object methods)");

        Console.WriteLine();
        Console.WriteLine("Public | Instance | DeclaredOnly  -> " + t.GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);

        Console.WriteLine("NonPublic | Instance | DeclaredOnly -> " + t.GetMethods(
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);

        Console.WriteLine();
        Console.WriteLine("The private methods reflection can see:");
        foreach (MethodInfo m in t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            string parameters = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            Console.WriteLine("  private " + m.ReturnType.Name + " " + m.Name + "(" + parameters + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Static members need BindingFlags.Static - they are NOT returned by Instance:");
        foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            string parameters = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            Console.WriteLine("  public static " + m.ReturnType.Name + " " + m.Name + "(" + parameters + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Important: 'private' is a COMPILER rule, enforced at compile time.");
        Console.WriteLine("Reflection with BindingFlags.NonPublic is allowed to read it at runtime.");
        Console.WriteLine("Accessibility is an encapsulation feature, not a security boundary.");
    }

    // =====================================================================
    // 7. Reading and writing state reflectively, private members included.
    // =====================================================================
    private static void ReadWriteThroughReflection()
    {
        var account = new SavingsAccount("SB-9", 5000m, 0.05m);
        Type t = account.GetType();

        // Read a public property.
        PropertyInfo balance = t.GetProperty(nameof(SavingsAccount.Balance))!;
        Console.WriteLine("Read  Balance via PropertyInfo.GetValue -> " + balance.GetValue(account));

        // Write a public settable property.
        PropertyInfo nickname = t.GetProperty(nameof(SavingsAccount.Nickname))!;
        nickname.SetValue(account, "Rainy day fund");
        Console.WriteLine("Wrote Nickname via PropertyInfo.SetValue -> " + account.Nickname);

        // Reach a private field. This is how serializers restore objects that have no setters.
        FieldInfo? auditTrail = t.GetField("_auditTrail", BindingFlags.NonPublic | BindingFlags.Instance);
        Console.WriteLine("Read  private field _auditTrail         -> " + auditTrail?.GetValue(account));

        auditTrail?.SetValue(account, "rewritten-by-reflection");
        Console.WriteLine("Wrote private field _auditTrail         -> " + auditTrail?.GetValue(account));

        Console.WriteLine();
        Console.WriteLine("Legitimate uses: deserialization, ORM materialisation, test fixtures.");
        Console.WriteLine("Illegitimate use: routing around someone else's invariants in production code.");
    }

    // =====================================================================
    // 8. Invoking a method whose name is only known at runtime.
    // =====================================================================
    private static void InvokeByName()
    {
        var account = new SavingsAccount("SB-3", 2000m, 0.06m);

        // The name could have come from a config file, an HTTP route, or a scripting console.
        const string methodName = "AddInterest";

        MethodInfo? method = account.GetType().GetMethod(methodName);
        if (method is null)
        {
            Console.WriteLine("Method '" + methodName + "' not found.");
            return;
        }

        Console.WriteLine("Found: " + method.ReturnType.Name + " " + method.Name
            + "(" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name)) + ")");

        // Arguments are passed as object[]; the runtime unboxes and binds them.
        object? result = method.Invoke(account, [12]);
        Console.WriteLine("Invoke(account, [12]) returned " + result);
        Console.WriteLine("Balance is now " + account.Balance);

        // Wrong argument count or type is a runtime error, not a compile error.
        Console.WriteLine();
        Console.WriteLine("Passing the wrong arguments fails at RUNTIME:");
        try
        {
            method.Invoke(account, ["not-a-number"]);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine("  " + ex.GetType().Name + ": " + ex.Message);
        }

        // An exception thrown INSIDE the target is wrapped in TargetInvocationException.
        Console.WriteLine();
        Console.WriteLine("An exception thrown inside the invoked method is wrapped:");
        MethodInfo withdraw = account.GetType().GetMethod(nameof(SavingsAccount.Withdraw))!;
        try
        {
            withdraw.Invoke(account, [decimal.MaxValue]);
        }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine("  TargetInvocationException -> InnerException is the real one:");
            Console.WriteLine("    " + ex.InnerException?.GetType().Name + ": " + ex.InnerException?.Message);
        }
    }

    // =====================================================================
    // 9. Activator.CreateInstance.
    // =====================================================================
    private static void ActivatorDemo()
    {
        // (a) Generic form - a compile-time type, needs a public parameterless constructor.
        var current = Activator.CreateInstance<CurrentAccount>();
        Console.WriteLine("(a) Activator.CreateInstance<CurrentAccount>()  -> " + current.Describe());

        // (b) Type + constructor arguments. The runtime picks the matching constructor
        //     by comparing the argument types against each ConstructorInfo's parameters.
        Type savingsType = typeof(SavingsAccount);
        object? savings = Activator.CreateInstance(savingsType, "SB-77", 15000m, 0.07m);
        Console.WriteLine("(b) Activator.CreateInstance(type, args)       -> " + ((BankAccount)savings!).Describe());

        // (c) The realistic case: the type name is a string, so nothing about it is compile-time known.
        const string typeName = "Metaprogramming.ReflectionDemo.FixedDepositAccount";
        Type? discovered = Type.GetType(typeName);
        if (discovered is not null)
        {
            object? fd = Activator.CreateInstance(discovered, "FD-12", 100000m, 24);
            Console.WriteLine("(c) from a string type name                   -> " + ((BankAccount)fd!).Describe());
        }

        // (d) Reaching a non-public constructor requires the nonPublic overload.
        object? internalOnly = Activator.CreateInstance(typeof(LedgerEntry), nonPublic: true);
        Console.WriteLine("(d) nonPublic:true reaches a private .ctor      -> " + internalOnly);

        // (e) Equivalent via ConstructorInfo - more explicit, and reusable if you cache it.
        ConstructorInfo ctor = savingsType.GetConstructor([typeof(string), typeof(decimal), typeof(decimal)])!;
        var viaCtor = (SavingsAccount)ctor.Invoke(["SB-88", 250m, 0.03m]);
        Console.WriteLine("(e) ConstructorInfo.Invoke                     -> " + viaCtor.Describe());

        Console.WriteLine();
        Console.WriteLine("Compared with 'new SavingsAccount(...)':");
        Console.WriteLine("  new      -> the compiler emits a single newobj IL instruction; type checked; fastest.");
        Console.WriteLine("  Activator-> the runtime searches constructors, binds object[] args, then constructs.");
        Console.WriteLine("  Use Activator only when the type genuinely is not known at compile time.");
    }

    // =====================================================================
    // 10. Each Activator failure mode, triggered deliberately.
    // =====================================================================
    private static void ActivatorFailureModes()
    {
        // MissingMethodException: no constructor matches the supplied arguments.
        try
        {
            Activator.CreateInstance(typeof(SavingsAccount), "only-one-arg");
        }
        catch (MissingMethodException ex)
        {
            Console.WriteLine("MissingMethodException  : " + ex.Message);
        }

        // MissingMethodException: abstract types cannot be instantiated.
        try
        {
            Activator.CreateInstance(typeof(BankAccount), "X", 0m);
        }
        catch (MissingMethodException ex)
        {
            Console.WriteLine("Abstract type           : " + ex.Message);
        }

        // MissingMethodException: the only constructor is private and nonPublic was not requested.
        try
        {
            Activator.CreateInstance(typeof(LedgerEntry));
        }
        catch (MissingMethodException)
        {
            Console.WriteLine("Private .ctor, nonPublic:false -> MissingMethodException (use the nonPublic overload)");
        }

        // TargetInvocationException: the constructor itself threw.
        try
        {
            Activator.CreateInstance(typeof(SavingsAccount), "SB-BAD", -1m, 0.01m);
        }
        catch (TargetInvocationException ex)
        {
            Console.WriteLine("Ctor threw              : TargetInvocationException -> "
                + ex.InnerException?.GetType().Name + ": " + ex.InnerException?.Message);
        }

        // ArgumentNullException: the Type argument was null.
        try
        {
            Activator.CreateInstance((Type)null!);
        }
        catch (ArgumentNullException ex)
        {
            Console.WriteLine("Null Type               : ArgumentNullException (paramName=" + ex.ParamName + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Every one of these is a RUNTIME failure. The compiler could not have warned you,");
        Console.WriteLine("which is the central tradeoff of reflective construction.");
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
// A small domain model, shaped to exercise every reflection feature above.
// =========================================================================

internal interface IInterestBearing
{
    decimal AnnualRate { get; }

    decimal ProjectInterest(int months);
}

internal abstract class BankAccount(string accountNumber, decimal balance)
{
    public string AccountNumber { get; } = accountNumber;

    public decimal Balance { get; protected set; } = balance;

    public string Nickname { get; set; } = "unnamed";

    public abstract string Describe();

    public void Withdraw(decimal amount)
    {
        if (amount > Balance)
        {
            throw new InvalidOperationException("Insufficient funds: balance is " + Balance + ".");
        }

        Balance -= amount;
    }
}

internal sealed class SavingsAccount : BankAccount, IInterestBearing
{
    // A private field, so the demo has something non-public to find.
    private string _auditTrail = "created";

    public SavingsAccount(string accountNumber, decimal balance, decimal annualRate)
        : base(accountNumber, balance)
    {
        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Opening balance cannot be negative.");
        }

        AnnualRate = annualRate;
    }

    public decimal AnnualRate { get; }

    public decimal ProjectInterest(int months) => Math.Round(Balance * AnnualRate * months / 12m, 2);

    public decimal AddInterest(int months)
    {
        decimal interest = ProjectInterest(months);
        Balance += interest;
        _auditTrail = Normalise("interest+" + interest);
        return interest;
    }

    public static string ProductCode() => "SAV-STD";

    public override string Describe() => "Savings " + AccountNumber + " balance=" + Balance + " rate=" + AnnualRate;

    // Private helper - visible to BindingFlags.NonPublic only.
    private string Normalise(string note) => note.Trim().ToLowerInvariant();
}

internal sealed class CurrentAccount : BankAccount
{
    // Public parameterless constructor, which is what Activator.CreateInstance<T>() requires.
    public CurrentAccount()
        : base("CA-0000", 0m)
    {
    }

    public override string Describe() => "Current " + AccountNumber + " balance=" + Balance;
}

internal sealed class FixedDepositAccount(string accountNumber, decimal principal, int termMonths)
    : BankAccount(accountNumber, principal), IInterestBearing
{
    public int TermMonths { get; } = termMonths;

    public decimal AnnualRate => 0.075m;

    public decimal ProjectInterest(int months) => Math.Round(Balance * AnnualRate * months / 12m, 2);

    public override string Describe() =>
        "FixedDeposit " + AccountNumber + " principal=" + Balance + " term=" + TermMonths + "m";
}

/// <summary>A type whose only constructor is private, to show the nonPublic overload.</summary>
internal sealed class LedgerEntry
{
    private LedgerEntry() => Reference = "LE-" + Guid.NewGuid().ToString("N")[..6];

    public string Reference { get; }

    public override string ToString() => "LedgerEntry " + Reference;
}
