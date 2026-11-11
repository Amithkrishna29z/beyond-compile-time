# Part 2 — Runtime Introspection

**Session time: 10 minutes (slides 8–15)**
**Runnable examples: [`02-Reflection`](../Code-Examples/02-Reflection),
[`03-Attributes`](../Code-Examples/03-Attributes),
[`04-Plugin-Discovery`](../Code-Examples/04-Plugin-Discovery)**

---

# 1. What Is Runtime Introspection?

## 1.1 Definition

**Runtime introspection** is a program examining its own structure — assemblies, types
and members — while it is executing, using the metadata stored in its assemblies.

In .NET this is done with the **Reflection API** in `System.Reflection`.

## 1.2 In simple English

The program asks questions about itself and gets answers:

- "What types are in this DLL?"
- "What methods does this type have?"
- "Is this method marked `[Obsolete]`?"
- "What arguments does this constructor need?"

It answers them by reading the description the compiler already wrote into the
assembly. It is **not** reading your `.cs` files — source is not needed and is usually
absent.

## 1.3 Runtime type information

The CLR keeps, for every loaded type, a runtime structure describing it. `System.Type`
is your handle on that structure. Every object on the heap carries a pointer to its
type's information, which is how `GetType()` can work on an `object` reference with no
other context — and how `is`, `as` and virtual dispatch work.

Introspection is therefore not bolted on. It exposes bookkeeping the runtime must do
anyway.

## 1.4 Use cases

| Use case | Who does it |
|---|---|
| Serialization | `System.Text.Json`, XML, protobuf |
| ORM mapping | EF Core, Dapper |
| Dependency injection | `Microsoft.Extensions.DependencyInjection` |
| Test discovery | xUnit, NUnit, MSTest |
| Plugin loading | MEF, custom hosts, MSBuild, Roslyn analyzers |
| Validation | ASP.NET Core model validation, FluentValidation |
| Mapping | AutoMapper, Mapster |
| Debuggers and profilers | Visual Studio, `dotnet-dump` |
| Admin/diagnostic UIs | Swagger, health-check dashboards |

## 1.5 Limitations

- **Read-only.** Introspection cannot change a loaded type. (Creating *new* types is
  Part 3.)
- **Needs the assembly loaded** (or a `MetadataLoadContext`).
- **Slow per call** relative to direct access.
- **Invisible to the trimmer**, so trimming can delete what you intend to reflect over.
- **No compile-time checking** — every error is a run-time error.
- **Cannot see inside method bodies.** Metadata describes *signatures*, not
  implementations. You can see that `Withdraw(decimal)` exists; you cannot ask "does it
  validate its argument?" The body is IL, and reading IL is a different job
  (`MethodBody.GetILAsByteArray()`, or a decompiler).

That last point is worth saying in the talk — people often assume reflection can read
logic. It cannot.

## 1.6 Documentation

- Reflection in .NET — <https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/reflection>

---

# 2. Understanding .NET Metadata

## 2.1 Definition

**Metadata** is the structured, binary description of every type and member in an
assembly, emitted by the compiler and stored in the assembly file itself.

## 2.2 The hierarchy

```text
AppDomain / process
└── Assembly            MyApp.dll — the deployment and versioning unit
    ├── Manifest        name, version, culture, referenced assemblies
    └── Module          usually exactly one, the PE file itself
        └── Type        class, struct, interface, enum, delegate
            ├── Constructor   ConstructorInfo
            ├── Method        MethodInfo
            ├── Property      PropertyInfo  (name bound to accessor methods)
            ├── Field         FieldInfo
            ├── Event         EventInfo
            └── Nested type   Type
```

Every level can carry **custom attributes**.

## 2.3 Verified, from example 02

```text
Simple name      : ReflectionDemo
Version          : 1.0.0.0
FullName         : ReflectionDemo, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
Location on disk : ReflectionDemo.dll
IsDynamic        : False
EntryPoint       : Metaprogramming.ReflectionDemo.Program.Main
Modules          : ReflectionDemo.dll
Referenced       : 3 assemblies, e.g. System.Runtime, System.Linq, System.Console
```

Everything there was read from the assembly's own tables.

## 2.4 Each level explained

**Assembly.** The unit of deployment, versioning and identity. One or more modules plus
a manifest. `Assembly.GetName()` gives the identity; `GetReferencedAssemblies()` gives
the dependencies recorded at compile time.

**Assembly manifest.** The part of the metadata recording the assembly's own identity,
its referenced assemblies and their expected versions, and its resources. This is what
the loader uses to resolve dependencies.

**Module.** A physical container. Multi-module assemblies are legal but essentially
unused in modern .NET, so assembly ≈ module in practice.

**Type.** `TypeDef` rows for types you declared, `TypeRef` rows for types you
referenced. `Type` also represents constructed generics (`List<int>`), arrays and
by-ref types, which is why `Type.Name` can be `List\`1`.

**Method.** Name, signature, calling convention, accessibility, and the location of the
IL body. `MethodInfo` exposes the signature; the body is separate.

**Field.** Name, type, accessibility. Note that **auto-properties produce a backing
field** — reflection shows it, with a compiler-generated name:

```text
FIELDS (FieldInfo) - including the compiler's backing fields:
  private  String     _auditTrail
  private  Decimal    <AnnualRate>k__BackingField
```

The `<...>k__BackingField` name is deliberately unspeakable in C# so you cannot collide
with it.

**Property.** *Not* a storage location. A property is metadata binding a name to a
getter and/or setter method. Those accessors are real methods, flagged
`IsSpecialName`:

```text
METHODS (MethodInfo) - DeclaredOnly to hide inherited object members:
  Decimal    get_AnnualRate()  (property accessor)
  Decimal    ProjectInterest(Int32 months)
  Decimal    AddInterest(Int32 months)
  String     Describe()
```

**Constructor.** A method named `.ctor` (instance) or `.cctor` (static), surfaced as
`ConstructorInfo`.

**Custom attributes.** Rows in the `CustomAttribute` table — Part 1 §5.5.

## 2.5 How metadata is stored

A managed assembly is a **PE file** (the Windows executable format) with extra
sections. The CLI header points at metadata streams:

