# Topic Mastery — Presenter Study Guide

Study this and you can explain any topic in the session without looking anything up.

Every topic has the same seven sections:

| | |
|---|---|
| **A — Beginner** | how you explain it to someone meeting it for the first time |
| **B — Technical** | the internal mechanism, in correct .NET terminology |
| **C — Analogy** | a memorable comparison — **and where it breaks**, which matters as much |
| **D — Practical** | where a working developer actually meets this |
| **E — Code walkthrough** | the lines that matter, and what happens when they run |
| **F — Common confusion** | what people get wrong, so you can pre-empt it |
| **G — Questions** | three or more, with answers |

> **On analogies.** Every analogy here is labelled with how it fails. Use them to open a
> door, then close the door behind you — *"the analogy breaks down because…"* An analogy
> left unqualified becomes the thing people remember, and then they're wrong.

---

# 1. Metadata and self-describing assemblies

## A — Beginner

When you compile C#, the DLL you get contains two things, not one. It contains the
instructions — the executable part — and it contains a **description** of everything you
declared: every type, every method, every parameter name, every attribute. That description
is called metadata, and it's why a program can be asked questions about itself.

## B — Technical

A managed assembly is a PE file with extra sections. The CLI header points at metadata
streams: `#~` holds the compressed tables (`TypeDef`, `MethodDef`, `Field`, `Property`,
`Param`, `CustomAttribute` and about forty more), `#Strings` holds identifiers, `#Blob`
holds signatures and attribute arguments. Tables reference each other by index, which makes
lookups fast. The format is specified in ECMA-335.

The critical property: **metadata is required, not optional.** The CLR needs it to resolve
calls, verify types and collect garbage correctly. Reflection does not add anything to your
assembly; it reads tables that have to be there anyway.

## C — Analogy

A shipping container with its manifest bolted to the outside — you can know what's inside
without opening it.

**Where it breaks:** a manifest is paperwork you could in principle lose and still ship the
container. Metadata isn't like that. The crane *cannot operate* without it. You can't strip
metadata to save space the way you might discard paperwork.

## D — Practical

Every time NuGet shows you IntelliSense for a package you have no source for. Every time a
decompiler shows you a library's API. Every time a stack trace names a method. All metadata.

## E — Code walkthrough

```csharp
Assembly current = Assembly.GetExecutingAssembly();
AssemblyName name = current.GetName();
Console.WriteLine(current.FullName);
Console.WriteLine(string.Join(", ", current.GetModules().Select(m => m.Name)));
```

`GetExecutingAssembly()` returns the assembly containing the running code. `GetName()` reads
the identity out of the manifest. `GetModules()` reads the module table. No file is opened
and no source is consulted — the assembly is already loaded and it describes itself.

## F — Common confusion

- **"Reflection reads my source files."** It does not. Source isn't deployed and isn't
  needed.
- **"Metadata is debug information."** No. Debug info lives in a PDB and *is* optional.
  Metadata is in the assembly and isn't.
- **"A property is a field."** In metadata a property is a name bound to accessor
  **methods**. That's why `get_Salary` shows up in method lists.

## G — Questions

**"Does metadata make my assemblies much bigger?"** A modest fraction, and it isn't
optional, so there's no trade to make.

**"Can I read metadata without loading the assembly?"** Yes — `MetadataLoadContext`, or
`System.Reflection.Metadata` for the low-level reader. Use it for untrusted or
cross-architecture assemblies, because no code runs.

**"Does this mean anyone can see my code?"** Anyone with your DLL can read your API and
decompile your IL. Reflection isn't the exposure; shipping IL is. Obfuscation renames
things, but the structure remains.

---

# 2. Reflection — inspecting types and members

## A — Beginner

Reflection is the API for asking those questions. `typeof(Customer)` or
`customer.GetType()` gives you a `Type`. Ask the `Type` for its properties and you get a
`PropertyInfo` for each; ask a `PropertyInfo` to read a value off an object and it will.

## B — Technical

`System.Reflection` exposes the metadata tables as an object graph rooted at `Assembly`.
Everything derives from `MemberInfo`, which is why they all carry `Name`, `DeclaringType`
and `GetCustomAttributes`. The graph is fully navigable — `MethodInfo.ReturnType` is a
`Type`, which has members, which have types — and that closure is what makes generic
framework code possible.

The CLR keeps runtime type information for every loaded type, and every heap object points
at its own. That's how `GetType()` works on a bare `object` reference, and it's the same
bookkeeping that powers `is`, `as` and virtual dispatch. Reflection exposes work the runtime
must do anyway.

## C — Analogy

Reading a book's table of contents and index — you learn the chapter titles and the page
numbers without reading the prose.

**Where it breaks:** the index tells you a chapter called "Withdraw" exists on page 42. It
does *not* tell you what the chapter says. Metadata describes **signatures, not bodies** —
you cannot ask "does `Withdraw` validate its argument?"

## D — Practical

`System.Text.Json` walking your properties. AutoMapper matching names. xUnit finding
`[Fact]` methods. EF Core building a schema. Swagger generating an OpenAPI document.

## E — Code walkthrough

```csharp
Type type = instance.GetType();
foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    if (!property.CanRead) continue;
    object? value = property.GetValue(instance);
    Console.WriteLine($"{property.Name} = {value}");
}
```

`GetType()` gives the **runtime** type — if the variable is declared `object` but holds a
`SavingsAccount`, you get `SavingsAccount`. `GetProperties` reads the property table and
allocates a fresh `PropertyInfo[]` **on every call** — hoist it out of loops.
`CanRead` guards against write-only properties, where `GetValue` would throw.
`GetValue` invokes the getter through the runtime and boxes the result into `object`.

