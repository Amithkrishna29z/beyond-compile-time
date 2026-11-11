using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metaprogramming.SerializationDemo;

/// <summary>
/// Part 4 of the session: serialization as the most widely used metaprogramming in .NET,
/// and the clearest side-by-side of RUNTIME reflection versus COMPILE-TIME source generation.
/// </summary>
internal static class Program
{
    private static readonly Invoice SampleInvoice = new()
    {
        InvoiceId = 4201,
        CustomerName = "Nila Traders",
        IssuedOn = new DateOnly(2026, 11, 10),
        AmountDue = 18_750.50m,
        InternalNotes = "chase on the 15th",
        Status = InvoiceStatus.AwaitingPayment,
        Lines =
        [
            new InvoiceLine { Description = "Mechanical Keyboard", Quantity = 2, UnitPrice = 4_999.00m },
            new InvoiceLine { Description = "USB-C Dock", Quantity = 1, UnitPrice = 8_752.50m }
        ]
    };

    private static void Main()
    {
        Banner("1. WHAT A SERIALIZER HAS TO FIGURE OUT");
        WhatSerializersNeed();

        Banner("2. A HAND-WRITTEN REFLECTION SERIALIZER (to remove the magic)");
        HandWrittenSerializer();

        Banner("3. System.Text.Json, REFLECTION MODE (the default)");
        ReflectionMode();

        Banner("4. ATTRIBUTES THAT STEER SERIALIZATION");
        SerializationAttributes();

        Banner("5. SOURCE-GENERATED MODE (compile-time metaprogramming)");
        SourceGeneratedMode();

        Banner("6. SAME INPUT, SAME OUTPUT, DIFFERENT MECHANISM");
        CompareModes();

        Banner("7. ROUND-TRIPPING, AND HOW DESERIALIZATION DIFFERS");
        Deserialization();

        Banner("8. WHICH MODE TO CHOOSE");
        Guidance();
    }

    // =====================================================================
    // 1. Frame the problem before showing any API.
    // =====================================================================
    private static void WhatSerializersNeed()
    {
        Console.WriteLine("To turn an arbitrary object into JSON, a library must answer, at run time:");
        Console.WriteLine("  - which members should be written?");
        Console.WriteLine("  - what name should each one have in the output?");
        Console.WriteLine("  - what is each member's type, so it knows how to write the value?");
        Console.WriteLine("  - how do I READ each value out of the object?");
        Console.WriteLine("  - on the way back: which constructor or setters do I use?");
        Console.WriteLine();
        Console.WriteLine("Every one of those answers is in metadata, so the Part 2 API is enough:");

        Type t = typeof(Invoice);
        foreach (PropertyInfo p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var nameAttribute = p.GetCustomAttribute<JsonPropertyNameAttribute>();
            bool ignored = p.GetCustomAttribute<JsonIgnoreAttribute>() is not null;

            string note = ignored ? "  [JsonIgnore]"
                : nameAttribute is not null ? "  -> \"" + nameAttribute.Name + "\""
                : string.Empty;

            Console.WriteLine($"  {p.PropertyType.Name,-18} {p.Name,-14}{note}");
        }

        Console.WriteLine();
        Console.WriteLine("This is why serialization is the canonical metaprogramming example: the library");
        Console.WriteLine("author cannot know your types, so the library must ask the types themselves.");
    }

    // =====================================================================
    // 2. Twenty lines that do the core job.
    // =====================================================================
    private static void HandWrittenSerializer()
    {
        Console.WriteLine("Output of MiniJson.Serialize (see the bottom of this file):");
        Console.WriteLine();
        Console.WriteLine(MiniJson.Serialize(SampleInvoice));

        Console.WriteLine();
        Console.WriteLine("MiniJson is reflection plus string building, and nothing else:");
        Console.WriteLine("  GetProperties() -> for each, GetValue() -> format by type -> write a pair.");
        Console.WriteLine();
        Console.WriteLine("It also shows what a real serializer adds on top: escaping, cycles, numbers,");
        Console.WriteLine("culture, nulls, polymorphism, dictionaries, streaming, async, and speed.");
        Console.WriteLine("Use System.Text.Json. Write MiniJson once, to understand it.");
    }