| Stream | Contents |
|---|---|
| `#~` | the compressed metadata tables (`TypeDef`, `MethodDef`, …) |
| `#Strings` | identifier names |
| `#US` | user strings (your string literals) |
| `#Blob` | signatures and attribute arguments |
| `#GUID` | GUIDs |

Tables reference each other by index, which is what makes lookups fast. The format is
specified in **ECMA-335**.

Two practical consequences:

1. **Metadata cannot be stripped.** It is load-bearing. Trimming removes *unused
   members*; it does not remove the description of what remains.
2. **Anyone with your DLL can read your API** — and decompile it. Reflection is not the
   privacy risk; the IL being right there is. Obfuscation renames things but the
   structure is still present.

## 2.6 Common mistakes

1. Thinking a property is a field. It is a pair of methods.
2. Being surprised by `<X>k__BackingField` and compiler-generated `<>c` closure types.
3. Expecting `GetReferencedAssemblies()` to list *loaded* assemblies. It lists what was
   referenced at compile time; use `AppDomain.CurrentDomain.GetAssemblies()` for what
   is actually loaded.

## 2.7 Audience questions

**Q: Does metadata make my assemblies much bigger?**
It is a modest fraction of a typical assembly and it is not optional. You cannot trade
it away for size.

**Q: Can I read metadata without loading and executing the assembly?**
Yes, and you should for untrusted or cross-architecture assemblies: use
`System.Reflection.Metadata` (low-level reader) or `MetadataLoadContext` (reflection-like
API that never runs code). Example 03's `GetCustomAttributesData()` is the same idea at
member level.

**Q: Is reflection reading my source files?**
No. Source is not deployed and is not needed. Everything comes from the DLL.

## 2.8 Documentation

- Assemblies in .NET — <https://learn.microsoft.com/en-us/dotnet/standard/assembly/>
- `MetadataLoadContext` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.metadataloadcontext>

---

# 3. Introduction to the Reflection API

## 3.1 The object model

```text
                    MemberInfo  (abstract)
                         │
     ┌──────────┬────────┼─────────┬──────────┐
     │          │        │         │          │
   Type   MethodBase  FieldInfo PropertyInfo EventInfo
     │       │    │
     │  MethodInfo ConstructorInfo
     │
  (Type is itself a MemberInfo, because a nested type is a member)
```

Everything derives from `MemberInfo`, which is why they all have `Name`,
`DeclaringType` and `GetCustomAttributes`.

## 3.2 The core types

| Type | Represents | Key members |
|---|---|---|
| `Assembly` | a loaded assembly | `GetTypes`, `GetName`, `GetType(string)`, `Location`, `IsDynamic` |
| `Module` | a module | `Name`, `GetTypes` |
| `Type` | a type | `GetMethods`, `GetProperties`, `GetFields`, `GetConstructors`, `IsAssignableFrom`, `BaseType`, `GetInterfaces` |
| `MethodInfo` | a method | `Invoke`, `ReturnType`, `GetParameters`, `CreateDelegate` |
| `ConstructorInfo` | a constructor | `Invoke`, `GetParameters` |
| `PropertyInfo` | a property | `GetValue`, `SetValue`, `CanRead`, `CanWrite`, `PropertyType` |
| `FieldInfo` | a field | `GetValue`, `SetValue`, `IsPrivate`, `IsInitOnly` |
| `ParameterInfo` | a parameter | `Name`, `ParameterType`, `HasDefaultValue` |
| `BindingFlags` | a filter | see §6 |

## 3.3 How they relate

```text
Assembly.GetTypes()        → Type[]
Type.GetMethods()          → MethodInfo[]
MethodInfo.GetParameters() → ParameterInfo[]
MethodInfo.ReturnType      → Type        ← back to Type: the graph is navigable
PropertyInfo.DeclaringType → Type
```

The graph is fully connected: from any node you can reach any other. That is what makes
generic framework code possible.

## 3.4 Common mistakes

1. **Not caching.** `GetProperties()` allocates a fresh array each call.
2. **Using `==` on `Type` for assignability.** Use `IsAssignableFrom` so inheritance
   counts.
3. **Confusing `typeof(T)` and `GetType()`.** The first is the static type, the second
   the runtime type. Example 02 shows them disagreeing.
4. **Forgetting generics complicate `Name`.** `List<int>.Name` is `List\`1`; use
   `GetGenericArguments()` for the rest.

## 3.5 Audience questions

**Q: Are two `typeof(X)` calls the same object?**
Yes — for a given type in a given load context the runtime returns the same `Type`
instance, so `ReferenceEquals` is true and `==` is safe. Across *different* load
contexts they are different types (example 04 §3).

**Q: Why is there both `MethodBase` and `MethodInfo`?**
`MethodBase` is what methods and constructors share (`Invoke`, `GetParameters`).
`MethodInfo` adds `ReturnType`, which a constructor does not have.

**Q: Thread safety?**
Reading reflection objects is thread-safe. Your own caches around them are not —
use `ConcurrentDictionary`.

## 3.6 Documentation

- `System.Reflection` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection>
- `Type` — <https://learn.microsoft.com/en-us/dotnet/api/system.type>

---

# 4. Assembly and Type Discovery

## 4.1 The four ways to get a `Type`

```csharp
Type a = typeof(BankAccount);                                        // compile time
Type b = someObject.GetType();                                       // runtime type of an instance
Type? c = Type.GetType("Metaprogramming.ReflectionDemo.BankAccount");// from a string
Type? d = Assembly.GetExecutingAssembly().GetType("...SavingsAccount");// from one assembly
```

Verified output:

```text
(a) typeof(BankAccount)            -> Metaprogramming.ReflectionDemo.BankAccount
(b) instance.GetType()             -> Metaprogramming.ReflectionDemo.SavingsAccount   (note: NOT BankAccount)
    static type of the variable is object, runtime type is SavingsAccount
(c) Type.GetType("...BankAccount") -> Metaprogramming.ReflectionDemo.BankAccount
(d) assembly.GetType("...")        -> Metaprogramming.ReflectionDemo.SavingsAccount
```

