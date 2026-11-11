# Glossary

Terms used in this session, defined precisely. Where a term is commonly misused, the
misuse is called out.

---

## A

**Activator** — `System.Activator`. Creates an instance from a `Type` object, choosing a
constructor by matching the supplied arguments. The bridge from "a `Type` I discovered"
to "a live object". Slower than `new` and type-checked only at run time.

**ActivatorUtilities** — `Microsoft.Extensions.DependencyInjection.ActivatorUtilities`.
Like `Activator`, but resolves constructor parameters from an `IServiceProvider` while
letting you supply the rest. Use `CreateFactory` to resolve the plan once and reuse it.

**Advice** *(AOP)* — the code an aspect runs at a join point: before, after, or around
the intercepted call. `CachingProxy.Invoke` is around-advice.

**AOP (Aspect-Oriented Programming)** — organising cross-cutting concerns (logging,
caching, authorization) into separate **aspects** instead of scattering them through
business code. In .NET, implemented with middleware, filters, proxies, decorators or IL
weaving.

**Application part** — an ASP.NET Core MVC abstraction for a source of controllers,
usually an assembly. The **search path** for discovery; adding one lets MVC find
controllers in another DLL.

**Assembly** — the unit of deployment, versioning and identity in .NET: a `.dll` or
`.exe` containing IL, metadata and a manifest. One or more modules.

**AssemblyBuilder** — `System.Reflection.Emit.AssemblyBuilder`. Defines an in-memory
dynamic assembly. **On .NET 10 it cannot save to disk** — use `PersistedAssemblyBuilder`.

**AssemblyBuilderAccess** — the enum controlling a dynamic assembly's capabilities.
**On .NET 10 it has exactly two members: `Run` and `RunAndCollect`.** `Save` and
`RunAndSave` were .NET Framework members and **do not exist**; code using them does not
compile.

**AssemblyLoadContext (ALC)** — an isolation boundary for assembly loading. Lets two
versions of a library coexist, and (when `isCollectible: true`) lets assemblies be
unloaded.
⚠ **Not a security sandbox.** It isolates **type identity and versions**, not
**privileges**. Loaded code runs with the host's full rights.

**Assembly-qualified name** — a type name including its assembly:
`System.Text.Json.JsonSerializerOptions, System.Text.Json`. **Required** by
`Type.GetType` for any type outside the calling assembly and `System.Private.CoreLib` —
the most common cause of an unexpected `null`.

**Attribute** — a class deriving from `System.Attribute`. Applying it stores a row in
the `CustomAttribute` metadata table.
⚠ **An attribute does nothing by itself.** It is inert declarative data until something
reads it.

**AttributeTargets** — `[Flags]` enum listing where an attribute may be applied
(`Class`, `Method`, `Property`, …). Violating it is a **compile error**.

**AttributeUsage** — the attribute applied *to* an attribute class, declaring `ValidOn`,
`AllowMultiple` and `Inherited`. Defaults (`All`, `false`, `true`) are rarely what you
meant, so declare it explicitly.

---

## B

**Backing field** — the compiler-generated field behind an auto-property. Reflection
shows it with an unspeakable name like `<AnnualRate>k__BackingField`, deliberately
chosen so your code cannot collide with it.

**BindingFlags** — the enum filtering which members a reflection lookup returns.
**Default with no flags is `Public | Instance | Static`.** A "missing" member is almost
always a `BindingFlags` mistake. You must specify at least one of `Public`/`NonPublic`
*and* one of `Instance`/`Static`.

**box / unbox** — IL instructions converting a value type to a heap object and back.
Boxing allocates, and in measurements it frequently dominates the cost people attribute
to reflection.

---

## C

**call / callvirt** — IL call instructions. `call` fixes the target at emit time (static
and non-virtual methods); `callvirt` resolves from the object's runtime type **and
null-checks the receiver** (virtual and interface methods). Using `call` where
`callvirt` is needed silently invokes the wrong override.

**Captive dependency** — a longer-lived service holding a shorter-lived one, classically
a singleton capturing a scoped service. Detected by
`ServiceProviderOptions.ValidateScopes`.

**CIL (Common Intermediate Language)** — the stack-based instruction set every .NET
language compiles to, specified in ECMA-335. Also called **MSIL** or just **IL**; the
three names mean the same thing.

**CLR (Common Language Runtime)** — the .NET execution engine: loading, JIT compilation,
garbage collection, type safety, exceptions.

**Collectible** — an assembly or load context that can be garbage-collected and
unloaded. Requires `AssemblyBuilderAccess.RunAndCollect` or
`AssemblyLoadContext(isCollectible: true)`.

**Convention (EF Core)** — a rule EF applies automatically when nothing configured a
detail (a property named `Id` becomes the key). Lowest precedence:
**convention < data annotation < fluent API**.

**Cross-cutting concern** — a requirement spanning many modules — logging, caching,
authorization, metrics, retries — orthogonal to business logic but easily tangled into
it.

**CustomAttributeData** — `GetCustomAttributesData()` returns the raw attribute metadata
rows **without constructing the attributes**. Use it for untrusted assemblies (no
constructor code runs) and tooling.