## F — Common confusion

- **`typeof(T)` vs `GetType()`** — static type vs runtime type. They disagree, and the
  disagreement is usually the point.
- **`==` on `Type`** instead of `IsAssignableFrom`, so inheritance is missed.
- **Not caching.** `GetProperties()` in a loop is the commonest performance bug.
- **Assuming member order is stable.** It isn't guaranteed; sort if you care.

## G — Questions

**"Are two `typeof(X)` calls the same object?"** Yes, within one load context — so `==` is
safe. Across load contexts they're genuinely different types.

**"Why both `MethodBase` and `MethodInfo`?"** `MethodBase` is what methods and constructors
share. `MethodInfo` adds `ReturnType`, which a constructor doesn't have.

**"Is it thread-safe?"** Reading is. Your caches around it are not — use
`ConcurrentDictionary`.

**"How do I scan every assembly in my app?"**
`AppDomain.CurrentDomain.GetAssemblies()` gives the *loaded* ones, and assemblies load
lazily — which is why scanning libraries make you pass assemblies explicitly.

---

# 3. BindingFlags and accessibility

## A — Beginner

By default, reflection shows you only public members — plus everything inherited. If a
member you know exists doesn't appear, you almost certainly need different `BindingFlags`.

## B — Technical

`BindingFlags` is a `[Flags]` enum filtering a member lookup. You must supply at least one
of `Public`/`NonPublic` **and** at least one of `Instance`/`Static`, or you get nothing.
Omitting the argument entirely means `Public | Instance | Static`, which includes inherited
members. `DeclaredOnly` restricts to the type itself; `FlattenHierarchy` adds inherited
statics.

`NonPublic` genuinely reaches private state. That is by design: accessibility is enforced by
the **compiler** at compile time. It is an encapsulation feature, not a runtime permission.

## C — Analogy

"Show hidden files" in a file explorer. The files were always there; the default view just
didn't list them.

**Where it breaks:** hidden files are hidden by a *convention* the OS could enforce.
`private` isn't even that — nothing at runtime refuses access. The information was never
protected, only unadvertised.

## D — Practical

Deserializers restoring objects with no public setters. ORMs materialising entities. Test
fixtures reaching otherwise-unreachable state. Diagnostic tools dumping object graphs.

## E — Code walkthrough

```csharp
FieldInfo? f = type.GetField("_auditTrail", BindingFlags.NonPublic | BindingFlags.Instance);
f?.SetValue(instance, "rewritten-by-reflection");
```

`GetField` with `NonPublic | Instance` finds the private field; without `NonPublic` it
returns `null`. `SetValue` writes it. No exception, no permission check — the object's
invariants are simply bypassed.

## F — Common confusion

- **"`GetMethods()` returned fourteen methods on a four-method class."** Inherited members
  plus property accessors. Add `DeclaredOnly`.
- **"`private` protects my secret."** It does not, from code in the same process.
- **"Static members are missing."** They need `BindingFlags.Static`; `Instance` excludes
  them.

## G — Questions

**"Should I unit-test private methods this way?"** Generally no — test the public surface,
or extract the unit. The pragmatic exception is a complex private algorithm, where
`InternalsVisibleTo` plus `internal` beats reflection.

**"So how do I actually protect something in-process?"** You can't. There is no supported
in-process sandbox in modern .NET. You need a process or OS boundary.

**"Is reading private state a security vulnerability in my app?"** Only if untrusted code is
running in your process — in which case that, not reflection, is the vulnerability.

---

# 4. Dynamic object creation — Activator

## A — Beginner

`new Customer()` needs the compiler to know about `Customer`. When all you have is a `Type`
you discovered at runtime, there's no name to type — so `Activator.CreateInstance` builds
the object for you.

## B — Technical

`Activator.CreateInstance` validates the `Type` (not null, not abstract, not an interface,
not an open generic), enumerates matching `ConstructorInfo`s, uses a `Binder` to select the
best match for the supplied argument types with coercions, allocates, runs the constructor,
and returns `object`.

Steps two and three are why it's slower than `new`, which the compiler reduces to a single
`newobj` IL instruction with the constructor already resolved.

## C — Analogy

A locksmith who can cut a key from a written description, versus simply having the key.

**Where it breaks:** the locksmith might cut the wrong key and you'd find out at the door.
That's actually the *right* intuition here — that's exactly what `MissingMethodException`
is. The analogy is unusually faithful.

## D — Practical

DI containers. Deserializers constructing your DTOs. Plugin hosts. EF Core materialising
entities. Test frameworks instantiating test classes.

## E — Code walkthrough

```csharp
Type? discovered = Type.GetType("Ns.FixedDepositAccount");
object? fd = Activator.CreateInstance(discovered!, "FD-12", 100000m, 24);

ConstructorInfo ctor = type.GetConstructor([typeof(string), typeof(decimal), typeof(decimal)])!;
var viaCtor = (SavingsAccount)ctor.Invoke(["SB-88", 250m, 0.03m]);
```

The first form searches constructors on **every call**. The second resolves the
`ConstructorInfo` once — cache that (or build a delegate from it) and you skip the search.
That's the shape every DI container uses internally.

## F — Common confusion

- **"Constructor not found" on an abstract type.** The message is misleading; the real
  problem is that it's abstract. Check `IsAbstract` yourself and say so.
- **Letting `TargetInvocationException` escape.** The real exception is in
  `InnerException`; unwrap with `ExceptionDispatchInfo` to keep the stack trace.