| Way | Resolved | Checked | Use when |
|---|---|---|---|
| `typeof(T)` | compile time | **yes** | the type is known — always prefer this |
| `GetType()` | run time | n/a | you have an instance |
| `Type.GetType(string)` | run time | no | the name is data (config, plugin manifest) |
| `Assembly.GetType(string)` | run time | no | you know which assembly to look in |

## 4.2 Assembly-qualified names, and why `Type.GetType` returns `null`

**This is the most common reflection bug.** `Type.GetType(string)` searches only:

1. the **calling** assembly, and
2. `System.Private.CoreLib` (the core library).

A type in any other assembly needs an **assembly-qualified name**. Verified:

```text
Unqualified name from another assembly:
  Type.GetType("System.Text.Json.JsonSerializerOptions") = null  <- this is the trap
Assembly-qualified name:
  Type.GetType("...JsonSerializerOptions, System.Text.Json") = JsonSerializerOptions
```

The canonical form is on the type itself:

```text
typeof(JsonSerializerOptions).AssemblyQualifiedName =
  System.Text.Json.JsonSerializerOptions, System.Text.Json, Version=10.0.0.0,
  Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
```

**Name grammar:**

| Form | Example |
|---|---|
| Namespace + name | `MyApp.Customer` |
| Assembly-qualified | `MyApp.Customer, MyApp` |
| Fully qualified | `MyApp.Customer, MyApp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null` |
| **Nested type** | `MyApp.Outer+Inner` ← a **`+`**, not a `.` |
| Generic | `System.Collections.Generic.List\`1[[System.Int32, System.Private.CoreLib]]` |
| Array | `MyApp.Customer[]` |

**The five reasons `Type.GetType` returns `null`:**

1. The name is not assembly-qualified and the type is elsewhere. *(most common)*
2. The containing assembly is not loaded, and nothing triggers a load.
3. A nested type written with `.` instead of `+`.
4. A typo — including the wrong namespace, or omitting it.
5. The assembly version/culture/key in a fully qualified name does not match.

**Make it diagnosable.** Pass `throwOnError: true` when a missing type is a bug:

```csharp
Type.GetType("Totally.Missing.Type", throwOnError: true);
// -> TypeLoadException, with a message naming what it looked for
```

Verified:

```text
  TypeLoadException: Could not load type 'Totally.
```

A silent `null` that becomes a `NullReferenceException` three frames later is far worse
than an immediate, named exception.

## 4.3 Enumerating types

```csharp
Assembly current = Assembly.GetExecutingAssembly();
Type[] all = current.GetTypes();
```

Verified:

```text
assembly.GetTypes() returned 8 types (compiler-generated ones included).

The types we actually declared:
  BankAccount            abstract class  base=Object
  CurrentAccount         class           base=BankAccount
  FixedDepositAccount    class           base=BankAccount
  IInterestBearing       interface       base=-
  LedgerEntry            class           base=Object
  Program                static class    base=Object
  SavingsAccount         class           base=BankAccount
```

Note `GetTypes()` includes **compiler-generated** types — closure classes (`<>c`),
iterator state machines, async state machines. Filter them (`!t.Name.StartsWith('<')`)
or your "list my types" feature looks bizarre.

| Method | Returns |
|---|---|
| `GetTypes()` | every type, public and not |
| `GetExportedTypes()` | only publicly visible types |
| `DefinedTypes` | every type as `TypeInfo` |

## 4.4 Finding implementations — the core of plugin discovery

```csharp
all.Where(t => typeof(IInterestBearing).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
```

Verified:

```text
  SavingsAccount implements IInterestBearing
  FixedDepositAccount implements IInterestBearing
```

Three details that are all mistakes if you skip them:

1. `IsAssignableFrom` — accounts for inheritance. `t == typeof(I)` would find nothing.
2. `!t.IsInterface` — the interface itself is assignable from itself.
3. `!t.IsAbstract` — an abstract base cannot be instantiated. Note that
   `IsAbstract` is also `true` for `static` classes.

## 4.5 `ReflectionTypeLoadException` — the production detail

`GetTypes()` **throws** if any type in the assembly cannot be loaded, typically because
a dependency is missing. The types that *did* load are still available:

```csharp
private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
{
    try
    {
        return assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException ex)
    {
        Console.WriteLine("  warning: " + ex.LoaderExceptions.Length + " type(s) failed to load in "
            + assembly.GetName().Name + "; continuing with the rest.");
        return ex.Types.Where(t => t is not null).Cast<Type>();
    }
}
```

Any host that scans assemblies it did not build **must** do this. One plugin with a
missing dependency would otherwise take down discovery for all of them.

## 4.6 Audience questions

**Q: How do I scan every assembly in my application?**
`AppDomain.CurrentDomain.GetAssemblies()` returns the *loaded* ones — and assemblies
load lazily, so one you have not touched yet will be missing. That is why scanning
libraries ask you to pass the assemblies explicitly, or scan DLL files in a directory.

**Q: Does `GetTypes()` force dependent assemblies to load?**
It can. Resolving a base type or an interface requires loading the assembly that
defines it. This is one reason type scanning costs startup time.

**Q: Can I scan without loading anything?**
Yes — `MetadataLoadContext`. Use it to vet a plugin before committing to loading it.

## 4.7 Documentation

- `Assembly` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.assembly>
- `Type` — <https://learn.microsoft.com/en-us/dotnet/api/system.type>

---

# 5. Inspecting Types

## 5.1 The four families

```csharp
Type t = typeof(SavingsAccount);

ConstructorInfo[] ctors = t.GetConstructors();
PropertyInfo[] properties = t.GetProperties();
MethodInfo[] methods = t.GetMethods();
FieldInfo[] fields = t.GetFields();
```

Verified:

```text
CONSTRUCTORS (ConstructorInfo):
  .ctor(String accountNumber, Decimal balance, Decimal annualRate)

PROPERTIES (PropertyInfo):
  Decimal    AnnualRate     {get}   declared on SavingsAccount
  String     AccountNumber  {get}   declared on BankAccount
  Decimal    Balance        {get;set}   declared on BankAccount
  String     Nickname       {get;set}   declared on BankAccount
```

