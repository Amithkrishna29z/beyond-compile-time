# Part 1 — Understanding Metaprogramming

**Session time: 6 minutes (slides 3–7)**
**Runnable example: [`Code-Examples/01-Basics`](../Code-Examples/01-Basics)**

---

## Terminology first

Three words are used throughout this guide. Learn them here and the rest reads easily.

| Term | Meaning |
|---|---|
| **Metadata** | Data *about* your program — the names, types and signatures of everything you declared. The C# compiler writes it into your `.dll` alongside the executable code. |
| **Introspection** | *Reading* metadata while the program runs ("what properties does this type have?"). |
| **Code generation** | *Creating* new executable code while the program runs. |

"Meta-" means "about". **Metaprogramming is programming about programs.** Reading
metadata and generating code are its two halves, and they map onto Parts 2 and 3 of
this session.

---

# 1. Normal Programming vs Metaprogramming

## 1.1 Definition

**Normal programming** writes code that operates on **data** — numbers, strings,
records, rows. The *structure* of the program is fixed when you compile it, and
knowledge of that structure lives in the developer's head.

**Metaprogramming** writes code that operates on **the program itself** — its types,
members, signatures and behaviour. The program holds knowledge about its own
structure and can act on it.

## 1.2 In simple English

In normal programming you tell the computer *"take this customer's email and send a
message to it."*

In metaprogramming you tell the computer *"look at whatever object I just handed you,
find out what properties it has, and print each one."*

The second instruction does not mention customers or emails. It works on objects whose
types you had never seen when you wrote it — including types written next year.

## 1.3 Why the concept exists

Because **library authors cannot know their users' types.**

The author of a JSON serializer, a DI container, a unit-test runner or an ORM is
writing code in 2019 that must work with a class you will write in 2027. There are
only two options:

1. Make every user hand-write adapter code for every one of their types; or
2. Write code that *asks each type about itself* at run time.

Option 2 is metaprogramming, and it is why `JsonSerializer.Serialize(anything)` works.

## 1.4 How it works internally

When `csc` compiles your C#, it produces a `.dll` containing **two** things:

```text
    MyApp.dll
    ├── IL (Intermediate Language)  ← the executable instructions
    └── METADATA TABLES             ← the description of everything you declared
        ├── TypeDef      : every type you declared
        ├── MethodDef    : every method, with its signature
        ├── Field        : every field
        ├── Property     : every property
        ├── Param        : every parameter, with its name
        ├── CustomAttribute : every attribute you applied
        └── (and about 40 more)
```

That metadata is not debug information and it is not optional. It is a required part
of every managed assembly — the CLR needs it to do its own job (resolving calls,
verifying types, garbage-collecting correctly). Reflection simply **reads the tables
that are already there.**

This is the single most important mental model in the whole session:

> **A .NET assembly is self-describing. Metaprogramming is reading the description.**

## 1.5 Important APIs

| API | Purpose |
|---|---|
| `object.GetType()` | The runtime type of an actual object |
| `typeof(T)` | The `Type` for a compile-time-known type |
| `Type.GetProperties()` | The properties a type declares |
| `PropertyInfo.GetValue(obj)` | Read a property's value from an instance |
| `Type.GetType("Name")` | Find a type from a string, at run time |

## 1.6 Syntax

```csharp
// Normal: the property is named in the source.
string email = customer.Email;

// Metaprogramming: the property is discovered at run time.
PropertyInfo property = customer.GetType().GetProperty("Email")!;
object? email2 = property.GetValue(customer);
```

## 1.7 Complete example

From [`01-Basics/Program.cs`](../Code-Examples/01-Basics/Program.cs). The hand-written
version first:

```csharp
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
    }
}
```

And the metaprogramming version that replaces **all** of it:

```csharp
internal static class Reflective
{
    public static IEnumerable<string> Describe(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        Type type = instance.GetType();

        yield return "-- " + type.Name + " (" + type.GetProperties().Length + " public properties)";

        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            object? value = property.GetValue(instance);
            yield return type.Name + "." + property.Name + " = " + value;
        }
    }
}
```

## 1.8 Line-by-line

| Line | What happens |
|---|---|
| `ArgumentNullException.ThrowIfNull(instance)` | Guard. Reflection on `null` gives a `NullReferenceException` deep inside the method otherwise. |
| `Type type = instance.GetType()` | Gets the **runtime** type. If the variable is declared `object` but holds a `SavingsAccount`, this returns `SavingsAccount`. This is the difference between `GetType()` and `typeof(T)`. |
| `type.GetProperties()` | Reads the `Property` metadata table for this type and returns one `PropertyInfo` per property. |
| `BindingFlags.Public \| BindingFlags.Instance` | Restricts the result to public, non-static properties. Omitting the flags means `Public \| Instance \| Static`. |
| `if (!property.CanRead)` | A write-only property has no getter; calling `GetValue` on it throws. |
| `property.GetValue(instance)` | Invokes the property's `get` accessor through the runtime and boxes the result into `object`. |

## 1.9 Expected output

Verified by running the example (`Code-Examples/expected-output/01-Basics.txt`):

```text
One reflection-based describer, used for three unrelated types:
  -- Customer (4 public properties)
  Customer.Id = 7
  Customer.FullName = Asha Menon
  Customer.Email = asha@example.com
  Customer.IsActive = True

  -- Product (3 public properties)
  Product.Sku = 101
  Product.Name = Mechanical Keyboard
  Product.Price = 4999.00

  -- Order (3 public properties)
  Order.OrderId = 5001
  Order.PlacedOn = 11/10/2026
  Order.LineCount = 2
```

One method, three unrelated types, and the method's source mentions none of them.

## 1.10 Real-world applications

Every one of these is `GetProperties()` plus a loop, at heart:

- `System.Text.Json` — walks your type's properties to write JSON.
- EF Core — walks them to build a table schema.
- AutoMapper — matches source properties to destination properties by name.
- `[Required]` validation in ASP.NET Core — walks properties looking for attributes.
- xUnit / NUnit — walks assemblies looking for `[Fact]` / `[Test]` methods.
- `ToString()` on a `record` — the compiler generates it, but conceptually the same job.

## 1.11 Advantages

- **One implementation serves unlimited types**, including types that do not exist yet.
- **Less code**, therefore fewer places for a bug to hide.
- **No drift.** A hand-written describer silently goes stale when a property is added;
  the reflective one picks it up automatically.
- **Enables whole categories of library** that are otherwise impossible.

## 1.12 Limitations

- **No compile-time safety.** `GetProperty("Emial")` compiles perfectly and returns
  `null` at run time.
- **Slower.** Measured in Part 3: a reflective property read is roughly 7–8× a direct
  one on the machine that produced this package.
- **Opaque.** "Go to definition" leads nowhere useful; stack traces go through
  `Invoke`.
- **Hostile to trimming and Native AOT.** The trimmer cannot see reflective usage, so
  it may remove members your reflection needs. Covered in Part 3.
- **Can leak internals.** `BindingFlags.NonPublic` reads private state, so reflective
  code can couple itself to things that were never part of the public contract.

## 1.13 Performance and security

**Performance.** The cost is *per call*, not per program. The fix is always the same:
do the reflection **once**, cache the result, and execute cached/compiled code
thereafter. Every framework in Part 4 does exactly this.

**Security.** `private` is a **compiler** rule, not a runtime permission.
`BindingFlags.NonPublic` reads private fields, and it is allowed. Accessibility is
**encapsulation**, not a **security boundary**. Never rely on `private` to protect a
secret from code running in the same process.

## 1.14 Common mistakes

1. **Using reflection when generics would do.** If the type is known at compile time,
   `T` is faster, safer and clearer.