- **Expecting `Activator` to supply dependencies.** It knows nothing about your container.
  That's `ActivatorUtilities`.

## G — Questions

**"Is `Activator.CreateInstance<T>()` as fast as `new T()`?"** No — it still goes through the
activation machinery. Don't assume parity; measure if it matters.

**"Can it create a generic type at runtime?"** Yes, but close it first:
`typeof(List<>).MakeGenericType(typeof(int))`. An open generic throws.

**"What about a private constructor, like a singleton?"** `nonPublic: true` reaches it —
another reminder that a private constructor is a hint, not a guarantee.

---

# 5. Attributes

## A — Beginner

An attribute is a label you stick on a class, method or property. It doesn't *do* anything
by itself — it's just information. Something else has to come along, look for the label, and
decide what to do about it.

## B — Technical

An attribute is a class deriving from `System.Attribute`. Applying it writes a row into the
`CustomAttribute` metadata table recording the attribute **type** and its **constructor
arguments**. The attribute *object* is not stored — only the recipe. `GetCustomAttributes`
runs the constructor on demand, which is why two calls return two different instances.

`AttributeUsage` controls `ValidOn` (compiler-enforced), `AllowMultiple` (CS0579 if
violated) and `Inherited` (which needs the call site to pass `inherit: true` as well).
Attribute arguments must be compile-time constants, which is why complex configuration
pushes toward a fluent API.

## C — Analogy

Sticky notes on a filing cabinet. "Shred after 2027." The note doesn't shred anything — a
clerk has to read it and act.

**Where it breaks:** a sticky note can be peeled off and moved. Attributes are baked into
the assembly at compile time and cannot be added to a loaded type.

## D — Practical

`[HttpGet]` and `[Route]` → the routing table. `[Required]` → validation. `[JsonIgnore]` →
the JSON shape. `[Table]`/`[Column]` → the database schema. `[Fact]` → test discovery.
`[Obsolete]` → read by the **compiler** itself.

## E — Code walkthrough

```csharp
var range = property.GetCustomAttribute<ValidateRangeAttribute>();
if (range is not null && value is not null)
{
    double numeric = Convert.ToDouble(value);
    if (numeric < range.Minimum || numeric > range.Maximum) { /* report */ }
}
```

`GetCustomAttribute<T>()` constructs the attribute by replaying the stored arguments — so
`range.Minimum` is the `0.01` written at the use site. `null` means the property wasn't
annotated, and the engine moves on. The engine never names the model class, which is why
annotating a new class tomorrow needs no change here.

## F — Common confusion

- **"The attribute validates the property."** No. The engine does. The attribute is data.
- **Singular `GetCustomAttribute<T>()` on an `AllowMultiple` attribute** →
  `AmbiguousMatchException`.
- **Forgetting one of the two inheritance switches.** Both must agree.
- **Expecting interface-member attributes to be inherited by implementations.** They are
  not — check the interface explicitly.

## G — Questions

**"Why do two reads give different objects?"** Because only the constructor arguments are
stored; each read constructs a fresh instance. Cache if you need identity or speed.

**"Can an attribute have behaviour?"** You can put methods on it, but nothing calls them
unless your own code does. Keep attributes as data and the behaviour in the reader.

**"Attributes or a fluent API for my library?"** Offer both if you can. Attributes are
discoverable and sit next to the member; a fluent API keeps your types out of domain
classes and can express what constants cannot. EF Core supports both for exactly this
reason.

---

# 6. Plugin discovery and AssemblyLoadContext

## A — Beginner

You want people to extend your app by dropping a DLL into a folder. You publish a small
interface they implement. At startup your app scans the folder, loads each DLL, finds the
types that implement your interface, creates them, and runs them. Your app never mentions
their class name.

## B — Technical

`AssemblyLoadContext` is the unit of assembly isolation. A context created with
`isCollectible: true` can be unloaded. `AssemblyDependencyResolver` reads a plugin's
`.deps.json` to locate its private dependencies.

The crucial design decision is in `Load`: returning `null` for the **shared contract
assembly** defers to the default context, so the plugin's `IReportPlugin` and the host's are
the same CLR type. Type identity in .NET is **(assembly identity + load context + full
name)** — duplicate the contract and you get an `InvalidCastException` whose message names
the same type twice.

`Unload()` is a *request*. The context dies only when nothing reachable references it, its
assemblies, or any object whose type came from it.

## C — Analogy

Separate rooms in a shared house. Two tenants can each keep their own version of the same
appliance without a conflict — but they all have a key to the front door.

**Where it breaks:** people hear "isolation" and think "security". The rooms isolate
*belongings*, not *access*. Everyone can still reach the front door, the fuse box and the
safe. `AssemblyLoadContext` is **not a security sandbox**.

## D — Practical

Visual Studio and VS Code extensions. MSBuild tasks. Roslyn analyzers. dotnet CLI tools.
Any "drop a DLL in to add a feature" product.

## E — Code walkthrough

```csharp
protected override Assembly? Load(AssemblyName assemblyName)
{
    if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
        return null;                       // defer to Default -> SAME type identity

    string? path = _resolver.ResolveAssemblyToPath(assemblyName);
    return path is null ? null : LoadFromAssemblyPath(path);
}
```

The runtime calls `Load` whenever code in this context needs an assembly. Returning `null`
means "I decline — use the default context". That one branch is the difference between a
working plugin system and the baffling same-type cast error.

## F — Common confusion

- **"ALC sandboxes the plugin."** It does not. Full process privileges.
- **"`Unload()` unloads it."** It requests unloading. One cached `Type` or a subscribed
  event handler keeps the context alive forever.