Note `DeclaringType`: `GetProperties()` returns **inherited** members too. Use
`BindingFlags.DeclaredOnly` to see only what this type declares.

Also note `Balance` reports `{get;set}` even though its setter is `protected`.
`CanWrite` is true if *a* setter exists, regardless of accessibility — to find a
*public* setter use `GetSetMethod()` (which returns `null` for a non-public one) or
`GetSetMethod(nonPublic: true)`.

## 5.2 Reading and writing

```csharp
PropertyInfo balance = t.GetProperty(nameof(SavingsAccount.Balance))!;
Console.WriteLine(balance.GetValue(account));

PropertyInfo nickname = t.GetProperty(nameof(SavingsAccount.Nickname))!;
nickname.SetValue(account, "Rainy day fund");

FieldInfo? auditTrail = t.GetField("_auditTrail", BindingFlags.NonPublic | BindingFlags.Instance);
auditTrail?.SetValue(account, "rewritten-by-reflection");
```

Verified:

```text
Read  Balance via PropertyInfo.GetValue -> 5000
Wrote Nickname via PropertyInfo.SetValue -> Rainy day fund
Read  private field _auditTrail         -> created
Wrote private field _auditTrail         -> rewritten-by-reflection
```

Use `nameof(...)` rather than `"Balance"` wherever the member is known at compile time.
It is a string in the IL either way, but a rename or typo becomes a **build error**.
This one habit removes a large share of reflection bugs.

## 5.3 Invoking by name

```csharp
MethodInfo? method = account.GetType().GetMethod("AddInterest");
object? result = method.Invoke(account, [12]);
```

Verified:

```text
Found: Decimal AddInterest(Int32)
Invoke(account, [12]) returned 120.00
Balance is now 2120.00
```

**Two failure modes you must know.**

Wrong argument types fail at run time:

```text
  ArgumentException: Object of type 'System.String' cannot be converted to type 'System.Int32'.
```

And an exception thrown *inside* the target is **wrapped**:

```text
  TargetInvocationException -> InnerException is the real one:
    InvalidOperationException: Insufficient funds: balance is 2120.00.
```

`TargetInvocationException` is the single most important exception to understand here.
If you let it escape, every `catch (InvalidOperationException)` upstream silently stops
matching. Always unwrap:

```csharp
catch (TargetInvocationException ex) when (ex.InnerException is not null)
{
    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();  // preserves the stack trace
}
```

`ExceptionDispatchInfo` rather than `throw ex.InnerException` — the latter resets the
stack trace. Example 12 does this properly.

## 5.4 Overloads

`GetMethod("Foo")` throws `AmbiguousMatchException` when `Foo` is overloaded. Specify
the signature:

```csharp
MethodInfo mathMax = typeof(Math).GetMethod(nameof(Math.Max), [typeof(int), typeof(int)])!;
```

## 5.5 Documentation

- `MethodInfo` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.methodinfo>
- `PropertyInfo` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.propertyinfo>

---

# 6. `BindingFlags` — Public vs Non-Public

## 6.1 Definition

`BindingFlags` is a `[Flags]` enum that filters which members a reflection lookup
returns. **If a member is "missing", this is almost always why.**

## 6.2 The important values

| Flag | Meaning |
|---|---|
| `Public` | public members |
| `NonPublic` | private, protected, internal, protected internal |
| `Instance` | per-object members |
| `Static` | per-type members |
| `DeclaredOnly` | this type only — no inherited members |
| `FlattenHierarchy` | include inherited **static** members |
| `IgnoreCase` | case-insensitive name matching |

**The default when you pass no flags is `Public | Instance | Static`.** So:

- private members are **missing** unless you ask for `NonPublic`;
- inherited members are **included** unless you ask for `DeclaredOnly`.

You must pass **at least one** of `Public`/`NonPublic` **and** at least one of
`Instance`/`Static`, or you get an empty result. An empty array from a type you know has
members is nearly always a `BindingFlags` mistake.

## 6.3 Verified counts

```text
GetMethods() with no arguments means Public | Instance | Static:
  count = 14 (includes inherited object methods)

Public | Instance | DeclaredOnly  -> 4
NonPublic | Instance | DeclaredOnly -> 1

The private methods reflection can see:
  private String Normalise(String note)

Static members need BindingFlags.Static - they are NOT returned by Instance:
  public static String ProductCode()
```

## 6.4 Accessibility is not security

This deserves its own statement, because it is a genuine and common misunderstanding:

> **`private` is a compiler rule, enforced at compile time.**
> **Reflection with `BindingFlags.NonPublic` reads private state, and that is allowed.**
> **Accessibility is an encapsulation feature, not a security boundary.**

Code running in your process can read any field of any object. There is no supported
in-process sandbox in modern .NET (Code Access Security was a .NET Framework feature and
is gone). If something must be protected from code in the same process, the answer is a
process or OS boundary, not an access modifier.

## 6.5 Legitimate and illegitimate uses of `NonPublic`

**Legitimate:** deserialization restoring objects with no public setters; ORM
materialisation; test fixtures setting up otherwise unreachable state; diagnostics.

**Illegitimate:** routing around another library's invariants in production code. It
compiles, it works, and it breaks on their next patch release — private members are not
part of any contract.

## 6.6 Audience questions

**Q: Why did `GetMethods()` return 14 methods on a class with 4?**
Inherited members. `object` contributes `ToString`, `Equals`, `GetHashCode`,
`GetType`; the base class contributes more; property accessors are methods too. Use
`DeclaredOnly`.

**Q: Can reflection call a private method?**
Yes — `GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)` then `Invoke`.
See §6.4 for why this is allowed.

**Q: Should I unit-test private methods this way?**
Generally no — test through the public surface, or make the unit a separate type. The
pragmatic exception is a complex private algorithm, where `InternalsVisibleTo` plus
`internal` is cleaner than reflection.

## 6.7 Documentation

- `BindingFlags` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.bindingflags>

---

# 7. Dynamic Object Creation — `Activator.CreateInstance`

## 7.1 Definition

`Activator.CreateInstance` creates an instance of a type represented by a `Type`
object, choosing a constructor by matching the arguments you supply.

