# Interview Questions

Technically accurate answers, with the depth an interviewer is usually probing for.
Every factual claim about .NET 10 behaviour here was verified by running the code in
[`Code-Examples`](../Code-Examples).

Difficulty: 🟢 fundamentals · 🟡 working knowledge · 🔴 deep

---

## Fundamentals

### 🟢 1. What is metaprogramming?

Code that operates on program structure or behaviour rather than on data. In .NET it
takes three forms: **compile-time generation** (Roslyn source generators),
**runtime introspection** (Reflection) and **runtime code generation**
(`Reflection.Emit`, expression trees).

**Follow-up — why is it possible in .NET at all?** Because every managed assembly is
**self-describing**: the compiler emits metadata tables (`TypeDef`, `MethodDef`,
`CustomAttribute`, …) alongside the IL, and the CLR needs them anyway. Reflection reads
tables that are already there.

### 🟢 2. What is reflection, and when would you use it?

The `System.Reflection` API for inspecting metadata and invoking members at run time.

Use it when **the set of types is not known when you write the code** — plugins,
serializers, DI containers, test runners, ORMs.

Do **not** use it when the type is known. Generics, interfaces or a source generator are
faster, type-checked and clearer.

### 🟢 3. Difference between `typeof(T)`, `GetType()` and `Type.GetType(string)`?

| | Resolved | Checked | Returns |
|---|---|---|---|
| `typeof(T)` | compile time | **yes** | the `Type` for a static type |
| `obj.GetType()` | run time | n/a | the **runtime** type of an instance |
| `Type.GetType("…")` | run time | no | the type, or **`null`** |

They differ. If a variable is declared `object` but holds a `SavingsAccount`,
`GetType()` returns `SavingsAccount`.

### 🟢 4. What is an attribute? Does it do anything by itself?

A class deriving from `System.Attribute`. Applying it writes a row into the
`CustomAttribute` metadata table.

**It does nothing by itself.** It is inert declarative data. `[JsonIgnore]` has no
behaviour; `System.Text.Json` *looks for it* and decides to skip the property. If
nothing reads an attribute, it is a comment the compiler stores.

**This is the most common misconception about attributes** and a very common interview
question.

### 🟢 5. `Activator.CreateInstance` vs `new`?

`new` is one `newobj` IL instruction with the constructor resolved at compile time:
type-checked, fastest. `Activator.CreateInstance` searches constructors at run time,
binds `object[]` arguments, then constructs: no type checking, much slower, and every
failure is a run-time failure.

Use `Activator` **only** when the type genuinely is not known at compile time.

---

## Working knowledge

### 🟡 6. Is reflection slow?

**Yes per call, and usually irrelevant in absolute terms.** Measured here (Release,
2,000,000 reads of one `decimal` property):

```text
  direct C# property access                     8 ms    1.0x
  PropertyInfo.GetValue                        67 ms    8.4x
  MethodInfo.Invoke                            59 ms    7.4x
  cached delegate (typed)                      10 ms    1.2x
  emitted IL (typed, no box)                   10 ms    1.2x
```

So ~7–8×. But 67 ms for **two million** reads is about **33 nanoseconds** each. Once per
web request: free. Per row of a million-row import: your bottleneck.

**The real answer an interviewer wants:** the cost is **per call**, and the fix is
always the same — **do the reflection once, cache a delegate, then call compiled code**.
That is what MVC, EF Core, `System.Text.Json` and the DI container all do.

**Why is it slow?** Per call it re-does member lookup, access checks, argument binding,
boxing of arguments into `object[]`, and unboxing the result.

### 🟡 7. Is `Reflection.Emit` faster than reflection?

**The generated code is** — roughly the same speed as direct access. **Generating it is
not**: creating and JIT-ing a method costs far more than one reflective call.

So it wins only if you amortise over many calls (order of thousands).

**The answer that shows judgement:** in the measurement above, a **cached delegate from
`MethodInfo.CreateDelegate` was exactly as fast as emitted IL** (both 1.2×). So try
`CreateDelegate` first — you get emit-level speed with **no IL at all**. Reach for Emit
only when the shape of the call is not fixed at compile time.

