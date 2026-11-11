using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Metaprogramming.Basics;

/// <summary>
/// Part 1 of the session: the difference between normal programming and metaprogramming,
/// why metaprogramming exists, compile-time vs runtime, and implicit vs explicit metadata.
/// Nothing here is exotic - it is deliberately the simplest possible demonstration.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. NORMAL PROGRAMMING - code that operates on DATA");
        NormalProgramming();

        Banner("2. THE PROBLEM - hand-written code does not scale across types");
        TheProblem();

        Banner("3. METAPROGRAMMING - code that operates on PROGRAM STRUCTURE");
        MetaprogrammingDemo();

        Banner("4. COMPILE-TIME vs RUNTIME knowledge");
        CompileTimeVsRuntime();

        Banner("5. IMPLICIT vs EXPLICIT METADATA");
        ImplicitVsExplicitMetadata();

        Banner("6. WHEN METAPROGRAMMING IS THE WRONG TOOL");
        WhenNotToUseIt();
    }

    // ---------------------------------------------------------------------
    // 1. Normal programming: the code reads and writes values.
    //    The *shape* of Customer is fixed at compile time and we hard-code it.
    // ---------------------------------------------------------------------
    private static void NormalProgramming()
    {
        var customer = new Customer(7, "Asha Menon", "asha@example.com", IsActive: true);

        // One line per property. The compiler checked every one of them.
        Console.WriteLine("Id       = " + customer.Id);
        Console.WriteLine("FullName = " + customer.FullName);
        Console.WriteLine("Email    = " + customer.Email);
        Console.WriteLine("IsActive = " + customer.IsActive);
        Console.WriteLine();
        Console.WriteLine("The code above mentions every property by name. That is normal programming:");
        Console.WriteLine("the program's structure is knowledge the DEVELOPER has, not knowledge the PROGRAM has.");
    }

    // ---------------------------------------------------------------------
    // 2. The problem. A hand-written "describe this object" routine must be
    //    rewritten for every type, and edited again every time a property is added.
    // ---------------------------------------------------------------------
    private static void TheProblem()
    {
        var customer = new Customer(7, "Asha Menon", "asha@example.com", IsActive: true);
        var product = new Product(101, "Mechanical Keyboard", 4999.00m);

        Console.WriteLine("Hand-written describers (one method per type):");
        foreach (var line in HandWritten.Describe(customer))
        {
            Console.WriteLine("  " + line);
        }

        foreach (var line in HandWritten.Describe(product))
        {
            Console.WriteLine("  " + line);
        }

        Console.WriteLine();
        Console.WriteLine("Cost of this approach:");
        Console.WriteLine("  - 1 new method for every new type (N types -> N methods).");
        Console.WriteLine("  - Every added property means editing the describer too (easy to forget).");
        Console.WriteLine("  - The methods are all the same shape: read each property, format it.");
        Console.WriteLine("That repetition is the signal that the real task is 'walk a type's properties'.");
    }

    // ---------------------------------------------------------------------
    // 3. The metaprogramming solution: ONE routine that asks the type about itself.
    // ---------------------------------------------------------------------
    private static void MetaprogrammingDemo()
    {
        object[] anything =
        [
            new Customer(7, "Asha Menon", "asha@example.com", IsActive: true),
            new Product(101, "Mechanical Keyboard", 4999.00m),
            new Order(5001, new DateOnly(2026, 11, 10), 2)
        ];

        Console.WriteLine("One reflection-based describer, used for three unrelated types:");
        foreach (var item in anything)
        {
            foreach (var line in Reflective.Describe(item))
            {
                Console.WriteLine("  " + line);
            }

            Console.WriteLine();
        }

        Console.WriteLine("Reflective.Describe never mentions Customer, Product or Order.");
        Console.WriteLine("It asks the runtime 'what properties does this object's type have?' and answers from that.");
        Console.WriteLine("The program now reasons about its own structure => metaprogramming.");
    }

    // ---------------------------------------------------------------------
    // 4. Compile-time vs runtime knowledge.
    // ---------------------------------------------------------------------
    private static void CompileTimeVsRuntime()
    {
        // typeof and nameof are resolved by the C# compiler. They cost nothing at runtime
        // and they are checked for you - a typo is a build error.
        Console.WriteLine("typeof(Customer).Name     = " + typeof(Customer).Name + "          <- compiler baked this in");
        Console.WriteLine("nameof(Customer.FullName) = " + nameof(Customer.FullName) + "          <- compiler baked this in");

        // A string, by contrast, is only resolved while the program runs.
        // This is the same lookup a config file or a plugin manifest would need.
        const string typeName = "Metaprogramming.Basics.Customer";
        Type? discovered = Type.GetType(typeName);
        Console.WriteLine("Type.GetType(\"" + typeName + "\") = " + (discovered?.Name ?? "null") + "   <- resolved at RUNTIME");

        // Why Type.GetType returns null: the name is not assembly-qualified, and the type is
        // neither in the calling assembly nor in System.Private.CoreLib.
        Type? notFound = Type.GetType("Some.Other.Assembly.Thing");
        Console.WriteLine("Type.GetType(\"Some.Other.Assembly.Thing\") = "
            + (notFound is null ? "null (not found - expected)" : notFound.Name));

        Console.WriteLine();
        Console.WriteLine("Three stages worth keeping apart:");
        Console.WriteLine("  compile-time generation : Roslyn source generators add real C# before the build finishes");
        Console.WriteLine("  runtime introspection   : Reflection reads metadata that is already in the assembly");
        Console.WriteLine("  runtime generation      : Reflection.Emit creates NEW IL while the process runs");
    }

    // ---------------------------------------------------------------------
    // 5. Implicit vs explicit metadata.
    //    NOTE: "implicit vs explicit" is a teaching distinction, not a CLR category.
    //    The CLR itself just has metadata tables; custom attributes are one of those tables.
    // ---------------------------------------------------------------------
    private static void ImplicitVsExplicitMetadata()
    {
        Type t = typeof(Customer);

        Console.WriteLine("IMPLICIT metadata - emitted by the compiler purely because you declared the type:");
        foreach (PropertyInfo p in t.GetProperties())
        {
            Console.WriteLine($"  {p.Name,-9} : {p.PropertyType.Name,-8} (CanRead={p.CanRead}, CanWrite={p.CanWrite})");
        }

        Console.WriteLine();
        Console.WriteLine("EXPLICIT metadata - attributes you deliberately attached:");
        foreach (PropertyInfo p in t.GetProperties())
        {
            // GetCustomAttributes reads the CustomAttribute metadata table for this member.
            object[] attributes = p.GetCustomAttributes(inherit: false);
            if (attributes.Length == 0)
            {
                continue;
            }

            string names = string.Join(", ", attributes.Select(a => a.GetType().Name));
            Console.WriteLine($"  {p.Name,-9} : {names}");
        }

        Console.WriteLine();
        Console.WriteLine("Both kinds live in the same assembly metadata and are read with the same Reflection API.");
        Console.WriteLine("The only difference is who put them there: the compiler, or you.");
    }

    // ---------------------------------------------------------------------
    // 6. Honest counterweight: metaprogramming is not free.
    // ---------------------------------------------------------------------
    private static void WhenNotToUseIt()
    {
        Console.WriteLine("Reflection replaced N hand-written methods with one. That is a real win.");
        Console.WriteLine("It also cost us something, and the cost is worth naming out loud:");
        Console.WriteLine();
        Console.WriteLine("  - Compile-time safety: renaming Customer.Email breaks nothing at build time,");
        Console.WriteLine("    so a reflection-driven feature can fail only once it is running.");
        Console.WriteLine("  - Debuggability: a stack trace through Invoke tells you much less than a direct call.");
        Console.WriteLine("  - Speed: a reflective property read is far slower than a direct one (see 05-DynamicMethod).");
        Console.WriteLine("  - Trimming / Native AOT: the trimmer cannot see reflective usage, so types may be removed.");
        Console.WriteLine();
        Console.WriteLine("Rule of thumb used throughout this session:");
        Console.WriteLine("  Reach for metaprogramming when the set of types is NOT known to you as you write the code");
        Console.WriteLine("  (plugins, serializers, mappers, DI containers). Do not reach for it just to avoid typing.");
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
// Model types. The attributes on Customer are the "explicit metadata".
// =========================================================================

internal sealed record Customer(
    int Id,
    [property: Required] [property: StringLength(100)] string FullName,
    [property: Required] [property: EmailAddress] [property: JsonPropertyName("email_address")] string Email,
    bool IsActive);

internal sealed record Product(int Sku, string Name, decimal Price);

internal sealed record Order(int OrderId, DateOnly PlacedOn, int LineCount);

// =========================================================================
// The hand-written approach: correct, fast, and duplicated per type.
// =========================================================================

internal static class HandWritten
{
    public static IEnumerable<string> Describe(Customer c)
    {
        yield return "Customer.Id = " + c.Id;
        yield return "Customer.FullName = " + c.FullName;
        yield return "Customer.Email = " + c.Email;
        yield return "Customer.IsActive = " + c.IsActive;
    }

    public static IEnumerable<string> Describe(Product p)
    {
        yield return "Product.Sku = " + p.Sku;
        yield return "Product.Name = " + p.Name;
        yield return "Product.Price = " + p.Price;

        // If someone adds Product.Weight, this method silently keeps reporting three values.
    }
}

// =========================================================================
// The metaprogramming approach: one routine for every type, present and future.
// =========================================================================

internal static class Reflective
{
    public static IEnumerable<string> Describe(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        // GetType() returns the RUNTIME type of the object - the real thing in memory,
        // not the static type of the variable that happens to hold it.
        Type type = instance.GetType();

        yield return "-- " + type.Name + " (" + type.GetProperties().Length + " public properties)";

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            // GetValue runs the property's getter through the runtime.
            object? value = property.GetValue(instance);
            yield return type.Name + "." + property.Name + " = " + value;
        }
    }
}