## 7.2 Why it exists

`new Foo()` requires the compiler to know `Foo`. When the type comes from a string, a
config file or a scanned assembly, there is no name to write. `Activator` is the bridge
from **"a `Type` I discovered"** to **"a live object"**.

## 7.3 How it works internally

1. Validate the `Type` — not null, not abstract, not an interface, not open generic.
2. Enumerate `ConstructorInfo`s matching the `BindingFlags`.
3. Use a `Binder` to pick the best match for the argument types, with coercions.
4. Allocate the object and run the chosen constructor with the unboxed arguments.
5. Return it as `object` — you then cast.

Steps 2–3 are why it is slower than `new`. `new` is a single `newobj` IL instruction
with the constructor resolved at compile time.

## 7.4 The forms

```csharp
// (a) generic — needs a public parameterless constructor
var current = Activator.CreateInstance<CurrentAccount>();

// (b) Type plus constructor arguments
object? savings = Activator.CreateInstance(typeof(SavingsAccount), "SB-77", 15000m, 0.07m);

// (c) the realistic case — the type name is a string
Type? discovered = Type.GetType("Metaprogramming.ReflectionDemo.FixedDepositAccount");
object? fd = Activator.CreateInstance(discovered!, "FD-12", 100000m, 24);

// (d) reach a non-public constructor
object? internalOnly = Activator.CreateInstance(typeof(LedgerEntry), nonPublic: true);

// (e) via ConstructorInfo — explicit, and cacheable
ConstructorInfo ctor = typeof(SavingsAccount).GetConstructor([typeof(string), typeof(decimal), typeof(decimal)])!;
var viaCtor = (SavingsAccount)ctor.Invoke(["SB-88", 250m, 0.03m]);
```

Verified:

```text
(a) Activator.CreateInstance<CurrentAccount>()  -> Current CA-0000 balance=0
(b) Activator.CreateInstance(type, args)       -> Savings SB-77 balance=15000 rate=0.07
(c) from a string type name                   -> FixedDeposit FD-12 principal=100000 term=24m
(d) nonPublic:true reaches a private .ctor      -> LedgerEntry LE-fc5fb2
(e) ConstructorInfo.Invoke                     -> Savings SB-88 balance=250 rate=0.03
```

Form **(e)** is the one to use on a hot path: cache the `ConstructorInfo` (or better,
build a delegate from it) and skip the constructor search on every call.

## 7.5 `Activator.CreateInstance` vs `new`

| | `new SavingsAccount(...)` | `Activator.CreateInstance(type, ...)` |
|---|---|---|
| Type known | compile time | run time |
| IL | one `newobj` | a method call that searches and binds |
| Type-checked | **yes** | no |
| Speed | fastest | much slower |
| Wrong args | build error | `MissingMethodException` |
| Use when | the type is known | and only when it is not |

## 7.6 Every failure mode, triggered deliberately

Verified output from example 02:

```text
MissingMethodException  : Constructor on type 'Metaprogramming.ReflectionDemo.SavingsAccount' not found.
Abstract type           : Constructor on type 'Metaprogramming.ReflectionDemo.BankAccount' not found.
Private .ctor, nonPublic:false -> MissingMethodException (use the nonPublic overload)
Ctor threw              : TargetInvocationException -> ArgumentOutOfRangeException: Opening balance cannot be negative. (Parameter 'balance')
Null Type               : ArgumentNullException (paramName=type)
```

| Exception | Cause |
|---|---|
| `MissingMethodException` | no constructor matches the arguments |
| `MissingMethodException` | the type is abstract, or an interface |
| `MissingMethodException` | the only constructor is non-public and you did not pass `nonPublic: true` |
| `TargetInvocationException` | the constructor itself threw — check `InnerException` |
| `ArgumentNullException` | the `Type` was null |
| `TypeLoadException` | the type could not be loaded |

Note the abstract-type message says "Constructor … not found", which is **misleading** —
the real problem is that the type is abstract. Check `type.IsAbstract` yourself and
throw a clear error. A host that reports *"PluginX is abstract and cannot be
instantiated"* is far kinder than one that reports *"Constructor on type PluginX not
found"*.

Every one of these is a **run-time** failure. That is the price of reflective
construction.

## 7.7 Real-world applications

- DI containers (example 07) — though they use `ActivatorUtilities`, which also resolves
  the constructor's dependencies.
- Deserializers creating your DTOs.
- Plugin hosts (example 04).
- EF Core materialising entities.
- Test frameworks instantiating test classes.

## 7.8 Common mistakes

1. **Using it when the type is known.** Use `new`.
2. **Not checking `IsAbstract` / `IsInterface` first**, then getting the confusing
   message above.
3. **Calling it per item in a loop** without caching a `ConstructorInfo` or delegate.
4. **Letting `TargetInvocationException` escape**, hiding the real error.
5. **Assuming a parameterless constructor exists.** Many types have none.
6. **Expecting `Activator` to supply dependencies.** It cannot — it knows nothing about
   your container. That is what `ActivatorUtilities` is for (Part 4 §2).

## 7.9 Audience questions

**Q: Is `Activator.CreateInstance<T>()` as fast as `new T()`?**
No. Even the generic form goes through the activation machinery. For `T : new()` the
JIT can optimise some cases, but do not assume parity — measure if it matters.

**Q: How do DI containers make this fast?**
They resolve the constructor **once**, build a compiled factory delegate (expression
tree or emitted IL), cache it, and never search again. Example 07 §8 shows the engine;
Part 3 §4 shows the technique.

**Q: Can it create a generic type at run time?**
Yes, but close it first: `typeof(List<>).MakeGenericType(typeof(int))`, then
`CreateInstance` that. Passing an open generic throws.

**Q: What about types with only a private constructor, like a singleton?**
`nonPublic: true` reaches it — see form (d). Which is another reminder that a private
constructor is not a guarantee, merely a strong hint.

## 7.10 Documentation

- `Activator.CreateInstance` — <https://learn.microsoft.com/en-us/dotnet/api/system.activator.createinstance>

---

# 8. Attributes and Custom Attributes

## 8.1 Definition