- **"`Assembly.LoadFrom` is fine."** It loads into the default context: no isolation, no
  unloading. Fine for a trusted first-party DLL, wrong for a plugin model.

## G — Questions

See [`Audience-QA.md`](Audience-QA.md) #11 and #12 for the two big ones.

**"How do I vet a plugin before loading it?"** `MetadataLoadContext` — inspect types and
attributes without running any of its code. Then verify a strong name or Authenticode
signature, and log the path, version and hash of everything you load.

**"How do I version the contract?"** Adding a member to a shipped interface breaks every
existing plugin. Add a *new* interface (`IReportPluginV2`) and check for both, or prefer new
optional properties on the attribute, where old plugins take the defaults.

---

# 7. IL and the evaluation stack

## A — Beginner

Your C# doesn't compile to machine code. It compiles to an intermediate language — IL —
which is turned into machine code when the program runs. IL has no variables you can name;
every instruction pushes values onto a stack or pops them off.

## B — Technical

CIL (also MSIL, also just IL) is the stack-based instruction set defined by ECMA-335. Two
invariants govern a method body: at `ret` the evaluation stack must hold exactly the return
value, and operand types must suit the opcode.

The JIT compiles each method to machine code on first call. Variations: ReadyToRun
precompiles at publish with IL as a fallback; tiered compilation compiles hot methods more
than once; Native AOT ships no JIT at all; the Mono interpreter never compiles.

`ldarg.0` is the **first argument** in a static method and **`this`** in an instance method.

## C — Analogy

A cafeteria stack of plates. Each instruction either puts plates on top or takes plates off
the top. You can never reach into the middle.

**Where it breaks:** real IL also has *locals* (`ldloc`/`stloc`), which are more like a
shelf beside the stack. The stack model is the core, not the whole machine.

## D — Practical

You read IL far more often than you write it: understanding what a `record`, a lambda, or
`async` actually costs; diagnosing a closure capture; checking whether a struct got boxed.
ILSpy and sharplab.io make this a ten-second investigation.

## E — Code walkthrough

```text
ldarg.0     [ a ]          push argument 0
ldarg.1     [ a, b ]       push argument 1
add         [ a+b ]        pop two, push their sum
ret         [ ]            pop the return value and return
```

That is a complete `int Add(int a, int b)`. Four instructions, and the stack returns to
empty — which is exactly what makes it *valid*.

## F — Common confusion

- **`ldarg.0` numbering** on instance vs static methods. The single commonest Emit bug.
- **"IL is machine code."** No — portable, stack-based, fully typed. Machine code is none of
  those.
- **"`call` and `callvirt` are interchangeable."** `callvirt` resolves from the object's real
  type and null-checks the receiver. Using `call` for a virtual method silently invokes the
  wrong override.

## G — Questions

**"Is IL human-writable?"** `ilasm` accepts IL source and people do write it, but it's a
specialist skill. From a .NET program you use `ILGenerator`.

**"Why a stack machine rather than registers?"** Simpler to verify and to target from a
compiler, and it doesn't commit to a register count — leaving the JIT free to allocate real
registers for the actual CPU.

**"Can I see the IL for my own method?"** Yes — ILSpy, `ildasm`, or sharplab.io. Do this
before writing any IL: write the C# you want, read its IL, emit that.

---

# 8. DynamicMethod — lightweight code generation

## A — Beginner

You can build a single method at runtime, instruction by instruction, and then call it like
any other method. It's the cheapest way to generate code, and it's how libraries make
reflection-heavy work fast.

## B — Technical

A `DynamicMethod` is a method with **no declaring type**. `GetILGenerator()` gives you the
body writer; `Emit(OpCode, …)` appends instructions; `CreateDelegate<T>()` finalises it and
returns something callable. It is collectible, cannot be saved, and cannot define fields or
additional methods.

Its IL, when fully trusted, is **not type-verified** — so a type mismatch may silently
produce garbage rather than throw. Validation of structural errors (a missing `ret`) happens
when the **JIT** first compiles the method, i.e. on first call.

## C — Analogy

An adjustable spanner versus a single-purpose spanner you have machined on the spot.
Reflection is the adjustable one: works on anything, slower every time. A `DynamicMethod` is
the machined one: costs time once, then fits perfectly and turns fast.

**Where it breaks:** a badly machined spanner visibly doesn't fit. Badly emitted IL may fit
*silently and wrongly* — which is the one way this is worse than the physical world.

## D — Practical

`System.Text.Json`'s member accessors. EF Core's entity materialisers. DI container
factories. Dapper's row mappers. `RegexOptions.Compiled`.

## E — Code walkthrough

```csharp
var m = new DynamicMethod("Add", typeof(int), [typeof(int), typeof(int)]);
ILGenerator il = m.GetILGenerator();
il.Emit(OpCodes.Ldarg_0);   // [ a ]
il.Emit(OpCodes.Ldarg_1);   // [ a, b ]
il.Emit(OpCodes.Add);       // [ a+b ]
il.Emit(OpCodes.Ret);       // [ ]
var add = m.CreateDelegate<Func<int, int, int>>();
```

The name is cosmetic — stack traces only. The signature must match both the IL you emit and
the delegate type. `CreateDelegate` hands the IL to the runtime; the JIT compiles on first
call. **Cache `add`.** Regenerating per call is slower than the reflection it replaced.

## F — Common confusion

- **Generating per call.** The one mistake that makes this technique counterproductive.
- **Expecting `Emit` to validate.** It doesn't. The JIT does, on first call — and not even
  for type mismatches.