### 🟡 8. Does .NET use reflection internally?

Extensively, and almost always at **startup or first use**, then cached:

| Framework | Reflects over | When |
|---|---|---|
| ASP.NET Core MVC | controllers, actions, routing attributes | startup |
| DI container | constructor parameters | startup / first resolve |
| EF Core | `DbSet<T>` properties, annotations | first use, then cached |
| `System.Text.Json` | properties and attributes | first use per type, cached |
| xUnit / NUnit | `[Fact]` / `[Test]` methods | discovery |
| Moq / Castle | generates proxy **types** | first use |

The important nuance: **it is used for *discovery*, not for *execution* on the hot
path.** MVC does not call `MethodInfo.Invoke` per request; it builds a cached invoker at
startup.

### 🟡 9. Does dependency injection always use reflection?

**No.** Distinguish the pattern from the container.

- **Constructor injection** needs no reflection — you can wire an app by hand with
  `new`.
- **Containers** use reflection to read constructor signatures, so you do not have to
  wire it yourself.
- Some containers then **generate code** to call those constructors fast; some do not;
  compile-time DI containers use no reflection at all.

Verified for `Microsoft.Extensions.DependencyInjection` on .NET 10 by reading the
provider's private `_engine` field:

```text
ServiceProvider's internal engine on THIS runtime: DynamicServiceProviderEngine
```

It picks a strategy at run time: a reflection-based engine first, a faster
expression-tree/IL-emitted engine once a service has been resolved enough times, and the
reflection-only engine when `RuntimeFeature.IsDynamicCodeCompiled` is `false` (Native
AOT).

> **"DI uses reflection" is the safe claim. "DI uses Reflection.Emit" is not** — it
> depends on the container, its version and the platform.

### 🟡 10. Can reflection access private methods and fields?

**Yes**, with `BindingFlags.NonPublic`:

```csharp
FieldInfo? f = type.GetField("_auditTrail", BindingFlags.NonPublic | BindingFlags.Instance);
f?.SetValue(instance, "changed");
```

**And the important part of the answer:**

> **`private` is a compiler rule, enforced at compile time. Accessibility is an
> encapsulation feature, not a security boundary.**

Code in your process can read any field of any object. There is no supported in-process
sandbox in modern .NET — Code Access Security and AppDomain partial trust were .NET
Framework features and have been removed. If something must be protected from code in
the same process, you need a process or OS boundary.

**Legitimate uses:** deserialization, ORM materialisation, test fixtures, diagnostics.
**Illegitimate use:** routing around another library's invariants — it breaks on their
next patch, because private members are part of no contract.

### 🟡 11. Reflection vs expression trees?

| | Reflection | Expression trees |
|---|---|---|
| Represents | **declarations** — what members exist | **code** — what an operation does |
| Example answer | "`Customer` has an `Age` property" | "someone wrote `c.Age > 18`" |
| Can execute | yes, slowly (`Invoke`) | yes, after `Compile()` — fast |
| Can be translated | no | **yes — this is how EF Core makes SQL** |
| Type-checked as built | n/a | **yes — factory methods throw immediately** |

They are commonly used **together**: a dynamic filter builder uses reflection
(`GetProperty`) to find the member, then builds an expression tree around it.

### 🟡 12. `DynamicMethod` vs `MethodBuilder`?

| | `DynamicMethod` | `MethodBuilder` |
|---|---|---|
| Declaring type | **none** | a `TypeBuilder` |
| Creates | one method | one method **of a type** |
| Fields, several methods, interfaces | no | **yes** |
| Collectible | **yes** | only with `RunAndCollect` |
| Savable | no | yes, via `PersistedAssemblyBuilder` |
| Ceremony | ~5 lines | ~60 lines |
| Right for | accessors, converters, serializer fast paths | **proxies, mocks** — anything that must *be* a type |

The deciding question: **do you need a method, or a type?** A mock must *be* an `IFoo`
to be passed where an `IFoo` is expected — only type generation can do that.

---

## Deep

### 🔴 13. What happens when generated IL is invalid?