An **attribute** is a class deriving from `System.Attribute` whose application to a
declaration stores a row of metadata on that declaration. It is **declarative data**,
not behaviour.

## 8.2 Why attributes exist

To attach information to code in the place it belongs, where it cannot drift away from
what it describes, and to let separate machinery act on it. The alternative — an
external configuration file listing members by name — is exactly the drift-prone design
attributes replace.

## 8.3 How it works internally

Applying an attribute writes a `CustomAttribute` row recording the attribute type and
its constructor arguments. **The attribute object is not stored** — only the recipe.
`GetCustomAttributes` runs the constructor on demand, so:

```text
  ReferenceEquals(first, second) = False
```

Two calls, two objects. (Part 1 §5.5.)

## 8.4 Declaring one

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class TableAttribute(string name) : Attribute
{
    public string Name { get; } = name;              // positional (constructor) argument
    public string Schema { get; set; } = "dbo";      // named argument
}
```

Applied as:

```csharp
[Table("invoice_lines", Schema = "sales")]
internal sealed class InvoiceLine { /* ... */ }
```

- **Positional arguments** are constructor parameters, required, in order.
- **Named arguments** are settable properties or fields, optional, in any order.
- Every argument must be a **compile-time constant** (or a `Type`, or an array of
  those). You cannot pass `DateTime.Now` or a `new`ed object — a real constraint that
  pushes complex configuration toward a fluent API.

By convention the class is named `XAttribute` and used as `[X]`; the compiler appends
the suffix for you.

## 8.5 `AttributeUsage` — the three settings

| Setting | Purpose | Default |
|---|---|---|
| `ValidOn` (constructor arg) | which declarations it may be applied to | `AttributeTargets.All` |
| `AllowMultiple` | may it repeat on one target? | `false` |
| `Inherited` | do derived types/overrides see it? | `true` |

Verified:

```text
TagAttribute:
  ValidOn       = Class
  AllowMultiple = True
  Inherited     = False
ValidateRangeAttribute:
  ValidOn       = Property
  AllowMultiple = False
  Inherited     = True
```

`AttributeTargets` is a `[Flags]` enum — `Class`, `Method`, `Property`, `Field`,
`Parameter`, `Constructor`, `Assembly`, `Module`, `Enum`, `Event`, `Interface`,
`Delegate`, `ReturnValue`, `GenericParameter`, `All`. Combine with `|`. Applying an
attribute to a disallowed target is a **compile error**, which is the point: it is
compiler-enforced documentation of intent.

## 8.6 `AllowMultiple`

```csharp
[Tag("billing")]
[Tag("generated-nightly")]
[Tag("pii-free")]
internal sealed class InvoiceLine { }
```

```text
InvoiceLine carries 3 [Tag] attributes:
  billing
  generated-nightly
  pii-free
```

With `AllowMultiple = false` a second application is **compile error CS0579** — the
constraint is enforced by the compiler, not discovered at run time.

**The trap:** `GetCustomAttribute<T>()` (singular) *throws* when several are present:

```text
  AmbiguousMatchException - use GetCustomAttributes<T>() instead.
```

## 8.7 `Inherited`

Both switches must agree. `BaseEntity` declares `[Audited]` (`Inherited = true`) and
`[Table]` (`Inherited = false`); `DerivedEntity` declares neither:

```text
inherit: false - look only at the type's own metadata:
  [Audited] found = False
  [Table]   found = False

inherit: true - also walk the base chain:
  [Audited] found = True   <- Inherited=true on the attribute, and we asked for inheritance
  [Table]   found = False  <- Inherited=false on the attribute, so inheritance is refused
```

> The attribute must be declared `Inherited = true` **and** the call must pass
> `inherit: true`. Either one off means no result.

Note also that `inherit: true` behaves differently per member kind, and does **not**
walk interface implementations — an attribute on an interface member is not found on the
implementing class this way. You must check the interface explicitly. (Example 12
deliberately puts `[Cacheable]` on the *interface* and reads it from the interface's
`MethodInfo`, which is why it works there.)

## 8.8 Reading attributes

| API | Constructs? | Use for |
|---|---|---|
| `IsDefined(type)` | no | cheapest existence check |
| `GetCustomAttribute<T>()` | yes | exactly one expected |
| `GetCustomAttributes<T>()` | yes | `AllowMultiple = true` |
| `GetCustomAttributesData()` | **no** | untrusted assemblies, tooling |

`GetCustomAttributesData()` matters for two reasons: no attribute constructor runs (so
untrusted code cannot execute), and the attribute type need not even be loadable.

## 8.9 The payoff — an attribute-driven engine

```csharp
internal sealed class Payment
{
    [ValidateRange(0.01, 1_000_000)]
    public decimal Amount { get; set; }

    [ValidateText(ExactLength = 3, Required = true)]
    public string CurrencyCode { get; set; } = string.Empty;

    [ValidateText(Required = true)]
    public string Reference { get; set; } = string.Empty;

    public string? Notes { get; set; }   // no attributes: skipped silently
}
```

The engine never names `Payment`:

```csharp
foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    if (!property.CanRead) continue;
    object? value = property.GetValue(instance);

    var range = property.GetCustomAttribute<ValidateRangeAttribute>();
    if (range is not null && value is not null)
    {
        double numeric = Convert.ToDouble(value);
        if (numeric < range.Minimum || numeric > range.Maximum)
        {
            errors.Add(property.Name + " must be between " + range.Minimum + " and " + range.Maximum
                + " (was " + numeric + ")");
        }
    }
    // ... text rules
}
```

Verified:

```text
VALID payment: Amount=2500 CurrencyCode="INR" Reference="PAY-001"
  OK - no violations

INVALID payment: Amount=-5 CurrencyCode="RUPEES" Reference=""
  ERROR: Amount must be between 0.01 and 1000000 (was -5)
  ERROR: CurrencyCode must be exactly 3 characters (was 6)
  ERROR: Reference is required