- **Wrong `Emit` overload** — `Ldc_I4_S` takes an `sbyte`.
- **Thinking this is the fastest option available.** Measured: a cached delegate from
  `MethodInfo.CreateDelegate` was identical. Try that first.

## G — Questions

See [`Audience-QA.md`](Audience-QA.md) #2, #7, #8.

**"Can it access private members?"** Yes, with the `restrictedSkipVisibility` overload —
which is how serializers set private backing fields.

**"Are dynamic methods garbage-collected?"** Yes, which is a real advantage over a
non-collectible dynamic assembly.

---

# 9. Runtime type generation — the builders

## A — Beginner

Sometimes a method isn't enough — you need a whole new *type*, because something expects an
object of a particular shape. A mock object has to actually *be* an `IFoo`. You can build
one at runtime.

## B — Technical

`AssemblyBuilder` → `ModuleBuilder` → `TypeBuilder` → `FieldBuilder` /
`ConstructorBuilder` / `MethodBuilder` / `PropertyBuilder`. Part 2's metadata hierarchy,
written instead of read. `TypeBuilder.CreateType()` bakes the type: before it, the type
cannot be instantiated; after it, nothing can be added.

On .NET 10 `AssemblyBuilderAccess` has exactly two members: `Run` and `RunAndCollect`.
`Save` and `RunAndSave` were .NET Framework and **do not exist**. To write a DLL, use
`System.Reflection.Emit.PersistedAssemblyBuilder` — in the shared framework, with
`Save(string)` and `Save(Stream)`. It is for *writing*; save, then load the file.

Every constructor must chain to a base constructor. An interface implementation must be
`Virtual`, and `DefineMethodOverride` binds it to the interface slot.

## C — Analogy

A 3D printer that prints a new tool at runtime — and prints it to fit a socket you already
own, so your existing wrench works on it.

**Where it breaks:** the printed tool is indistinguishable from a manufactured one to the
socket, but you have no blueprint to hand the next engineer. That's the maintainability
cost, and it's real.

## D — Practical

Moq, NSubstitute, FakeItEasy. Castle DynamicProxy. EF Core lazy-loading proxies.
`DispatchProxy` itself.

## E — Code walkthrough

```csharp
typeBuilder.AddInterfaceImplementation(typeof(IScaler));          // the key move
FieldBuilder f = typeBuilder.DefineField("_factor", typeof(int),
    FieldAttributes.Private | FieldAttributes.InitOnly);           // InitOnly IS readonly
// ...
ctorIl.Emit(OpCodes.Ldarg_0);
ctorIl.Emit(OpCodes.Call, objectCtor);                             // MUST chain to base
Type created = typeBuilder.CreateType();                           // bakes it
var scaler = (IScaler)Activator.CreateInstance(created, 7)!;        // then NORMAL calls
```

`AddInterfaceImplementation` is what lets the last line work. Reflection is used **once**, at
creation; from then on it's an ordinary interface call. That pattern is the whole reason this
technique is practical.

## F — Common confusion

- **`AssemblyBuilderAccess.Save`.** Doesn't exist. The single most repeated piece of stale
  advice in this area.
- **Forgetting the base constructor call.** Produces a type that fails confusingly.
- **Forgetting `Virtual`** on an interface implementation.
- **Regenerating a type per request.** Cache the `Type`.
- **Thinking a `Run` assembly is collectible.** It isn't — that's `RunAndCollect`. And a
  cached generated `Type` keeps the whole context alive.

## G — Questions

**"Can I save a generated assembly on .NET 10?"** Yes — `PersistedAssemblyBuilder`. The demo
writes a 2 KB DLL, reloads it with `Assembly.LoadFrom`, and invokes it.

**"Why would I want to save it?"** To *read* the IL you emitted in ILSpy, and to run ILVerify
over it. It turns IL debugging from guesswork into inspection.

**"Is it slower than a compiled type?"** Once JIT-compiled, no. The JIT neither knows nor
cares that the IL was generated. You pay at generation and first call.

---

# 10. Dependency injection

## A — Beginner

Your class says what it needs in its constructor. The container reads that list, builds each
thing, and hands you a finished object. You never write `new` for your services.

## B — Technical

A constructor signature *is* a machine-readable dependency list. The container reads
`GetConstructors()`, selects the **greediest constructor it can fully satisfy**, resolves
each parameter recursively, constructs, and caches by lifetime: singleton (per provider),
scoped (per scope — per HTTP request in ASP.NET Core), transient (per resolution).

`Microsoft.Extensions.DependencyInjection` picks an activation strategy at runtime. On
.NET 10 the provider's engine is `DynamicServiceProviderEngine`: a reflection engine first, a
faster compiled engine once a service has been resolved enough times, and reflection-only
when `RuntimeFeature.IsDynamicCodeCompiled` is false.

`ActivatorUtilities` bridges container services and caller-supplied arguments;
`CreateFactory` resolves the plan once for reuse.

## C — Analogy

Handing a recipe's ingredient list to a shopper, who fetches everything and hands you a full
basket.

**Where it breaks:** a shopper can substitute. The container cannot — if a dependency isn't
registered you get an exception, not a near-enough alternative. And the "greediest
satisfiable constructor" rule means the basket you get can change when *someone else* adds
an item to the shop.

## D — Practical

Every ASP.NET Core app. Every worker service. Any `IHost`-based application.

## E — Code walkthrough