**It depends, and the asymmetry is the point.** Both cases verified:

**Case A — a missing `ret`:**

```text
  CreateDelegate succeeded - nothing has been verified yet.
  Calling it...
  InvalidProgramException on FIRST CALL: Common Language Runtime detected an invalid program.
```

Not at `Emit`, not at `CreateDelegate`, but when the **JIT first compiles** the method.
Your bug surfaces far from the line that caused it.

**Case B — `add` on an `int` and a string reference:**

```text
  No exception. broken(1) returned 299021057, and again 299021057.
```

**No error at all.** A plausible-looking garbage number — the string's managed reference
added to 1 as an integer.

> **IL emitted by a fully-trusted `DynamicMethod` is not type-verified.** A mistake can
> silently produce garbage instead of failing, and memory-unsafe IL can corrupt process
> state rather than throw.

**How to debug it:** write the C# first and read its IL (ILSpy, sharplab.io); track
expected stack depth as you emit; generate with `PersistedAssemblyBuilder`, `Save`, and
run **ILVerify**; keep emitted methods tiny and `call` into real C#. Or use expression
trees, where `Expression.Add(intNode, stringNode)` throws immediately.

### 🔴 14. Difference between IL and machine code?

| | IL | Machine code |
|---|---|---|
| Target | the CLR's virtual machine | a specific CPU |
| Model | **stack-based** | register-based |
| Type information | **full** | none |
| Portability | one assembly runs everywhere | per architecture |
| Produced by | the C#/F#/VB compiler | the **JIT**, or an AOT compiler |

**Why the extra step?** One assembly runs on x64, Arm64, Windows, Linux and macOS, and
the JIT can specialise for the actual CPU — using AVX-512 where available without you
shipping a separate build. Being verifiable and type-rich also lets the runtime enforce
memory safety.

**Nuance worth adding:** the pipeline is not always "IL → JIT". **ReadyToRun**
pre-compiles at publish time with IL as a fallback; **tiered compilation** compiles a
method *more than once* (tier-0 fast, tier-1 optimised for hot methods); **Native AOT**
ships no JIT at all; the **Mono interpreter** interprets IL.

### 🔴 15. Can `Reflection.Emit` work with Native AOT?

**No.** Under Native AOT there is no JIT, so there is nothing to compile generated IL.
`RuntimeFeature.IsDynamicCodeSupported` is `false` and the `Reflection.Emit` APIs throw
`PlatformNotSupportedException`.

Also unavailable: `DispatchProxy`, `Expression<T>.Compile()`, and any library depending
on them (Moq, Castle DynamicProxy, reflection-mode `System.Text.Json` warns and may
fail).

**Two flags, and they are different:**

```csharp
RuntimeFeature.IsDynamicCodeSupported   // can I create code at all?
RuntimeFeature.IsDynamicCodeCompiled    // will it be JIT-compiled to machine code?
```

| Platform | Supported | Compiled | Effect |
|---|---|---|---|
| CoreCLR | `true` | `true` | works, fast |
| **Native AOT** | **`false`** | `false` | **throws** |
| Mono interpreter (iOS) | `true` | **`false`** | works, but **interpreted** — no speed benefit |

**What to do instead:** source generators. `[GeneratedRegex]`, `JsonSerializerContext`,
`[LoggerMessage]` and the configuration binder generator all exist precisely to move this
work to build time.

**The library-author rule:** check `IsDynamicCodeSupported` and keep a reflection-based
fallback. That is exactly what `System.Text.Json` and the DI container do.

**A precision point interviewers like:** expression trees can still be **built and
read** under AOT — which is why EF Core can translate LINQ to SQL there, since
translation only reads the tree. It is `Compile()` that needs the JIT.

### 🔴 16. Can a plugin be loaded without restarting the application?

**Loading: yes.** `AssemblyLoadContext` + `LoadFromAssemblyPath` + reflection discovery +
activation. Verified in example 04.

**Replacing or unloading: much harder.** You need a **collectible** context
(`isCollectible: true`), you must drop **every** reference, call `Unload()`, and then
wait — because `Unload()` is a **request, not an action**. Verified:

```text
Unload() has been requested. Now the GC has to actually collect it.
  collection attempt 1: alive = True
  collection attempt 2: alive = False
```

**What keeps a context alive** — all common mistakes:

- any live instance of a plugin type;
- a cached `Type`, `MethodInfo` or `Assembly`;
- a delegate pointing at plugin code (an event handler left subscribed);
- a static field in the **host** holding any of the above.

The example needs `[MethodImpl(MethodImplOptions.NoInlining)]` on its helper, because an
inlined method's locals could stay rooted and the context would never collect. That is
how delicate it is.

Also: unloading frees **managed state only**. It cannot undo side effects, and a plugin
that P/Invoked a native library may have pinned it.

**What production systems usually do:** run plugins in a **child process** and restart
that. Far more robust than in-process unloading.

### 🔴 17. Is `AssemblyLoadContext` a security boundary?

**No — and this is the most important "no" in the whole topic.**

> **`AssemblyLoadContext` is an isolation boundary for TYPE IDENTITY and VERSIONS.**
> **It is NOT a security sandbox.**

Once you call `LoadFromAssemblyPath`, that code runs with your process's **full
privileges**. It can read and write any file your process can, open sockets, start
processes, read environment variables and secrets, reflect against your private state,
and call `Environment.Exit`.

**There is no supported in-process sandbox in modern .NET.** CAS and AppDomain partial
trust were .NET Framework features and are gone.

**For untrusted code:** isolate where the OS can enforce it — a separate process with a
reduced token, a container, a VM, or a WASM runtime.

**For a trusted plugin model, harden it anyway:** verify a strong name or Authenticode
signature before loading; keep the contracts assembly tiny and versioned; vet with
`MetadataLoadContext` first (no plugin code runs); wrap every plugin call in
`try`/`catch`; log the path, version and hash of everything loaded.

**What ALC *is* good for** — and the failure that proves it: if the plugin context loads
its own copy of the shared contract assembly, the plugin implements a *different*
`IReportPlugin` and the cast fails with an `InvalidCastException` whose message **names
the same type twice**. Type identity in .NET is **(assembly identity + load context +
full name)**.

### 🔴 18. How does EF Core translate LINQ into SQL?

The query arrives as **data**, not code, because `IQueryable<T>.Where` takes
`Expression<Func<T,bool>>`, not `Func<T,bool>`. That single type difference is the whole
mechanism.

**The pipeline:**

1. LINQ operators build an **expression tree**; nothing executes.
2. EF runs a chain of `ExpressionVisitor`s: identify the query root; bind member
   accesses to **mapped** properties and navigations; convert navigations to joins;
   translate method calls to provider-specific SQL functions; apply global query filters;
   produce a relational expression tree.
3. The provider prints SQL.
4. Results are materialised with **cached accessor delegates** (compiled once per
   property, not `PropertyInfo.GetValue` per row).
5. The translation is **cached per query shape**, so a loop with different parameter
   values translates once.

Verified:

```sql
SELECT "b"."Title", "b0"."Title" AS "Blog", "a"."full_name" AS "Author"
FROM "blog_posts" AS "b"
INNER JOIN "Blogs" AS "b0" ON "b"."BlogId" = "b0"."Id"
LEFT JOIN "authors" AS "a" ON "b"."AuthorId" = "a"."AuthorKey"
WHERE "b"."Rating" >= 0 AND "b"."Rating" >= 4 AND instr("b0"."Title", '.NET') > 0
ORDER BY "b"."PublishedOn" DESC
```

Note: `string.Contains` became **`instr(...) > 0`**, a **SQLite** function — not `LIKE`.
**The provider chooses the translation**, so always check with `ToQueryString()`.

**Why some queries fail:** EF **refuses** rather than silently downloading the table:

```text
The LINQ expression '...Where(p => Program.IsProofRead(p.Content))' could not be translated
```

That refusal is deliberate — the silent version was older EF Core behaviour and caused
production incidents.

**Keep two failures apart:**

- *"cannot be EXPRESSED as a tree"* → a **C# compile error** (statement bodies,
  `await`, assignment).