2. **Calling `GetProperties()` inside a loop.** It allocates an array every time.
   Hoist it out and cache it.
3. **Reflecting to avoid typing.** Reflection is for when the types are *unknown*, not
   when they are merely *tedious*.
4. **Forgetting `BindingFlags`** and then being surprised that a private or static
   member is missing.
5. **Assuming `GetProperties()` returns a stable order.** It does not guarantee one;
   sort explicitly if order matters.

## 1.15 Audience questions

**Q: Is this the same as `dynamic`?**
No. `dynamic` defers *binding* of an expression to run time but you still write the
member name in source (`obj.Email`). Reflection lets the *name itself* be data (a
string, a config value, a database row). Also, `dynamic` relies on the much heavier
DLR; reflection is the lower-level primitive.

**Q: Is metaprogramming the same thing as reflection?**
Reflection is one *kind* of metaprogramming — the read-only, run-time kind. Part 3
covers run-time code *generation*, and source generators do it at compile time.
Reflection is the most common form, not the whole category.

**Q: Should I use this in application code, or only in libraries?**
Mostly libraries and infrastructure. In application code the types are usually known,
so generics and plain C# are better. The honest exceptions are plugin systems and
genuinely data-driven features (a report builder whose columns come from config).

## 1.16 Documentation

- Reflection and attributes (C# guide) — <https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/reflection-and-attributes/>
- Reflection in .NET — <https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/reflection>
- Managed code and the CLR — <https://learn.microsoft.com/en-us/dotnet/standard/managed-code>

## 1.17 Comparison table

| | Normal programming | Metaprogramming |
|---|---|---|
| Operates on | values and data | types, members, behaviour |
| Structure known | when you write the code | only when the program runs |
| Checked by | the compiler | nothing — you find out at run time |
| Typical speed | full | slower per call, unless cached |
| Typical author | application developer | library / framework developer |
| Failure mode | build error | runtime exception, or silent wrong behaviour |
| Example | `customer.Email` | `type.GetProperty("Email")!.GetValue(c)` |
| Debuggability | step straight in | opaque frames through `Invoke` |
| AOT / trimming | always fine | needs care, sometimes impossible |

**Why metaprogramming is called a "higher-level" technique:** not because it is more
advanced in the sense of better, but because it sits one level *above* ordinary code —
its input is the program. Normal code reasons about customers; metaprogramming code
reasons about the *idea* of a customer type.

---

# 2. Why Do We Need Metaprogramming?

## 2.1 Definition

The need arises whenever the **set of types a piece of code must handle is not known
when that code is written.**

## 2.2 The problem, demonstrated first

Say you must log every property of every domain object for an audit trail. There are
40 domain types.

**Without metaprogramming** you write 40 methods:

```csharp
void Audit(Customer c) { /* 4 lines */ }
void Audit(Product p)  { /* 3 lines */ }
void Audit(Order o)    { /* 3 lines */ }
// ... 37 more
```

Count the costs:

| Cost | Detail |
|---|---|
| **Volume** | 40 methods, ~150 lines of pure mechanism |
| **Drift** | Add `Product.Weight` and the audit log is silently wrong. The compiler does **not** warn you. |
| **Scaling** | Every new type needs a new method. 41st type, 41st method. |
| **Review burden** | 150 lines of near-identical code hide the one that is wrong |
| **Impossible case** | A type from a plugin DLL loaded at run time — you *cannot* write its method in advance |

That last row is the decisive one. Repetition is annoying; **impossibility** is a hard
block.

## 2.3 The metaprogramming solution

One method, from §1.7 above, handles all 40 types and every future one. The output is
in §1.9.

## 2.4 When the problem is NOT this

Be fair about the alternatives. Repetition alone does not justify reflection:

| Situation | Better tool than reflection |
|---|---|
| Types known at compile time, behaviour identical | **Generics** — `Audit<T>(T item)` with a constraint |
| Types known, behaviour differs slightly | **An interface** — `IAuditable.Describe()` |
| Boilerplate known at build time | **A source generator** — zero run-time cost |
| A fixed, small set of types | **Just write the 3 methods.** Honestly. |

Reflection earns its place at the point where the type set is **open**.

## 2.5 Real-world applications

- A plugin host loading DLLs discovered on disk (example 04).
- A serializer handling your types and your users' types (example 09).
- A DI container constructing types it has never seen (example 07).
- A test runner finding tests in any assembly.
- A report engine whose columns come from a database table.

## 2.6 Common mistakes

Using reflection to work around a design problem. If you find yourself reflecting over
your *own* types inside your *own* application, the usual root cause is a missing
interface or a missing generic parameter. Fix the design instead.

## 2.7 Audience questions

**Q: Could a source generator solve the 40-types problem instead?**
Yes, and better — *if* all 40 types are in your solution at build time. It would
generate the 40 methods for you, with zero run-time cost and full trimming support.
Source generators do not help with the plugin case, because that type does not exist at
build time. See §4.

**Q: We have 40 types but they are all ours. Which should we pick?**
Source generator or generics. Reflection is the answer only for the open-set case.
Choosing reflection for a closed set means paying the costs in §1.12 for benefits you
do not need.

**Q: Isn't 150 lines of boring code fine? It is explicit and fast.**
It is a real position and sometimes the right one — explicit code is debuggable and the
trimmer loves it. The decisive question is not volume but **drift**: can the code go
silently wrong when someone adds a property? If yes, mechanism should be derived, not
written by hand.

---

# 3. Developer Benefits

Four benefits, each stated on its own, with the counterweight.

## 3.1 Automation

**Definition.** Deriving mechanism from declarations instead of writing it.

**Example.** ASP.NET Core model validation. You declare:

```csharp
public sealed class CreateReportRequest
{
    [Required]
    [StringLength(60, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Owner { get; set; } = string.Empty;
}
```

You write **no validation code at all**. Example 10 POSTs an invalid body and the
framework answers:

```text
POST /api/reports   body: {"title":"x","owner":"not-an-email"}
  -> 400 Bad Request
  -> {"title":"One or more validation errors occurred.","status":400,
      "errors":{"Owner":["The Owner field is not a valid e-mail address."],
                "Title":["The field Title must be a string with a minimum length of 3
                          and a maximum length of 60."]}}
```

**Advantage.** The rule is declared once, next to the thing it constrains, and cannot
be forgotten at a call site.

**Limitation.** The rules are limited to what the attributes can express. Anything
conditional ("required only when Status is Draft") escapes the model and needs real
code.

## 3.2 Reducing boilerplate

**Definition.** Removing code whose content is fully implied by a declaration.

**Example.** `[JsonPropertyName]` and `[JsonIgnore]` (example 09) replace a
hand-written DTO-mapping layer. One attribute keeps a field out of an API response:

```csharp
[JsonIgnore]
public string? InternalNotes { get; set; }
```

**Advantage.** Less code, and the intent is visible on the member itself.

**Limitation.** Boilerplate is *explicit*. Replacing it moves the logic somewhere
invisible. A new team member can read a hand-written mapper; they cannot read an
attribute's effect without knowing the framework.

## 3.3 Maintainability

**Definition.** Change costs stay proportional to the change, not to the codebase.

**Example.** Example 03's validator:

```csharp
foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    var range = property.GetCustomAttribute<ValidateRangeAttribute>();
    if (range is not null && value is not null) { /* check it */ }
}
```

Add a 41st annotated class and the validator needs no edit. Output:

```text
INVALID payment: Amount=-5 CurrencyCode="RUPEES" Reference=""
  ERROR: Amount must be between 0.01 and 1000000 (was -5)
  ERROR: CurrencyCode must be exactly 3 characters (was 6)
  ERROR: Reference is required
```

**Advantage.** One place to fix a bug in the mechanism; N places become one.

**Limitation.** When the generic mechanism *is* the bug, debugging is harder. And
because nothing references the attribute explicitly, "find all usages" tells you
nothing.

## 3.4 Extensibility

**Definition.** Third parties can add behaviour without modifying — or rebuilding —
your code.

**Example.** Example 04's host:

```text
Loaded PluginDiscovery.Plugins.Sales v1.0.0.0
  into load context: PluginALC:PluginDiscovery.Plugins.Sales
  accepted MonthlySalesPlugin as "Monthly Sales Summary" v2.1.0
  accepted TopCustomersPlugin as "Top Customers" v2.1.0
  skipped ExperimentalForecastPlugin - [ReportPlugin(Enabled = false)]
  skipped UnannotatedHelperPlugin - implements IReportPlugin but has no [ReportPlugin]
```

The host's source names no plugin class. Drop a DLL in a folder, get a feature.

**Advantage.** This is the only one of the four that is *qualitative*. The others make
existing work cheaper; this one makes previously impossible things possible.

**Limitation.** You have just created a plugin API, with everything that implies:
versioning, trust, failure isolation. And `AssemblyLoadContext` **is not a security
sandbox** — see Part 2 §8.

## 3.5 When metaprogramming increases complexity instead of reducing it

Be honest about this during the talk; it buys credibility for everything else.

Reach for something simpler when:

1. **The type set is closed.** Generics or a source generator win outright.
2. **It is called once.** Generation and JIT cost more than the reflection saved.
3. **The abstraction has one implementation.** A reflective "engine" with exactly one
   caller is a hand-written method with extra steps.
4. **The team will not maintain it.** IL and visitor chains are write-mostly. A clever
   50-line emitter that nobody dares touch is worse than 200 obvious lines.
5. **You are shipping Native AOT.** Much of Part 3 simply does not run there.
6. **The failure mode matters more than the convenience.** A compile error at 3pm
   beats a `NullReferenceException` at 3am.

A reasonable rule:

> **Metaprogramming trades compile-time safety for run-time flexibility.**
> Make that trade when you genuinely need the flexibility — and not one step sooner.

## 3.6 Audience questions

**Q: Our team banned reflection for performance reasons. Reasonable?**
Partly. Banning *per-call* reflection on a hot path is sound. Banning it at *startup*
is not — you would have to ban ASP.NET Core, EF Core and the DI container, all of which
reflect at startup and then execute cached code. The useful rule is "no reflection per
request", not "no reflection".

**Q: How do I know whether I have gone too far?**
Two tests. Could a competent colleague change the behaviour safely without you
explaining it? And does a typo in a *string* become a production incident rather than a
build failure? Two yeses mean you have gone too far.

**Q: Which of the four benefits matters most?**
Extensibility, because it is the only one you cannot get another way. Automation and
reduced boilerplate are better served by source generators when the shape is known at
build time.

---

# 4. Compile-Time vs Runtime Metaprogramming

## 4.1 Definition

Metaprogramming happens at one of three **stages**, and confusing them is the most
common conceptual error in this subject.

| Stage | When | Tool | Produces |
|---|---|---|---|
| **Compile-time generation** | during the build | **Roslyn source generators** | new **C# source**, compiled with yours |
| **Runtime introspection** | while running | **Reflection** | *information* about existing code |
| **Runtime generation** | while running | **Reflection.Emit**, expression trees | new **IL**, in memory |

## 4.2 In simple English

- A **source generator** is a colleague who writes extra code for you just before the
  compiler runs. By the time the program starts, that code is ordinary compiled C#.
- **Reflection** is reading the table of contents of a finished book.
- **Reflection.Emit** is writing a new chapter while the book is being read.

## 4.3 Why three stages exist

Because the information needed arrives at different times.

- If the shape is known at **build time** → generate then. Zero run-time cost, full
  type checking, trimming-safe. Always prefer this.
- If the shape is known only at **run time** (a plugin, a config file, a user query)
  → build time cannot help you.

## 4.4 How each works internally

**Source generators.** A generator is an analyzer implementing `IIncrementalGenerator`.
Roslyn runs it during compilation, passing a model of your syntax and symbols. It adds
source files to the same compilation. The output is normal IL — the generator is not
present at run time and costs nothing then.

**Reflection.** Reads the metadata tables described in §1.4. Nothing is created; the
cost is lookups, allocations for the `*Info` objects, and per-call binding on `Invoke`.

**Runtime generation.** You build IL (or an expression tree, which is compiled to IL).
The JIT compiles it to machine code on first call. Requires a JIT, which is why it
fails under Native AOT.

## 4.5 Key differences

| | Source generators | Reflection | Reflection.Emit |
|---|---|---|---|
| Runs at | build time | run time | run time |
| Output | C# source | information | IL |
| Run-time cost | **zero** | per call | once, then ~direct |
| Type-checked | yes, by the compiler | no | no (and not always verified) |
| Native AOT | **yes** | partly | **no** |
| Trimming | safe | needs annotation | unsafe |
| Debuggable | yes — real source | frames via `Invoke` | poorly |
| Handles types unknown at build time | **no** | yes | yes |
| Startup cost | none | first-use reflection | generation + JIT |

## 4.6 The same job, both ways

Example 09 does this deliberately, and the outputs are **byte-for-byte identical**:

```csharp
// RUNTIME: reflects over Invoice on first use, then caches the plan.
string a = JsonSerializer.Serialize(invoice, options);

// COMPILE-TIME: the plan was generated during the build.
string b = JsonSerializer.Serialize(invoice, InvoiceJsonContext.Default.Invoice);
```

The source-generated context is declared with no body at all:

```csharp
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Invoice))]
internal partial class InvoiceJsonContext : JsonSerializerContext;
```

Verified output:

```text
Byte-for-byte identical: True
```

Same goal, same result. **The only difference is when the metaprogramming ran.**

## 4.7 Choosing

```text
Is the shape known at BUILD time?
├── YES → source generator            (zero runtime cost, AOT-safe)  ← prefer
└── NO  → is it a FUNCTION or a TYPE you need?
          ├── function → expression trees   (readable, type-checked as you build)
          └── type     → Reflection.Emit / DispatchProxy
          └── or just read metadata → Reflection
```

## 4.8 Real-world: .NET is moving run-time work to build time

| Feature | Runtime version | Build-time version |
|---|---|---|
| JSON | reflection-based | `JsonSerializerContext` source generator |
| Regex | `RegexOptions.Compiled` (emits IL) | `[GeneratedRegex]` source generator |
| Logging | reflective formatting | `[LoggerMessage]` source generator |
| Config binding | reflective binding | configuration binder generator |
| DI | reflection + emitted engine | compile-time container experiments |

The direction is consistent, and the motivation is Native AOT and startup time.

## 4.9 Common mistakes

1. **"Source generators replace reflection."** They replace it for *closed* type sets
   only. Plugins remain reflection's territory.
2. **Thinking a source generator runs at run time.** It does not exist at run time.
3. **Expecting `Reflection.Emit` to work under Native AOT.** It throws.
4. **Forgetting expression trees need `Compile()` — and `Compile()` needs a JIT.**
   Building and *reading* a tree works under AOT (which is why EF Core can translate
   queries there); compiling one does not.

## 4.10 Audience questions

**Q: Are source generators strictly better?**
Better where they apply, and they apply to most boilerplate. They cannot handle types
that do not exist at build time, cannot see run-time configuration, and they add build
time and a learning curve.

**Q: Do source generators slow my build?**
Yes, measurably but usually mildly. Incremental generators are designed to cache
aggressively. Trading build time for run-time cost and AOT compatibility is normally a
good trade.

**Q: Where do T4 templates and IL weaving (PostSharp, Fody) fit?**
T4 is an older compile-time text generator, pre-Roslyn and not integrated with the
compiler's semantic model. IL weavers run *after* compilation and rewrite the IL in your
assembly — a fourth stage, "post-compile", which this session does not cover but which
is how some AOP frameworks work.

## 4.11 Documentation

- Source generators overview — <https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview>
- `System.Text.Json` source generation — <https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation>
- Native AOT limitations — <https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/#limitations-of-native-aot-deployment>

---

# 5. Implicit vs Explicit Metadata

## 5.1 A necessary caveat first

**"Implicit" and "explicit" metadata are teaching labels, not CLR categories.**

The CLR has no such distinction. It has metadata tables, one of which is
`CustomAttribute`. Everything is simply *metadata*, read through the same API. The
split used here is about **who put it there**:

- **Implicit** — the compiler emitted it because you declared a type.
- **Explicit** — you deliberately attached it with an attribute.

Say this out loud when presenting. It is a genuinely useful distinction *and* it is not
a formal one, and conflating the two confuses people later when they read the ECMA-335
spec and find no "implicit metadata" table.

## 5.2 Implicit metadata

You wrote no metadata here, yet all of this is queryable:

```csharp
internal sealed record Customer(int Id, string FullName, string Email, bool IsActive);
```

Verified output:

```text
IMPLICIT metadata - emitted by the compiler purely because you declared the type:
  Id        : Int32    (CanRead=True, CanWrite=True)
  FullName  : String   (CanRead=True, CanWrite=True)
  Email     : String   (CanRead=True, CanWrite=True)
  IsActive  : Boolean  (CanRead=True, CanWrite=True)
```

Names, types, accessibility, arity, parameter names, base types, interfaces — all
emitted because the compiler must.

## 5.3 Explicit metadata

```csharp
internal sealed record Customer(
    int Id,
    [property: Required] [property: StringLength(100)] string FullName,
    [property: Required] [property: EmailAddress] [property: JsonPropertyName("email_address")] string Email,
    bool IsActive);
```

Verified output:

```text
EXPLICIT metadata - attributes you deliberately attached:
  FullName  : RequiredAttribute, StringLengthAttribute
  Email     : RequiredAttribute, EmailAddressAttribute, JsonPropertyNameAttribute
```

## 5.4 Attributes as declarative metadata

An attribute does not *do* anything. It is **inert data** until something reads it.

```csharp
[JsonIgnore]
public string? InternalNotes { get; set; }
```

`[JsonIgnore]` has no behaviour. `System.Text.Json` looks for it and chooses to skip
the property. If nothing reads an attribute, it is a comment the compiler stores.

This is why attributes are called **declarative**: you declare *what is true*, and
separate machinery decides *what to do*.

## 5.5 Metadata tables

The compiler stores an applied attribute as a row in the `CustomAttribute` table
recording the attribute type and the constructor arguments. The attribute **object
does not exist** in the assembly — only the recipe for building one.

Which explains a result that surprises people (example 03):

```text
  ReferenceEquals(first, second) = False
```

Two calls to `GetCustomAttribute<TableAttribute>()` return **different objects**,
because each call runs the constructor again.

And it is why `GetCustomAttributesData()` exists — it reads the row *without*
constructing anything:

```text
GetCustomAttributesData() returns the raw metadata rows:
  NullableContextAttribute(1)
  NullableAttribute(0)
  TableAttribute(invoice_lines, Schema = sales)
  TagAttribute(billing)
  TagAttribute(generated-nightly)
  TagAttribute(pii-free)
```

Note the first two rows: `NullableContextAttribute` and `NullableAttribute` are
attributes **the compiler** applied, to record nullable reference annotations. A neat
illustration that the implicit/explicit line is about authorship, not mechanism —
these are compiler-authored *attributes*.

## 5.6 Important APIs

| API | Purpose |
|---|---|
| `GetCustomAttribute<T>()` | One attribute, **constructed**. Throws `AmbiguousMatchException` if several. |
| `GetCustomAttributes<T>()` | All of them, constructed. Use when `AllowMultiple = true`. |
| `GetCustomAttributesData()` | The raw rows, **nothing constructed** |
| `IsDefined(type)` | Cheapest existence check — no construction |

## 5.7 Advantages and limitations

**Implicit:** free, always present, impossible to forget, cannot express intent.
**Explicit:** expresses intent precisely, but must be remembered, can drift from
reality, and couples your domain type to a framework's attribute.

That last point drives a real design decision: EF Core offers data annotations **and**
a fluent API precisely so you can keep persistence attributes out of a domain class.
Part 4 §3 shows both.

## 5.8 Performance and security

**Performance.** `GetCustomAttributes` constructs objects on every call — do not put it
on a hot path. Cache the result, or use `IsDefined` / `GetCustomAttributesData` when
you only need to *know*, not to *have*.

**Security.** Reading attributes **runs their constructors**. For an assembly you do
not trust, that is executing its code. Use `GetCustomAttributesData()`, or
`MetadataLoadContext`, to inspect untrusted assemblies without running anything.

## 5.9 Common mistakes

1. **`GetCustomAttribute<T>()` (singular) on an `AllowMultiple = true` attribute** →
   `AmbiguousMatchException`. Example 03 triggers it on purpose.
2. **Forgetting `inherit: true`.** Both the attribute's `Inherited` setting *and* the
   call's `inherit` argument must agree. Example 03 shows all four combinations.
3. **Expecting attribute instances to be cached.** They are not.
4. **Putting logic in an attribute.** Attributes are data; the reader holds the
   behaviour.
5. **Omitting `[AttributeUsage]`.** The defaults (`All`, `AllowMultiple = false`,
   `Inherited = true`) are rarely what you meant.

## 5.10 Audience questions

**Q: Do unused attributes cost anything at run time?**
Essentially nothing. They add a few bytes of metadata and are never constructed unless
something asks. They are not free for *trimming*, though — an attribute referencing a
type can keep that type alive.

**Q: Can I add attributes at run time?**
Not to an existing type. Metadata of a loaded type is immutable. You can apply
attributes to types you **generate** (`TypeBuilder.SetCustomAttribute`, Part 3). For
"run-time configuration" the usual answers are a fluent API (as EF Core does) or a
`TypeDescriptionProvider`.

**Q: Attributes or a fluent API, for my own library?**
Offer both if you can. Attributes are discoverable and sit next to the member; a fluent
API keeps framework types out of domain classes, supports conditional configuration,
and can express things attributes cannot (attribute arguments must be compile-time
constants). EF Core's choice to support both is the reference example.

## 5.11 Documentation

- Writing custom attributes — <https://learn.microsoft.com/en-us/dotnet/standard/attributes/writing-custom-attributes>
- `AttributeUsageAttribute` — <https://learn.microsoft.com/en-us/dotnet/api/system.attributeusageattribute>
- `CustomAttributeData` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.customattributedata>
- Assemblies in .NET — <https://learn.microsoft.com/en-us/dotnet/standard/assembly/>

---

## Part 1 summary

1. Normal code operates on data; metaprogramming operates on the program's own
   structure.
2. It exists because library authors cannot know their users' types.
3. It works because **every .NET assembly is self-describing** — metadata is required,
   not optional.
4. Benefits: automation, less boilerplate, maintainability, and — uniquely —
   extensibility.
5. Three stages, and keeping them apart matters: compile-time generation, runtime
   introspection, runtime generation.
6. Implicit vs explicit metadata is about **authorship**, not a CLR category.
7. The trade is always the same: **compile-time safety for run-time flexibility.**

**Next:** [Part 2 — Runtime Introspection](Part-2-Runtime-Introspection.md)