    // =====================================================================
    // 3. The default path.
    // =====================================================================
    private static void ReflectionMode()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string json = JsonSerializer.Serialize(SampleInvoice, options);
        Console.WriteLine(json);

        Console.WriteLine();
        Console.WriteLine("What happened on the first call for this type:");
        Console.WriteLine("  1. System.Text.Json reflected over Invoice and built a JsonTypeInfo for it.");
        Console.WriteLine("  2. It created fast member accessors - on a JIT runtime it generates them,");
        Console.WriteLine("     rather than calling PropertyInfo.GetValue per property per object.");
        Console.WriteLine("  3. It CACHED that plan on the JsonSerializerOptions instance.");
        Console.WriteLine();
        Console.WriteLine("Hence the standard advice: CREATE JsonSerializerOptions ONCE AND REUSE IT.");
        Console.WriteLine("The usual explanation - 'otherwise it re-reflects every call' - turns out to be");
        Console.WriteLine("only half right on current .NET. Let us measure instead of asserting.");

        const int iterations = 5_000;

        var reusedOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Warm the cache so the first-call cost is not counted.
        _ = JsonSerializer.Serialize(SampleInvoice, reusedOptions);

        // (a) The recommended pattern: one options instance, reused.
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = JsonSerializer.Serialize(SampleInvoice, reusedOptions);
        }

        long reused = sw.ElapsedMilliseconds;

        // (b) A new options object each call, but always with the SAME settings.
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _ = JsonSerializer.Serialize(SampleInvoice,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        long newButEqual = sw.ElapsedMilliseconds;

        // (c) A new options object each call with DIFFERENT settings, so no two are equal.
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _ = JsonSerializer.Serialize(SampleInvoice,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    MaxDepth = 16 + (i % 64)
                });
        }

        long newAndDistinct = sw.ElapsedMilliseconds;

        Console.WriteLine();
        Console.WriteLine("  " + iterations.ToString("N0") + " serializations of the same object:");
        Console.WriteLine($"    one reused options instance      {reused,6} ms   1.0x");
        Console.WriteLine($"    new options, identical settings  {newButEqual,6} ms   "
            + (newButEqual / Math.Max(1.0, reused)).ToString("N1") + "x");
        Console.WriteLine($"    new options, DIFFERENT settings  {newAndDistinct,6} ms   "
            + (newAndDistinct / Math.Max(1.0, reused)).ToString("N1") + "x");

        Console.WriteLine();
        Console.WriteLine("What the three rows tell us:");
        Console.WriteLine("  - Reusing one instance is fastest, as expected.");
        Console.WriteLine("  - New-but-identical options is only moderately slower. System.Text.Json keeps");
        Console.WriteLine("    a process-wide cache of serialization metadata that EQUIVALENT options");
        Console.WriteLine("    instances share, so the expensive reflection is not repeated. You still pay");
        Console.WriteLine("    for the allocation and the cache lookup.");
        Console.WriteLine("  - New-and-DIFFERENT options is dramatically slower. Nothing can be shared, so");
        Console.WriteLine("    the type really is analysed again for every distinct configuration.");
        Console.WriteLine();
        Console.WriteLine("So the correct statement is: reuse your options instance, and above all do not");
        Console.WriteLine("vary its settings per call. The second mistake is the expensive one.");
    }

    // =====================================================================
    // 4. Attributes as the declarative control surface.
    // =====================================================================
    private static void SerializationAttributes()
    {
        Console.WriteLine("The attributes used on Invoice and what each one did:");
        Console.WriteLine();
        Console.WriteLine("  [JsonPropertyName(\"invoice_id\")]  renamed InvoiceId in the output");
        Console.WriteLine("  [JsonIgnore]                       dropped InternalNotes entirely");
        Console.WriteLine("  [JsonPropertyOrder(-1)]            moved CustomerName to the front");
        Console.WriteLine("  [JsonConverter(...)]               took over DateOnly formatting");
        Console.WriteLine("  [JsonStringEnumConverter]          wrote the enum as text, not a number");
        Console.WriteLine();

        // Serialize WITHOUT a naming policy so the attribute names show through clearly.
        string json = JsonSerializer.Serialize(SampleInvoice, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);

        Console.WriteLine();
        Console.WriteLine("Note the precedence: [JsonPropertyName] beats PropertyNamingPolicy.");
        Console.WriteLine("An explicit attribute always wins over a global convention - the same");
        Console.WriteLine("'explicit metadata overrides implicit metadata' rule from Part 1.");
        Console.WriteLine();
        Console.WriteLine("Note also InternalNotes: the value exists on the object but never reaches");
        Console.WriteLine("the JSON. That is a one-attribute way to keep internal data out of an API");
        Console.WriteLine("response, and it is enforced by the serializer, not by the caller.");
    }

    // =====================================================================
    // 5. The source-generated path - compile-time metaprogramming.
    // =====================================================================
    private static void SourceGeneratedMode()
    {
        Console.WriteLine("InvoiceJsonContext (in JsonContext.cs) is declared like this:");
        Console.WriteLine();
        Console.WriteLine("  [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]");
        Console.WriteLine("  [JsonSerializable(typeof(Invoice))]");
        Console.WriteLine("  internal partial class InvoiceJsonContext : JsonSerializerContext;");
        Console.WriteLine();
        Console.WriteLine("That partial class has NO body that we wrote. A Roslyn source generator");
        Console.WriteLine("filled it in during the build. Proof, read from the compiled assembly:");
        Console.WriteLine();

        Type contextType = typeof(InvoiceJsonContext);
        Console.WriteLine("  type     : " + contextType.FullName);
        Console.WriteLine("  base     : " + contextType.BaseType?.Name);

        // These members were written by the generator, not by us.
        PropertyInfo[] generatedProperties = contextType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Console.WriteLine("  generated properties:");
        foreach (PropertyInfo p in generatedProperties)
        {
            Console.WriteLine("    " + p.PropertyType.Name + " " + p.Name);
        }

        Console.WriteLine();
        Console.WriteLine("Serializing through the generated metadata:");
        Console.WriteLine();

        // Note the overload: it takes a JsonTypeInfo<T>, not JsonSerializerOptions.
        // That is the whole point - the plan is a compile-time artifact, passed in explicitly.
        string json = JsonSerializer.Serialize(SampleInvoice, InvoiceJsonContext.Default.Invoice);
        Console.WriteLine(json);

        Console.WriteLine();
        Console.WriteLine("What is different about this call:");
        Console.WriteLine("  - No type was reflected over at run time. The read plan was compiled in.");
        Console.WriteLine("  - The generated code calls the property getters DIRECTLY, by name.");
        Console.WriteLine("  - Because nothing is discovered dynamically, the trimmer can see everything,");
        Console.WriteLine("    so this works under trimming and Native AOT where reflection mode warns or fails.");
        Console.WriteLine("  - Startup is faster: no first-call reflection cost for this type.");
    }

    // =====================================================================
    // 6. The two mechanisms, side by side.
    // =====================================================================
    private static void CompareModes()
    {
        var reflectionOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string viaReflection = JsonSerializer.Serialize(SampleInvoice, reflectionOptions);
        string viaSourceGen = JsonSerializer.Serialize(SampleInvoice, InvoiceJsonContextCompact.Default.Invoice);

        Console.WriteLine("Reflection mode output:");
        Console.WriteLine("  " + viaReflection);
        Console.WriteLine();
        Console.WriteLine("Source-generated output:");
        Console.WriteLine("  " + viaSourceGen);
        Console.WriteLine();
        Console.WriteLine("Byte-for-byte identical: " + string.Equals(viaReflection, viaSourceGen, StringComparison.Ordinal));

        Console.WriteLine();
        Console.WriteLine("  aspect              reflection mode              source-generated mode");
        Console.WriteLine("  -----------------------------------------------------------------------------");
        Console.WriteLine("  when the plan is    first call, at run time      during the build");
        Console.WriteLine("    built");
        Console.WriteLine("  mechanism           System.Reflection, then      Roslyn generator emits C#");
        Console.WriteLine("                      generated accessors");
        Console.WriteLine("  startup cost        pay on first use per type    none");
        Console.WriteLine("  trimming            unsafe: members may be       safe: all usage is visible");
        Console.WriteLine("                      trimmed away");
        Console.WriteLine("  Native AOT          not supported               supported");
        Console.WriteLine("  new type support    automatic                   add [JsonSerializable] and rebuild");
        Console.WriteLine("  polymorphism, dy-   more flexible                more restricted");
        Console.WriteLine("    namic shapes");
        Console.WriteLine();
        Console.WriteLine("This is the Part 1 slide made concrete. Same goal, same output, and the only");
        Console.WriteLine("real difference is WHEN the metaprogramming happened: build time or run time.");
    }

    // =====================================================================
    // 7. Deserialization is the harder direction.
    // =====================================================================
    private static void Deserialization()
    {
        string json = JsonSerializer.Serialize(SampleInvoice, InvoiceJsonContext.Default.Invoice);

        Invoice? roundTripped = JsonSerializer.Deserialize(json, InvoiceJsonContext.Default.Invoice);

        Console.WriteLine("Round-tripped through JSON and back:");
        Console.WriteLine("  InvoiceId     : " + roundTripped?.InvoiceId);
        Console.WriteLine("  CustomerName  : " + roundTripped?.CustomerName);
        Console.WriteLine("  IssuedOn      : " + roundTripped?.IssuedOn);
        Console.WriteLine("  AmountDue     : " + roundTripped?.AmountDue);
        Console.WriteLine("  Status        : " + roundTripped?.Status);
        Console.WriteLine("  Lines         : " + roundTripped?.Lines.Count + " line(s)");
        Console.WriteLine("  Total         : " + roundTripped?.Lines.Sum(l => l.Quantity * l.UnitPrice).ToString("N2"));

        Console.WriteLine();
        Console.WriteLine("  InternalNotes : " + (roundTripped?.InternalNotes ?? "(null)")
            + "  <- [JsonIgnore] means it was never written, so it cannot come back");

        Console.WriteLine();
        Console.WriteLine("Why deserialization is harder than serialization:");
        Console.WriteLine("  - Writing only needs a getter. Reading needs to CREATE the object first.");
        Console.WriteLine("  - It must choose a constructor, then match JSON names to parameters.");
        Console.WriteLine("  - For init-only or readonly members it has to use the constructor, not setters.");
        Console.WriteLine("  - Missing members, extra members and nulls all need a policy.");
        Console.WriteLine();
        Console.WriteLine("This is the same constructor-selection problem the DI container solves in");
        Console.WriteLine("example 07, and the same one Activator.CreateInstance faces in example 02.");
        Console.WriteLine("Three unrelated-looking features, one underlying metadata question.");

        Console.WriteLine();
        Console.WriteLine("Malformed input is a runtime error, as always with metadata-driven code.");
        Console.WriteLine("Note the property name has to be the one the metadata declares - \"invoice_id\",");
        Console.WriteLine("not \"invoiceId\" - because [JsonPropertyName] renamed it:");
        try
        {
            _ = JsonSerializer.Deserialize("{ \"invoice_id\": \"not-a-number\" }", InvoiceJsonContext.Default.Invoice);
            Console.WriteLine("  (no exception - unexpected)");
        }
        catch (JsonException ex)
        {
            Console.WriteLine("  JsonException: " + ex.Message.Split(" Path")[0]);
        }

        Console.WriteLine();
        Console.WriteLine("And an UNKNOWN property is silently ignored by default, which surprises people:");
        Invoice? withUnknown = JsonSerializer.Deserialize(
            "{ \"invoice_id\": 99, \"totallyUnknownProperty\": 123 }", InvoiceJsonContext.Default.Invoice);
        Console.WriteLine("  deserialized InvoiceId = " + withUnknown?.InvoiceId + ", unknown member discarded");
        Console.WriteLine("  set JsonSerializerOptions.UnmappedMemberHandling = Disallow to make that an error.");
    }

    // =====================================================================
    // 8. Guidance.
    // =====================================================================
    private static void Guidance()
    {
        Console.WriteLine("Use SOURCE GENERATION when:");
        Console.WriteLine("  - you are publishing Native AOT or trimmed (a Blazor WASM or container image);");
        Console.WriteLine("  - startup time matters (serverless, CLI tools);");
        Console.WriteLine("  - the set of serialized types is known at build time, which is the usual case.");
        Console.WriteLine();
        Console.WriteLine("Use REFLECTION MODE when:");
        Console.WriteLine("  - the types are genuinely not known until run time (a plugin's own models);");
        Console.WriteLine("  - you need the more dynamic features, such as broad polymorphic support;");
        Console.WriteLine("  - you are prototyping and do not want the extra declaration.");
        Console.WriteLine();
        Console.WriteLine("In both modes, the single highest-value habit: ONE cached JsonSerializerOptions");
        Console.WriteLine("(or one context) for the lifetime of the application. Section 3 measured why.");
        Console.WriteLine();
        Console.WriteLine("Where else this exact pattern appears in .NET:");
        Console.WriteLine("  - the regex source generator vs RegexOptions.Compiled");
        Console.WriteLine("  - the logging source generator ([LoggerMessage]) vs reflection-based formatting");
        Console.WriteLine("  - the configuration binder source generator vs reflective binding");
        Console.WriteLine("  - minimal API request-delegate generation");
        Console.WriteLine();
        Console.WriteLine("The direction of travel in .NET is clear: move metaprogramming from run time to");
        Console.WriteLine("build time where the shape is statically known, and keep reflection for the");
        Console.WriteLine("cases where it genuinely is not.");
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
// The model. Attributes here are "explicit metadata" from Part 1.
// =========================================================================

internal sealed class Invoice
{
    [JsonPropertyName("invoice_id")]
    public int InvoiceId { get; set; }

    [JsonPropertyOrder(-1)]
    public string CustomerName { get; set; } = string.Empty;

    [JsonConverter(typeof(IsoDateOnlyConverter))]
    public DateOnly IssuedOn { get; set; }

    public decimal AmountDue { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<InvoiceStatus>))]
    public InvoiceStatus Status { get; set; }

    public List<InvoiceLine> Lines { get; set; } = [];

    /// <summary>Present on the object, deliberately absent from the JSON.</summary>
    [JsonIgnore]
    public string? InternalNotes { get; set; }
}