```csharp
ConstructorInfo constructor = implementationType.GetConstructors()
    .OrderByDescending(c => c.GetParameters().Length).First();     // 1. reflection

object[] arguments = constructor.GetParameters()
    .Select(p => Resolve(p.ParameterType, resolving)).ToArray();   // 2. recursion

object instance = constructor.Invoke(arguments);                   // 3. construct
```

Three ideas: a dictionary from service type to implementation type, `GetConstructors()` to
discover needs, and recursion. A production container adds lifetimes, scopes, disposal,
thread safety, open generics, cycle detection and a compiled fast path — but that *is* the
core.

## F — Common confusion

- **"DI uses Reflection.Emit."** Depends on container, version and platform. "DI uses
  reflection" is the safe claim.
- **Captive dependency** — a singleton holding a scoped service. Enable `ValidateScopes`.
- **Surprise at which constructor ran.** Greediest satisfiable. One constructor per class
  avoids it.
- **Service locator** (`GetService` everywhere) instead of constructor injection.

## G — Questions

See [`Audience-QA.md`](Audience-QA.md) #4.

**"Is DI slow because of reflection?"** Resolution cost is dominated by graph size and
lifetime bookkeeping; the plan is cached after first use. Transients in a deep graph cost
more than the reflection ever did.

**"How do I activate a type that isn't registered?"**
`ActivatorUtilities.CreateInstance<T>(provider, extraArgs)` — and `CreateFactory` if it's on
a hot path.

---

# 11. ASP.NET Core MVC discovery

## A — Beginner

You write a controller class with `[HttpGet]` on a method, and the framework finds it. You
never register it. It finds it by looking through your assembly for classes that look like
controllers.

## B — Technical

At **startup**: `ApplicationPartManager` holds the search path (by default the entry
assembly, plus anything marked `[ApplicationPart]`). A feature provider populates a
`ControllerFeature` by scanning those parts. A class is a controller if it carries
`[Controller]`/`[ApiController]`, **or** its name ends with `"Controller"` — inheriting
`ControllerBase` is *not* the rule. Routing and binding attributes are then read into an
`ActionDescriptor` collection, and a cached invoker and model binders are built per action.

Per **request**: match the URL against the prepared route table, create the controller via
DI, bind parameters with the prepared binders, invoke through a cached delegate. **No type
scanning happens per request.**

## C — Analogy

A hotel compiling its room directory once, when it opens. Guests then get directed instantly
— nobody re-surveys the building per guest.

**Where it breaks:** a hotel can add a room and update the directory by hand. Changing MVC's
directory at runtime is possible but genuinely awkward — the normal model is "decide at
startup".

## D — Practical

Every ASP.NET Core API you have written. Also Swagger/OpenAPI generation, which reads the
same descriptor table.

## E — Code walkthrough

```csharp
var actionProvider = app.Services.GetRequiredService<IActionDescriptorCollectionProvider>();
foreach (ControllerActionDescriptor d in actionProvider.ActionDescriptors.Items
             .OfType<ControllerActionDescriptor>())
{
    // d.ControllerName, d.ActionName, d.AttributeRouteInfo?.Template, d.MethodInfo
}
```

This is the framework's own routing table, exposed. `d.MethodInfo` is a plain
`System.Reflection` `MethodInfo` — proof the discovery was reflective. Exposing this over
HTTP is, in miniature, what Swagger does.

## F — Common confusion

- **"`ControllerBase` makes it a controller."** It doesn't — the name or the attribute does.
- **"MVC reflects per request."** It doesn't. Startup only.
- **"A public method on a controller might not be an action."** It is an action *by default*;
  you opt out with `[NonAction]`.

## G — Questions

**"Does MVC use `MethodInfo.Invoke` per request?"** No — a faster cached invoker, built at
startup.

**"Can I add a controller at runtime?"** You can add an `ApplicationPart` and notify the
change provider, but it's advanced and fragile. Register candidate assemblies at startup.

**"Why do minimal APIs exist, then?"** Less startup reflection, less ceremony, and far
friendlier to AOT and source generation — convention-based discovery is exactly what AOT
dislikes.

---

# 12. EF Core — model building and LINQ translation

## A — Beginner

You write C# classes and a `DbContext`. EF reads your classes and works out what tables and
columns they correspond to. Then when you write a LINQ query, EF turns it into SQL and the
database does the work.

## B — Technical

**Model building (startup, cached):** EF reflects over the `DbSet<T>` properties to find root
entity types, then applies conventions, then data annotations, then your fluent
configuration — in that precedence order — producing an `IModel`.

**The model is not your classes.** It is a separate, richer structure: it can *exclude* CLR
members (`[NotMapped]`), treat some as navigations rather than scalars, and *add* members
that have no CLR property at all — shadow properties.

**Query translation:** `IQueryable<T>.Where` takes `Expression<Func<T,bool>>`, so the query
arrives as data. A chain of `ExpressionVisitor`s binds members to mapped properties, converts
navigations to joins, and translates method calls to provider-specific SQL. Translation is
cached per query *shape*. Materialisation uses accessor delegates compiled once per property.

## C — Analogy

A translator who reads the *structure* of your sentence and produces another language — not
a phrasebook that matches whole phrases.

**Where it breaks:** a human translator can paraphrase something with no direct equivalent.
EF **refuses**: if it can't translate your expression it throws, rather than silently
fetching the whole table. That refusal is deliberate, and it's a feature.

## D — Practical

Every EF Core query you've written. And the performance bug: `.ToList()` before `.Where()`
moves filtering from SQL into your process, after downloading every row.

## E — Code walkthrough

```csharp
context.Posts
    .Where(p => p.Rating >= 4 && p.Blog!.Title.Contains(".NET"))
    .Select(p => new { p.Title, Author = p.Author!.Name });
```

