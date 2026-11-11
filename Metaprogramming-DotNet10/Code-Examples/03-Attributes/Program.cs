using System.Reflection;

namespace Metaprogramming.AttributesDemo;

/// <summary>
/// Part 2 of the session: attributes as explicit, declarative metadata, and the
/// Reflection APIs that read them back. Ends with a small attribute-driven
/// validation engine - the same pattern ASP.NET Core model validation uses.
/// </summary>
internal static class Program
{
    private static void Main()
    {
        Banner("1. WHAT AN ATTRIBUTE IS - data attached to a declaration");
        WhatAnAttributeIs();

        Banner("2. AttributeUsage - AttributeTargets, AllowMultiple, Inherited");
        AttributeUsageRules();

        Banner("3. AllowMultiple IN PRACTICE");
        AllowMultipleDemo();

        Banner("4. Inherited IN PRACTICE");
        InheritedDemo();

        Banner("5. READING ATTRIBUTES WITHOUT INSTANTIATING THEM (CustomAttributeData)");
        CustomAttributeDataDemo();

        Banner("6. AN ATTRIBUTE-DRIVEN VALIDATION ENGINE");
        ValidationEngineDemo();

        Banner("7. WHAT TO REMEMBER");
        Summary();
    }

    // =====================================================================
    // 1. An attribute is a class. Applying it stores a construction recipe
    //    in metadata; reading it back constructs the instance.
    // =====================================================================
    private static void WhatAnAttributeIs()
    {
        Type t = typeof(InvoiceLine);

        // GetCustomAttribute<T>() returns a real, constructed attribute object.
        var table = t.GetCustomAttribute<TableAttribute>();
        Console.WriteLine("Type-level attribute on InvoiceLine:");
        Console.WriteLine("  [Table(Name = \"" + table?.Name + "\", Schema = \"" + table?.Schema + "\")]");
        Console.WriteLine("  the returned object is an instance of " + table?.GetType().Name);

        Console.WriteLine();
        Console.WriteLine("Member-level attributes:");
        foreach (PropertyInfo p in t.GetProperties())
        {
            var column = p.GetCustomAttribute<ColumnAttribute>();
            string description = column is null
                ? "(no [Column] - convention would use the property name)"
                : "[Column(\"" + column.Name + "\", Order = " + column.Order + ")]";

            Console.WriteLine($"  {p.Name,-12} {description}");
        }

        Console.WriteLine();
        Console.WriteLine("Key point: the attribute instance did not exist until we asked for it.");
        Console.WriteLine("The assembly stores the constructor arguments; Reflection runs the constructor on demand.");
        Console.WriteLine("Two calls to GetCustomAttribute therefore need not return the same object:");
        Console.WriteLine("  ReferenceEquals(first, second) = "
            + ReferenceEquals(t.GetCustomAttribute<TableAttribute>(), t.GetCustomAttribute<TableAttribute>()));
    }

    // =====================================================================
    // 2. AttributeUsage controls where an attribute may be applied.
    // =====================================================================
    private static void AttributeUsageRules()
    {
        foreach (Type attributeType in new[]
                 {
                     typeof(TableAttribute), typeof(ColumnAttribute),
                     typeof(ValidateRangeAttribute), typeof(AuditedAttribute), typeof(TagAttribute)
                 })
        {
            // AttributeUsageAttribute is itself an attribute, read with the same API.
            var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

            Console.WriteLine(attributeType.Name + ":");
            Console.WriteLine("  ValidOn       = " + usage?.ValidOn);
            Console.WriteLine("  AllowMultiple = " + usage?.AllowMultiple);
            Console.WriteLine("  Inherited     = " + usage?.Inherited);
        }

        Console.WriteLine();
        Console.WriteLine("Defaults when you omit [AttributeUsage]: ValidOn = All, AllowMultiple = false, Inherited = true.");
        Console.WriteLine("Declaring it explicitly is worth the two lines - it is compiler-enforced documentation.");
    }