---

## D

**Decorator** — an object implementing an interface and wrapping another implementation
of it, adding behaviour. A **hand-written decorator is usually preferable to a generated
proxy**: debuggable, trimmable, AOT-safe, obvious.

**DispatchProxy** — `System.Reflection.DispatchProxy`. Generates a type at run time that
implements an interface and routes **every** call through your `Invoke` override.
⚠ Interfaces only; the base proxy class must not be `sealed`; self-calls inside the
target are **not** intercepted; async needs explicit handling; unavailable under Native
AOT.

**DynamicMethod** — a single method with **no declaring type**, created at run time. The
cheapest form of runtime code generation; collectible; cannot be saved; its IL is **not
type-verified** when fully trusted.

---

## E

**ECMA-335** — the standard specifying the Common Language Infrastructure: IL, the
metadata format, and the PE layout.

**Evaluation stack** — the stack every IL instruction operates on. Two rules: it must
hold exactly the return value at `ret`, and operand types must suit the opcode.

**Expression tree** — code represented as an immutable, inspectable object graph
(`Expression<TDelegate>`). Can be read, rewritten, compiled, or **translated** (to SQL,
to a URL). The reason `IQueryable` can target a database.
⚠ Technically a **graph with shared nodes**, not a strict tree — one
`ParameterExpression` object appears at every use site.

**ExpressionVisitor** — the base class for walking or rewriting an expression tree.
Trees are immutable, so a visitor returns a **new** tree. EF Core's query pipeline is a
chain of visitors.

---

## F – I

**Fluent API (EF Core)** — configuring the model in `OnModelCreating` instead of with
attributes. **Highest precedence.** Keeps framework types out of domain classes and
expresses things attributes cannot (attribute arguments must be compile-time constants).

**ILGenerator** — the writer that appends IL instructions to a `DynamicMethod` or
`MethodBuilder` body, via `Emit(OpCode, …)`.

**ILVerify** — a .NET tool that validates the IL in an assembly. Pair it with
`PersistedAssemblyBuilder` to check emitted IL properly, instead of waiting for
`InvalidProgramException` on first call.

**Implicit vs explicit metadata** — a **teaching distinction**, not a CLR category,
about *who* produced the metadata: the compiler (implicit, from your declarations) or
you (explicit, via attributes). The CLR has only metadata tables, one of which is
`CustomAttribute`.

**Introspection** — reading a program's own structure at run time. In .NET, reflection.

**InvalidProgramException** — thrown when the JIT finds invalid IL, **on first call**,
not when you emit it.

**IQueryable&lt;T&gt;** — a query whose operators take **`Expression<Func<…>>`** rather
than `Func<…>`, so a provider receives the query as **data** and can translate it. That
single type difference is the whole LINQ-provider mechanism.

---

## J – M

**JIT (Just-In-Time compiler)** — compiles IL to machine code per method, at first call.
Why generated IL runs at full speed, and why Native AOT (no JIT) cannot run it.
Note that **tiered compilation** means a method may be compiled more than once.

**Join point** *(AOP)* — a place where an aspect could apply, typically a method call.

**ldarg** — the IL instruction family that pushes an argument.
⚠ **In a static method `ldarg.0` is the first argument; in an instance method it is
`this`** and the first argument is `ldarg.1`. The most common beginner Emit bug.

**ldc.i4** — the IL family that pushes an `int` constant, in 1-, 2- or 5-byte encodings
(`ldc.i4.0`…`ldc.i4.8`, `ldc.i4.s`, `ldc.i4`). All push the same value.

**Manifest** — the part of an assembly's metadata recording its own identity, its
referenced assemblies and their expected versions, and its resources.

**MemberInfo** — the abstract base of `Type`, `MethodInfo`, `ConstructorInfo`,
`PropertyInfo`, `FieldInfo` and `EventInfo`. Why they all have `Name`, `DeclaringType`
and `GetCustomAttributes`.

**Metadata** — the structured binary description of every type and member, emitted by
the compiler into the assembly. **Required, not optional** — the CLR needs it. This is
what makes .NET assemblies self-describing and reflection possible.

**MetadataLoadContext** — a reflection-like API for inspecting assemblies **without
executing any of their code**. The right tool for vetting a plugin before loading it.

**Metaprogramming** — code that operates on program structure or behaviour rather than
on data.

**Middleware** — an ASP.NET Core pipeline component handling every HTTP request. The
right seam for concerns about *the request* (auth, CORS, request logging) rather than
about a particular method.

**Module** — a physical container of types within an assembly. Multi-module assemblies
are legal but essentially unused in modern .NET, so assembly ≈ module in practice.

**MSIL** — see **CIL**.

---

## N – P

**Native AOT** — publishing a .NET app compiled fully ahead of time, with no JIT.
⚠ **`Reflection.Emit`, `DispatchProxy` and `Expression<T>.Compile()` do not work.**
`RuntimeFeature.IsDynamicCodeSupported` is `false`. Note that expression trees can still
be **built and read** under AOT — only `Compile()` needs the JIT.