```

This is, in miniature, ASP.NET Core model validation.

## 8.10 Real-world attribute readers

| Attribute | Read by | To decide |
|---|---|---|
| `[HttpGet]`, `[Route]` | ASP.NET Core, at startup | the routing table |
| `[Required]`, `[EmailAddress]` | MVC model binding | validation errors |
| `[JsonPropertyName]`, `[JsonIgnore]` | `System.Text.Json` | the JSON shape |
| `[Table]`, `[Column]`, `[NotMapped]` | EF Core, at model build | the schema |
| `[Fact]`, `[Theory]` | xUnit | which methods are tests |
| `[Obsolete]` | the **compiler** | whether to warn |
| `[Serializable]`, `[Flags]`, `[DllImport]` | the runtime | low-level behaviour |

## 8.11 Common mistakes

1. Singular `GetCustomAttribute<T>()` on a multiple-allowed attribute →
   `AmbiguousMatchException`.
2. Forgetting one of the two `Inherited`/`inherit` switches.
3. Expecting interface-member attributes to be inherited by implementations. They are
   not; check the interface.
4. Putting behaviour in the attribute. Attributes are data.
5. Omitting `[AttributeUsage]` and getting surprising defaults.
6. Reading attributes on a hot path without caching.
7. Trying to pass a non-constant attribute argument — a compile error.

## 8.12 Audience questions

**Q: Can an attribute change behaviour on its own?**
No. It is inert until something reads it. `[JsonIgnore]` does nothing unless
`System.Text.Json` looks for it. This is the most common misconception about attributes.

**Q: Why are two `GetCustomAttribute` results different objects?**
Because only the constructor *arguments* are stored; each read constructs a fresh
instance. Cache if you need identity or performance.

**Q: Can I put an attribute on a record's positional parameter?**
Yes, with an explicit target: `[property: Required]` puts it on the generated property;
`[param: ...]` on the parameter. Part 1 §5.3 does exactly this.

## 8.13 Documentation

- Writing custom attributes — <https://learn.microsoft.com/en-us/dotnet/standard/attributes/writing-custom-attributes>
- `AttributeUsageAttribute` — <https://learn.microsoft.com/en-us/dotnet/api/system.attributeusageattribute>
- `AttributeTargets` — <https://learn.microsoft.com/en-us/dotnet/api/system.attributetargets>

---

# 9. Practical Example — Dynamic Plugin Discovery

**Code: [`04-Plugin-Discovery`](../Code-Examples/04-Plugin-Discovery) (three projects)**

## 9.1 The architecture

```text
┌───────────────────────────┐
│ PluginDiscovery.Contracts │   IReportPlugin, [ReportPlugin]
│   (the shared contract)   │   Referenced by BOTH sides. Tiny. Stable.
└───────────┬───────────────┘
            │
    ┌───────┴────────┐
    │                │
