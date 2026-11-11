using System.Text.Json.Serialization;

namespace Metaprogramming.SerializationDemo;

/// <summary>
/// The source-generated serialization plan, indented for display.
/// <para>
/// We write only the declaration. At BUILD time the System.Text.Json Roslyn source
/// generator produces the other half of this partial class: a JsonTypeInfo for Invoice
/// (and transitively for InvoiceLine and InvoiceStatus) with hand-rolled, direct
/// property access instead of runtime reflection.
/// </para>
/// <para>
/// To see what was generated, add this to the .csproj and look under obj/:
///   &lt;EmitCompilerGeneratedFiles&gt;true&lt;/EmitCompilerGeneratedFiles&gt;
/// </para>
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Invoice))]
internal partial class InvoiceJsonContext : JsonSerializerContext;

/// <summary>
/// The same plan without indentation, so section 6 can compare its output byte-for-byte
/// against the reflection-based serializer's default (unindented) output.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Invoice))]
internal partial class InvoiceJsonContextCompact : JsonSerializerContext;