`p.Blog!.Title` becomes an `INNER JOIN`; the nullable `p.Author!.Name` a `LEFT JOIN`; the
anonymous type a `SELECT` list — so no `Post` entity is ever materialised.
`string.Contains` became `instr(...) > 0` on SQLite, **not** `LIKE`: the provider chooses.
Check with `ToQueryString()`, which produces the SQL without executing it.

## F — Common confusion

- **"The EF model is my classes."** It is a separate, richer model. Shadow properties prove
  it.
- **"EF uses reflection per row."** No — accessor delegates, compiled once.
- **"`.ToList().Where(...)` is the same as `.Where(...).ToList()`."** Same answer, radically
  different SQL.
- **"If it works on SQL Server it works on SQLite."** Translation is provider-specific.

## G — Questions

See [`Audience-QA.md`](Audience-QA.md) #13.

**"What's a shadow property for?"** Audit columns, timestamps, foreign keys you don't want on
the class. Read and written through the change tracker by name.

**"Why did my query throw 'could not be translated'?"** You used something with no SQL
equivalent — usually a local method. Rewrite it, or call `.AsEnumerable()` explicitly to mark
the boundary, or map a database function.

---

# 13. Serialization — reflection vs source generation

## A — Beginner

To turn an object into JSON, a library has to find out what properties it has and read each
one. It can do that at runtime by inspecting your class, or it can have the code written for
it during the build.

## B — Technical

**Reflection mode:** on first use per type, `System.Text.Json` reflects over the type and
builds a `JsonTypeInfo`, including fast member accessors (generated on a JIT runtime), then
caches the plan on the options instance — and in a process-wide cache shared between
*equivalent* options instances.

**Source-generated mode:** a Roslyn generator fills in a `partial` `JsonSerializerContext`
during the build, producing direct property access and a `JsonTypeInfo` per type —
transitively, for the whole object graph. No runtime reflection, trimming-safe, works under
Native AOT.

Both produce byte-identical output for the same model and settings.

## C — Analogy

Hiring an interpreter to attend your meeting, versus having the document translated in
advance. Both get you a translation; one costs time at the meeting.

**Where it breaks:** the interpreter can handle a topic nobody anticipated. The
pre-translated document cannot — which is exactly why source generation can't serialize a
type that didn't exist at build time.

## D — Practical

Every API response. Configuration binding. And the AOT story: if you're publishing Native
AOT or trimmed, source generation isn't an optimisation, it's a requirement.

## E — Code walkthrough

```csharp
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Invoice))]
internal partial class InvoiceJsonContext : JsonSerializerContext;

string json = JsonSerializer.Serialize(invoice, InvoiceJsonContext.Default.Invoice);
```

We write only the declaration; the generator writes the other half of the partial class. Note
the overload takes a `JsonTypeInfo<T>`, not `JsonSerializerOptions` — the plan is a
compile-time artifact passed in explicitly. Declaring one type produced five `JsonTypeInfo`
properties, because the generator walked the whole graph.

## F — Common confusion

- **"Source generators run at runtime."** They don't exist at runtime.
- **"Reuse your options or it re-reflects every call."** Only half right. Measured:
  new-but-identical options was 1.4x (a shared cache absorbs it); new options with
  **different** settings was 7.2x. Varying the settings is the expensive mistake.
- **"Unknown JSON properties cause an error."** By default they're silently ignored. Set
  `UnmappedMemberHandling = Disallow`.

## G — Questions

**"Which should I use?"** Source generation if the types are known at build time — which is
the usual case. Reflection mode for genuinely dynamic types, or broad polymorphism.

**"Why is deserialization harder?"** Writing needs a getter. Reading must *create* the
object: choose a constructor, match names to parameters, handle init-only members. It's the
same constructor-selection problem DI and `Activator` face.

**"Where else does this pattern appear?"** `[GeneratedRegex]` vs `RegexOptions.Compiled`,
`[LoggerMessage]`, the configuration binder generator. The direction of travel in .NET is
consistently build-time.

---

# 14. Dynamic proxies and cross-cutting concerns

## A — Beginner

You have a service, and you want logging around every method without putting logging code in
it. So you wrap it in an object that implements the same interface, logs, and forwards the
call. `DispatchProxy` builds that wrapper for you at runtime.

## B — Technical

`DispatchProxy.Create<T, TProxy>()` generates a type implementing `T` and deriving from
`TProxy`, routing every interface call to your `Invoke(MethodInfo, object?[])` override. It
is the generated-type pattern from topic 9, packaged.

Constraints: interfaces only; the base proxy class must not be `sealed` (the generated type
derives from it) and needs an accessible parameterless constructor; only calls **through the
interface** are intercepted, so a self-call inside the target is not; async returns a `Task`
immediately, so timing or catching requires awaiting it; every call pays
`targetMethod.Invoke`; and it needs dynamic code, so no Native AOT.

`Invoke` must unwrap `TargetInvocationException` with `ExceptionDispatchInfo`, or upstream
`catch` blocks stop matching.

## C — Analogy

A receptionist who takes every call, notes who rang and when, then puts it through.

**Where it breaks:** if someone inside the building phones another extension directly, the
receptionist never sees it. That is *exactly* the self-call blind spot — a genuinely faithful
part of the analogy, and the thing that breaks naive caching.

## D — Practical

Castle DynamicProxy via Autofac interceptors — the common production route. Moq and
NSubstitute. EF Core lazy-loading proxies. Audit and retry decorators.

## E — Code walkthrough