┌───▼──────────┐  ┌──▼─────────────────────────┐
│   ...Host    │  │ ...Plugins.Sales           │
│ (the app)    │  │ MonthlySalesPlugin         │
│              │  │ TopCustomersPlugin         │
│ scans a      │  │ ExperimentalForecastPlugin │
│ folder,      │  │ UnannotatedHelperPlugin    │
│ loads, runs  │  └────────────────────────────┘
└──────────────┘      built to  bin/.../plugins/*.dll

The host has NO compile-time reference to the plugin assembly:
    <ProjectReference ... ReferenceOutputAssembly="false" />
It builds the plugin (so the demo works) but cannot see its types.
```

## 9.2 The contract

```csharp
public interface IReportPlugin
{
    string Name { get; }
    string Render(IReadOnlyDictionary<string, string> parameters);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReportPluginAttribute(string displayName) : Attribute
{
    public string DisplayName { get; } = displayName;
    public string Version { get; set; } = "1.0.0";
    public int Order { get; set; } = 100;
    public bool Enabled { get; set; } = true;
}
```

**Why both an interface and an attribute?** The interface says *what the plugin can
do*; the attribute says *what the host should know before deciding to create it*. The
host can list, filter and order plugins from metadata **without constructing any of
them** — which matters when construction might be slow or might fail.

## 9.3 The whole discovery mechanism

```csharp
private static List<PluginDescriptor> DescribePluginsIn(Assembly assembly)
{
    var found = new List<PluginDescriptor>();

    foreach (Type type in SafeGetTypes(assembly))
    {
        if (type.IsAbstract || type.IsInterface || !typeof(IReportPlugin).IsAssignableFrom(type))
        {
            continue;
        }

        var attribute = type.GetCustomAttribute<ReportPluginAttribute>();
        if (attribute is null || !attribute.Enabled)
        {
            continue;
        }

        found.Add(new PluginDescriptor(type, attribute.DisplayName, attribute.Version, attribute.Order));
    }

    return found;
}
```

Twenty lines, and every technique in Part 2 is in them: `GetTypes`,
`IsAssignableFrom`, `IsAbstract`, `GetCustomAttribute`, and then `Activator` to build.

## 9.4 Verified workflow

```text
Plugin folder: ...\PluginDiscovery.Host\bin\Debug\net10.0\plugins
Candidate assemblies: PluginDiscovery.Plugins.Sales.dll

Loaded PluginDiscovery.Plugins.Sales v1.0.0.0
  into load context: PluginALC:PluginDiscovery.Plugins.Sales
  collectible      : True
  accepted MonthlySalesPlugin as "Monthly Sales Summary" v2.1.0
  accepted TopCustomersPlugin as "Top Customers" v2.1.0
  skipped ExperimentalForecastPlugin - [ReportPlugin(Enabled = false)]
  skipped UnannotatedHelperPlugin - implements IReportPlugin but has no [ReportPlugin]

  --- monthly-sales ---
    Monthly sales for 2026-11
      South 418,500.00
      West  302,750.00
      North 211,000.00
      TOTAL 932,250.00
      (rendered by PluginDiscovery.Plugins.Sales v1.0.0.0)
```

Note the two rejections. A good host reports **why** it declined something — a plugin
author whose DLL is silently ignored has nothing to debug.

## 9.5 Dependency isolation and `AssemblyLoadContext`

Each plugin gets its own load context:

```csharp
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private static readonly string[] SharedAssemblies = ["PluginDiscovery.Contracts"];

    public PluginLoadContext(string pluginPath)
        : base(name: "PluginALC:" + Path.GetFileNameWithoutExtension(pluginPath), isCollectible: true)
        => _resolver = new AssemblyDependencyResolver(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share the contract: returning null defers to the Default context.
        if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
        {
            return null;
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
```

Two jobs, and the second is the subtle one:

1. **Isolate private dependencies** so plugin A can use Newtonsoft 11 and plugin B
   version 13.
2. **Deliberately NOT isolate the shared contract**, so both sides use the *same*
   `IReportPlugin` type.

Verified:

```text
Host's   IReportPlugin is from PluginDiscovery.Contracts in context Default
Plugin's IReportPlugin is from PluginDiscovery.Contracts in context Default

Same CLR type? True
Cast works?    True
```

**If you get this wrong** the context loads its own copy of the contracts, the plugin
implements a *different* `IReportPlugin`, and the cast fails with
`InvalidCastException` whose message names **the same type twice**:

> *Unable to cast object of type 'MonthlySalesPlugin' to type 'IReportPlugin'*

…when `MonthlySalesPlugin` obviously implements `IReportPlugin`. That baffling message
is the signature symptom of a duplicated contract assembly. Type identity in .NET is
**(assembly identity + load context + full name)**, not just the name.

## 9.6 Unloading

`isCollectible: true` makes `Unload()` possible. Verified:

```text
Ran "monthly-sales" inside collectible context PluginALC:PluginDiscovery.Plugins.Sales
Unload() has been requested. Now the GC has to actually collect it.
  collection attempt 1: alive = True
  collection attempt 2: alive = False

Load context unloaded: True
```

**`Unload()` is a request, not an action.** The context dies only when nothing
reachable refers to it, its assemblies, or any object whose type came from it.

What keeps a context alive — all common mistakes:

- any live instance of a plugin type;
- a cached `Type`, `MethodInfo` or `Assembly` from the plugin;
- a delegate pointing at plugin code (an event handler left subscribed);
- a static field in the **host** holding any of the above.

Note the `[MethodImpl(MethodImplOptions.NoInlining)]` on the helper in the example: if
the JIT inlined it, the locals could stay rooted in the caller's frame and the context
would never collect. That is how delicate this is.

And unloading only frees **managed state**. It cannot undo side effects the plugin
already caused, and a plugin that P/Invoked a native library may have pinned it.

## 9.7 Trusted vs untrusted assemblies — the security section

This is the most important paragraph in Part 2, and the one most often got wrong:

> **`AssemblyLoadContext` is an isolation boundary for TYPE IDENTITY and VERSIONS.**
> **It is NOT a security sandbox.**

Once you call `LoadFromAssemblyPath`, that code runs with your process's full
privileges. It can:

- read and write any file your process can;
- open sockets, start processes, read environment variables and secrets;
- use reflection against your own private state (§6.4);
- call `Environment.Exit` and take the process down.

**There is no supported in-process sandbox in modern .NET.** Code Access Security and
AppDomain partial trust were .NET Framework features and have been removed. If you must
run untrusted code, isolate it where the OS can enforce it: a separate process with a
reduced token, a container, a VM, or a WASM runtime.

**Practical hardening for a trusted plugin model:**

| Measure | Why |
|---|---|
| Verify a strong name or Authenticode signature before loading | establishes provenance |
| Keep the contracts assembly tiny and version it deliberately | it is your ABI |
| Inspect with `MetadataLoadContext` first and reject early | no plugin code runs during vetting |
| Wrap every plugin call in `try`/`catch` | one bad plugin must not kill the host |
| Log path, version and hash of everything loaded | you will need this after an incident |
| Use `ActivatorUtilities`, not bare `Activator` | plugins can then take dependencies you control |

## 9.8 Version compatibility

The contracts assembly is your ABI. Rules that save pain:

- **Adding a member to an existing interface is a breaking change** for every existing
  plugin. Use a *new* interface (`IReportPluginV2`) and check for both.
- Prefer adding optional **properties on the attribute** over changing the interface —
  old plugins keep working and simply take the defaults.
- Record a plugin's expected contract version and refuse mismatches with a clear
  message rather than letting a `MissingMethodException` surface later.

## 9.9 Audience questions

**Q: Can I load a plugin without restarting the app?**
Loading, yes — that is example 04. *Replacing* one needs a collectible context, dropping
every reference, unloading, and loading the new DLL; and the unload is cooperative, so
it may not happen promptly. Most production systems take the simpler path: run plugins
in a child process and restart that.

**Q: Is `AssemblyLoadContext` a security boundary?**
No. See §9.7. Isolation of type identity, not of privileges.

**Q: Why not just `Assembly.LoadFrom`?**
It loads into the default context: no dependency isolation (plugin versions collide with
yours) and no possibility of unloading. Fine for a trusted first-party DLL, wrong for a
plugin model.

**Q: MEF instead?**
MEF (`System.Composition`) packages this pattern with a catalog and richer composition.
Worth it for a large plugin surface. Understanding the 20 lines in §9.3 first means MEF
stops being magic.

## 9.10 Documentation

- Understanding `AssemblyLoadContext` — <https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext>
- Assembly unloadability — <https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability>
- Create an app with plugin support — <https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support>
- `AssemblyLoadContext` — <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext>

---

## Part 2 summary

1. Introspection reads the metadata every assembly must already carry.
2. The hierarchy is Assembly → Module → Type → Member, and the API mirrors it exactly.
3. All `*Info` types derive from `MemberInfo`, so the object graph is fully navigable.
4. Prefer `typeof`; use `Type.GetType` only when the name is genuinely data — and
   remember it needs an **assembly-qualified** name for other assemblies.
5. `BindingFlags` decides what you see. A missing member is almost always a flags bug.
6. `private` is encapsulation, **not security**.
7. `Activator.CreateInstance` bridges `Type` → object, and every one of its failures is
   a run-time failure.
8. Attributes are **inert declarative data**; something has to read them.
9. Interface + attribute + reflection + `Activator` = a plugin system in 20 lines.
10. `AssemblyLoadContext` isolates **type identity**, not **privileges**.

**Next:** [Part 3 — Runtime Code Generation](Part-3-Runtime-Code-Generation.md)