- *"cannot be TRANSLATED to SQL"* → a **provider exception at run time**.

### 🔴 19. What are the limitations of dynamic proxies?

All verified with `DispatchProxy` on .NET 10:

1. **Interfaces only.**
   `ArgumentException: The type 'AccountService' must be an interface, not a class.`
   Castle DynamicProxy can subclass a class, but then only intercepts **virtual**
   members.
2. **The proxy base class must not be `sealed`** — the generated type derives from it.
   It also needs an accessible parameterless constructor.
3. **Only calls through the interface are intercepted.** Crucially, **a self-call inside
   the service is not intercepted**, because `this` is the real object, not the proxy.
   This blind spot breaks naive caching and surprises everyone once.
4. **Async needs deliberate handling.** `Invoke` returns `object?`; for an `async`
   method that is a `Task` returned immediately, so a naive stopwatch measures only how
   long it took to *create* the task. You must inspect the return type and handle
   `Task`, `Task<T>` and `ValueTask<T>`.
5. **Per-call cost** — every call goes through `targetMethod.Invoke`, the slow path.
6. **No Native AOT.**
7. **Debuggability** — stack traces run through a generated type; "go to definition" no
   longer reaches the code that runs.

**And the exception detail that catches people:** `targetMethod.Invoke` wraps target
exceptions in `TargetInvocationException`. Let it escape and every
`catch (TimeoutException)` upstream silently stops matching. Unwrap with
`ExceptionDispatchInfo.Capture(ex.InnerException).Throw()` — not
`throw ex.InnerException`, which resets the stack trace.

**The senior answer:** a **hand-written decorator** is right more often than a generated
proxy. Twenty lines of ordinary C#: debuggable, trimmable, AOT-safe, obvious. Use a
generated proxy when the number of interfaces makes hand-writing impractical.

---

## Design judgement — the questions that separate levels

### 🔴 20. When would you NOT use metaprogramming?

1. **The type set is closed** → generics or a source generator.
2. **It runs once** → generation costs more than it saves.
3. **One implementation** → a reflective "engine" with one caller is a method with extra
   steps.
4. **The team will not maintain it** → IL and visitor chains are write-mostly.
5. **You are shipping Native AOT** → much of it does not run.
6. **The failure mode matters** → a compile error at 3pm beats a
   `NullReferenceException` at 3am.

> **Metaprogramming trades compile-time safety for run-time flexibility. Make that trade
> when you need the flexibility, and not one step sooner.**

### 🔴 21. Source generators or reflection?

| Shape known at | Use |
|---|---|
| **build** time | **source generator** — zero runtime cost, AOT-safe, type-checked |
| **run** time | reflection, or runtime generation |

Example 09 shows both producing **byte-identical** JSON. The difference is *when* the
work happened. The direction of travel in .NET is consistently build-time: regex,
logging, JSON, configuration binding all now have generator-based alternatives, driven by
Native AOT and startup time.

What generators cannot do: handle types that do not exist at build time (plugins), or
depend on run-time configuration.

### 🔴 22. Your API endpoint is slow and the profiler shows reflection. What do you do?

A good answer walks the diagnosis, not straight to a fix:

1. **Where** is it — startup or per request? Startup reflection is normal and amortised.
2. **How many calls?** 33 ns each; if it is 10 calls per request, it is not your problem
   and the profiler is pointing at a symptom.
3. **Is it uncached?** The usual real culprit: `GetProperties()` in a loop, a new
   `JsonSerializerOptions` per call, or `Activator.CreateInstance` per item.
4. **Fix in this order:** cache the `*Info` objects → cache a **delegate**
   (`CreateDelegate`) → expression tree → source generator. Emit almost never.
5. **Check for boxing.** Measured: the same emitted getter was **2.6× vs 1.2×** purely
   because its signature boxed a `decimal`. Signature shape often dominates.
6. **Re-measure** with BenchmarkDotNet, not `Stopwatch` in a loop.

**And the senior instinct:** confirm reflection is actually the bottleneck. More often
the real cost is an N+1 query, a missing index, or JSON over-serialization — and
reflection is simply the most *visible* frame in the profile.