    // =====================================================================
    // 3. AllowMultiple = true lets the same attribute repeat on one target.
    // =====================================================================
    private static void AllowMultipleDemo()
    {
        Type t = typeof(InvoiceLine);

        // GetCustomAttributes (plural) is the one to use when AllowMultiple is true.
        TagAttribute[] tags = t.GetCustomAttributes<TagAttribute>().ToArray();
        Console.WriteLine("InvoiceLine carries " + tags.Length + " [Tag] attributes:");
        foreach (TagAttribute tag in tags)
        {
            Console.WriteLine("  " + tag.Value);
        }

        Console.WriteLine();
        Console.WriteLine("With AllowMultiple = false, a second application is a COMPILE error (CS0579),");
        Console.WriteLine("so the constraint is enforced by the compiler, not discovered at runtime.");
        Console.WriteLine();
        Console.WriteLine("Careful: GetCustomAttribute<T>() (singular) THROWS AmbiguousMatchException");
        Console.WriteLine("when more than one is present. Demonstrating that now:");
        try
        {
            t.GetCustomAttribute<TagAttribute>();
        }
        catch (AmbiguousMatchException)
        {
            Console.WriteLine("  AmbiguousMatchException - use GetCustomAttributes<T>() instead.");
        }
    }

    // =====================================================================
    // 4. Inherited controls whether a derived type sees the base's attribute.
    // =====================================================================
    private static void InheritedDemo()
    {
        // [Audited] is Inherited = true; [Table] is Inherited = false.
        Console.WriteLine("BaseEntity declares [Audited] (Inherited=true) and [Table] (Inherited=false).");
        Console.WriteLine("DerivedEntity declares neither.");
        Console.WriteLine();

        Type derived = typeof(DerivedEntity);

        Console.WriteLine("inherit: false - look only at the type's own metadata:");
        Console.WriteLine("  [Audited] found = " + (derived.GetCustomAttributes(typeof(AuditedAttribute), inherit: false).Length > 0));
        Console.WriteLine("  [Table]   found = " + (derived.GetCustomAttributes(typeof(TableAttribute), inherit: false).Length > 0));

        Console.WriteLine();
        Console.WriteLine("inherit: true - also walk the base chain:");
        Console.WriteLine("  [Audited] found = " + (derived.GetCustomAttributes(typeof(AuditedAttribute), inherit: true).Length > 0)
            + "   <- Inherited=true on the attribute, and we asked for inheritance");
        Console.WriteLine("  [Table]   found = " + (derived.GetCustomAttributes(typeof(TableAttribute), inherit: true).Length > 0)
            + "  <- Inherited=false on the attribute, so inheritance is refused");

        Console.WriteLine();
        Console.WriteLine("Both switches must agree: the attribute must be declared Inherited=true AND");
        Console.WriteLine("the call must pass inherit:true. Either one off means no result.");
    }

    // =====================================================================
    // 5. CustomAttributeData reads the recipe WITHOUT running the constructor.
    // =====================================================================
    private static void CustomAttributeDataDemo()
    {
        Type t = typeof(InvoiceLine);

        Console.WriteLine("GetCustomAttributesData() returns the raw metadata rows:");
        foreach (CustomAttributeData data in t.GetCustomAttributesData())
        {
            string positional = string.Join(", ", data.ConstructorArguments.Select(a => a.Value?.ToString() ?? "null"));
            string named = string.Join(", ", data.NamedArguments.Select(a => a.MemberName + " = " + a.TypedValue.Value));
            string arguments = string.Join(", ", new[] { positional, named }.Where(s => s.Length > 0));

            Console.WriteLine("  " + data.AttributeType.Name + "(" + arguments + ")");
        }

        Console.WriteLine();
        Console.WriteLine("Why this matters:");
        Console.WriteLine("  - No attribute constructor runs, so untrusted or broken attributes cannot execute code.");
        Console.WriteLine("  - The attribute's type does not have to be loadable.");
        Console.WriteLine("  - This is what analysis tools and MetadataLoadContext use.");
    }

    // =====================================================================
    // 6. The payoff: one validator that works for any annotated type.
    // =====================================================================
    private static void ValidationEngineDemo()
    {
        var good = new Payment { Amount = 2500m, CurrencyCode = "INR", Reference = "PAY-001" };
        var bad = new Payment { Amount = -5m, CurrencyCode = "RUPEES", Reference = "" };

        foreach (var (label, payment) in new[] { ("VALID", good), ("INVALID", bad) })
        {
            Console.WriteLine(label + " payment: Amount=" + payment.Amount
                + " CurrencyCode=\"" + payment.CurrencyCode + "\" Reference=\"" + payment.Reference + "\"");

            IReadOnlyList<string> errors = AttributeValidator.Validate(payment);
            if (errors.Count == 0)
            {
                Console.WriteLine("  OK - no violations");
            }
            else
            {
                foreach (string error in errors)
                {
                    Console.WriteLine("  ERROR: " + error);
                }
            }

            Console.WriteLine();
        }

        Console.WriteLine("AttributeValidator has no knowledge of Payment. Annotate a new class today");
        Console.WriteLine("and the validator handles it without being recompiled or edited.");
    }