**newobj** — the single IL instruction that allocates an object, runs its constructor
with the popped arguments, and pushes the reference. What `new`, `Activator`, a DI
container and a deserializer all ultimately perform.

**NonAction** — `[NonAction]` excludes a public controller method from routing. Needed
because a public method on a controller is an action **by default**.

**PE file (Portable Executable)** — the Windows executable format. A managed assembly is
a PE file with extra sections holding the CLI header, metadata streams and IL.

**PersistedAssemblyBuilder** — `System.Reflection.Emit.PersistedAssemblyBuilder`. **How
you save a generated assembly on modern .NET.** In the shared framework (no package
needed). `Save(string)` / `Save(Stream)`. For **writing**, not running — save, then load
the file. Verified here: wrote a 2 KB DLL, reloaded it, invoked it.

**Pointcut** *(AOP)* — the rule selecting which join points an aspect applies to. In
example 12, the `[Cacheable]` attribute is the pointcut.

**Property** — ⚠ **not a storage location.** Metadata binding a name to getter and/or
setter **methods**. Those accessors are real methods, flagged `IsSpecialName`.

**Proxy** — an object standing in for another, implementing the same interface and
forwarding calls, with behaviour added around them.

---

## R – S

**ReadyToRun (R2R)** — AOT compilation at publish time with IL kept as a fallback, so
the JIT can still re-optimise. Improves startup without the restrictions of Native AOT.

**Reflection** — the `System.Reflection` API for reading metadata and invoking members
at run time.

**Reflection.Emit** — the `System.Reflection.Emit` API for generating new IL at run time.

**ReflectionTypeLoadException** — thrown by `Assembly.GetTypes()` when any type fails to
load. ⚠ Its `.Types` property still holds the ones that **did** load. Any host scanning
assemblies it did not build must catch this.

**ret** — the IL instruction that pops the return value (if any) and returns. **Omitting
it produces invalid IL**, discovered on first call.

**RuntimeFeature.IsDynamicCodeCompiled** — whether dynamically created code will be
**JIT-compiled**. `false` on Mono interpreter builds: Emit *works* but runs interpreted,
so the speed benefit disappears.

**RuntimeFeature.IsDynamicCodeSupported** — whether runtime code generation is possible
at all. `false` under Native AOT. **Library code that emits IL should check this and keep
a reflection fallback.**

**Service lifetime** — how long a DI container reuses an instance: **singleton** (one per
provider), **scoped** (one per scope — one per HTTP request in ASP.NET Core), or
**transient** (a new one every resolution).

**Shadow property** — an EF Core property that exists **in the model but not on the CLR
class**, read and written through the change tracker by name. The cleanest proof that
the EF model is a separate, richer model rather than a view over reflection.

**Source generator** — a Roslyn component that adds C# source **during the build**. Zero
run-time cost, fully type-checked, trimming- and AOT-safe. ⚠ Does not exist at run time,
and cannot handle types unknown at build time.

---

## T – W

**TargetInvocationException** — the wrapper around any exception thrown *inside* a
reflectively invoked member. ⚠ **Always unwrap it**, or every `catch` of the real
exception type upstream silently stops matching. Use
`ExceptionDispatchInfo.Capture(ex.InnerException).Throw()` — not
`throw ex.InnerException`, which resets the stack trace.

**Tiered compilation** — the runtime compiling a method quickly first (tier-0) and
re-compiling hot methods optimised (tier-1). Why benchmark warm-up loops matter.

**Trimming** — removing unused IL to shrink a deployment. ⚠ The trimmer **cannot see
reflective usage**, so it may remove members your reflection needs. Annotate with
`DynamicallyAccessedMembers`, use source generators, or do not trim.

**TypeBuilder** — the builder that defines a type at run time. `CreateType()` "bakes"
it: before that call it cannot be instantiated; after it, nothing can be added.

**Type identity** — what makes two types the same to the CLR:
**assembly identity + load context + full name**. Not just the name — which is why a
duplicated contract assembly produces an `InvalidCastException` naming the same type
twice.

**Weaving** *(AOP)* — combining aspects with code: at compile time, post-compile (IL
weaving, as PostSharp/Fody do), or at run time (proxies). `DispatchProxy.Create` is
run-time weaving.

---

## Symbols you will see in reflection output

| Appearance | Meaning |
|---|---|
| `List\`1` | a generic type with 1 type parameter (backtick + arity) |
| `<AnnualRate>k__BackingField` | the compiler's backing field for an auto-property |
| `<>c`, `<>c__DisplayClass0_0` | compiler-generated closure classes for lambdas |
| `<GetAll>d__3` | a compiler-generated async/iterator state machine |
| `b__1_0` | a compiler-generated lambda method |
| `Outer+Inner` | a nested type — note the **`+`**, not a `.` |
| `.ctor` / `.cctor` | an instance / static constructor |
| `get_Salary` / `set_Salary` | property accessor methods |
| `generatedProxy_1` | a `DispatchProxy`-generated type |
| `<In Memory Module>` | the module of a dynamic assembly |