internal sealed class InvoiceLine
{
    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}

internal enum InvoiceStatus
{
    Draft,
    AwaitingPayment,
    Paid,
    Cancelled
}

/// <summary>A custom converter, to show that formatting can be taken over per member.</summary>
internal sealed class IsoDateOnlyConverter : JsonConverter<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateOnly.ParseExact(reader.GetString()!, Format);

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format));
}

// =========================================================================
// A deliberately naive serializer, so the audience sees there is no magic.
// =========================================================================

internal static class MiniJson
{
    public static string Serialize(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var builder = new StringBuilder();
        WriteValue(builder, instance);
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;

            case string s:
                // Minimal escaping only - a real serializer handles far more.
                builder.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                return;

            case bool b:
                builder.Append(b ? "true" : "false");
                return;

            case int or long or decimal or double or float:
                builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                return;

            case Enum e:
                builder.Append('"').Append(e).Append('"');
                return;

            case DateOnly d:
                builder.Append('"').Append(d.ToString("yyyy-MM-dd")).Append('"');
                return;

            case System.Collections.IEnumerable list:
                builder.Append('[');
                bool firstItem = true;
                foreach (object? item in list)
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    WriteValue(builder, item);
                    firstItem = false;
                }

                builder.Append(']');
                return;

            default:
                WriteObject(builder, value);
                return;
        }
    }

    private static void WriteObject(StringBuilder builder, object instance)
    {
        // THE reflection step: ask the type what it is made of.
        PropertyInfo[] properties = instance.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        builder.Append('{');

        bool first = true;
        foreach (PropertyInfo property in properties)
        {
            if (!property.CanRead)
            {
                continue;
            }

            // Honour [JsonIgnore], exactly as the real serializer does.
            if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
            {
                continue;
            }

            // Honour [JsonPropertyName], else camel-case the CLR name.
            string name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? char.ToLowerInvariant(property.Name[0]) + property.Name[1..];

            if (!first)
            {
                builder.Append(',');
            }

            builder.Append('"').Append(name).Append("\":");
            WriteValue(builder, property.GetValue(instance));
            first = false;
        }

        builder.Append('}');
    }
}