    private static void Summary()
    {
        Console.WriteLine("  - An attribute is a class deriving from System.Attribute.");
        Console.WriteLine("  - Applying it writes a row into the assembly's CustomAttribute metadata table.");
        Console.WriteLine("  - GetCustomAttributes* CONSTRUCTS the attribute; GetCustomAttributesData only READS it.");
        Console.WriteLine("  - AllowMultiple=false is enforced by the compiler (CS0579).");
        Console.WriteLine("  - Inheritance needs Inherited=true on the attribute AND inherit:true at the call site.");
        Console.WriteLine("  - Real-world users of exactly this pattern: ASP.NET Core routing and model validation,");
        Console.WriteLine("    System.Text.Json naming, EF Core data annotations, xUnit test discovery.");
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
// Custom attributes. Each one demonstrates a different AttributeUsage setting.
// =========================================================================

/// <summary>Maps a class to a database table. Single use, not inherited.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class TableAttribute(string name) : Attribute
{
    // A positional (constructor) argument.
    public string Name { get; } = name;

    // A named argument: a settable property, assigned with Schema = "..." at the use site.
    public string Schema { get; set; } = "dbo";
}

/// <summary>Maps a property to a column.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    public int Order { get; set; } = -1;
}

/// <summary>Repeatable free-text tag.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
internal sealed class TagAttribute(string value) : Attribute
{
    public string Value { get; } = value;
}

/// <summary>Marks a type as audited. Inherited, so subclasses are audited too.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
internal sealed class AuditedAttribute : Attribute
{
    public string Reason { get; set; } = "unspecified";
}

/// <summary>Validation: the value must sit inside an inclusive numeric range.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class ValidateRangeAttribute(double minimum, double maximum) : Attribute
{
    public double Minimum { get; } = minimum;

    public double Maximum { get; } = maximum;
}

/// <summary>Validation: the string must be non-empty and of an exact length.</summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
internal sealed class ValidateTextAttribute : Attribute
{
    public int ExactLength { get; set; } = -1;

    public bool Required { get; set; }
}

// =========================================================================
// Types the demo inspects.
// =========================================================================

[Table("invoice_lines", Schema = "sales")]
[Tag("billing")]
[Tag("generated-nightly")]
[Tag("pii-free")]
internal sealed class InvoiceLine
{
    [Column("line_id", Order = 0)]
    public int LineId { get; set; }

    [Column("description", Order = 1)]
    public string Description { get; set; } = string.Empty;

    // Deliberately un-annotated, to show the "convention applies" case.
    public decimal UnitPrice { get; set; }
}

[Audited(Reason = "financial record")]
[Table("base_entities")]
internal class BaseEntity
{
    public int Id { get; set; }
}

internal sealed class DerivedEntity : BaseEntity
{
    public string Note { get; set; } = string.Empty;
}

internal sealed class Payment
{
    [ValidateRange(0.01, 1_000_000)]
    public decimal Amount { get; set; }

    [ValidateText(ExactLength = 3, Required = true)]
    public string CurrencyCode { get; set; } = string.Empty;

    [ValidateText(Required = true)]
    public string Reference { get; set; } = string.Empty;

    // No validation attributes: the engine must skip this property silently.
    public string? Notes { get; set; }
}

// =========================================================================
// The engine. Note that it never names Payment or any of its properties.
// =========================================================================

internal static class AttributeValidator
{
    public static IReadOnlyList<string> Validate(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var errors = new List<string>();
        Type type = instance.GetType();

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            object? value = property.GetValue(instance);

            // --- Range rule ---
            var range = property.GetCustomAttribute<ValidateRangeAttribute>();
            if (range is not null && value is not null)
            {
                // Convert.ToDouble handles int, decimal, double, float uniformly.
                double numeric = Convert.ToDouble(value);
                if (numeric < range.Minimum || numeric > range.Maximum)
                {
                    errors.Add(property.Name + " must be between " + range.Minimum + " and " + range.Maximum
                        + " (was " + numeric + ")");
                }
            }

            // --- Text rules ---
            var text = property.GetCustomAttribute<ValidateTextAttribute>();
            if (text is not null)
            {
                string? s = value as string;

                if (text.Required && string.IsNullOrWhiteSpace(s))
                {
                    errors.Add(property.Name + " is required");
                }
                else if (text.ExactLength > 0 && s is not null && s.Length != text.ExactLength)
                {
                    errors.Add(property.Name + " must be exactly " + text.ExactLength
                        + " characters (was " + s.Length + ")");
                }
            }
        }

        return errors;
    }
}