```csharp
protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
{
    Log.Add("--> " + targetMethod!.Name);
    try   { return InvokeTarget(targetMethod, args); }
    catch (Exception ex) { Log.Add("!!! " + ex.GetType().Name); throw; }
}
```

One method sees every call. `InvokeTarget` forwards and unwraps
`TargetInvocationException` — without that unwrapping, a `TimeoutException` from the target
arrives upstream as `TargetInvocationException` and every `catch (TimeoutException)` silently
stops matching.

## F — Common confusion

- **"It intercepts everything."** Only interface calls. Self-calls are invisible to it.
- **"Metaprogramming gave me caching."** It gave you the **seam**. The demo cache has no
  expiry, no size limit, no invalidation and no thread safety. Same for authorization: a
  proxy is *where* a policy runs, not the policy.
- **"Castle and `DispatchProxy` are equivalent."** Castle can also subclass a class — but
  then only intercepts **virtual** members.

## G — Questions

See [`Audience-QA.md`](Audience-QA.md) #14.

**"Should I use a proxy or a hand-written decorator?"** Decorator, more often than not.
Twenty lines of ordinary C#: debuggable, trimmable, AOT-safe, obvious. Reach for a generated
proxy when the number of interfaces makes hand-writing impractical.

**"Middleware, filter or proxy?"** By where the concern belongs. The test: would it still
apply if the call came from a background job instead of HTTP? If yes, it belongs at the
service seam.

---

# 15. Expression trees

## A — Beginner

Normally a lambda becomes compiled code you can call. But if you assign it to
`Expression<Func<...>>` instead of `Func<...>`, the compiler gives you a *description* of the
lambda — an object graph you can read, change, or translate into something else entirely,
like SQL.

## B — Technical

`Expression<TDelegate>` is an immutable object graph: `BinaryExpression`,
`MemberExpression`, `ConstantExpression`, `ParameterExpression`,
`MethodCallExpression` and friends. The C# compiler decides which to produce purely from the
**target type** of the lambda.

`Compile()` walks the tree, generates IL and JITs it — so it's expensive, and it needs
dynamic code support. But the tree can be **built and read** without a JIT, which is why EF
Core can translate queries under Native AOT.

`ExpressionVisitor` is the standard way to process a tree; because trees are immutable, a
visitor returns a *new* tree. And a tree is really a **graph with shared nodes** — the same
`ParameterExpression` object appears at every use site.

## C — Analogy

Sheet music versus a recording. You can transpose, analyse and rearrange sheet music. A
recording you can only play.

**Where it breaks:** sheet music and a recording are both complete. A tree is complete, but
its *translatability* is not guaranteed — some perfectly valid trees have no SQL equivalent,
and that failure appears only when a provider tries.

## D — Practical

Every EF Core query. `IQueryable` providers generally. LINQ to SQL-style libraries. Dynamic
filter builders driven by user input. Mapping libraries that compile converters once.

## E — Code walkthrough

```csharp
Func<int, bool>             d = n => n > 18;   // compiled IL — you can CALL it
Expression<Func<int, bool>> e = n => n > 18;   // object graph — you can READ it

ParameterExpression a = Expression.Parameter(typeof(int), "a");
Expression<Func<int, int, int>> lambda =
    Expression.Lambda<Func<int, int, int>>(
        Expression.Multiply(Expression.Add(a, b), Expression.Constant(2)), a, b);
```

Identical source text on the first two lines; the *type* decides everything. And building by
hand is **type-checked as you go** — `Expression.Add` on an `int` and a `string` throws
immediately with a clear message. Compare topic 8, where the IL equivalent silently returned
garbage. That is the main argument for preferring expression trees.

## F — Common confusion

- **Two different failures.** "Cannot be *expressed* as a tree" — statement bodies, `await`,
  assignment — is a **C# compile error**. "Cannot be *translated* to SQL" is a **provider
  exception at runtime**. Different fixes; easy to conflate.
- **"`Compile()` is cheap."** It is not. Compile once, cache the delegate.
- **"I can convert a `Func` back to an `Expression`."** You cannot. The structure is gone.
- **"Trees are strict trees."** Shared nodes. Matters when you write a visitor.

## G — Questions

See [`Audience-QA.md`](Audience-QA.md) #5.

**"Expression trees or Reflection.Emit?"** Trees, unless you need a *type*. Same speed class,
type-checked as you build, printable, and they fail loudly.

**"Why is `IQueryable`'s `Where` different?"** Only in the parameter type. That one
difference is what lets a provider translate instead of execute — and why an accidental
`.ToList()` changes where your filtering happens.

**"Can `Expression.Block` give me statements?"** The API can represent blocks and assignment.
The **C# lambda syntax** cannot — that's the restriction.

---

# The five sentences that carry the whole session

If you can say these five fluently and defend each, you are ready.

1. **"A .NET assembly is self-describing — metadata is required, not optional, and
   reflection just reads it."**
2. **"Discover once, cache, then execute compiled code."** (MVC, DI, EF Core, JSON — all of
   them.)
3. **"A cached delegate from `CreateDelegate` measured identical to emitted IL, so try that
   first."**
4. **"Expression trees for a function; Reflection.Emit only for a type."**
5. **"Metaprogramming trades compile-time safety for runtime flexibility — make that trade
   when you need the flexibility, not one step sooner."**

And the three corrections to defend:

- `AssemblyLoadContext` is **not** a security sandbox.
- `AssemblyBuilderAccess.Save` **does not exist** — use `PersistedAssemblyBuilder`.
- **"DI uses reflection"**, not "DI uses Reflection.Emit".
