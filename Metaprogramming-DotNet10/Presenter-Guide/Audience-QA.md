# Audience Q&A

**On-stage answers.** Each one is written to be *said*, in about 20–40 seconds, with the
sharpest sentence first. For the long written versions see
[`../Study-Materials/Interview-Questions.md`](../Study-Materials/Interview-Questions.md).

**How to use this.** Read the bold "lead with" line of each. If you can say those twenty
lines fluently you can handle almost any question this talk provokes.

---

## The fourteen most likely questions

### 1. "Is reflection slow?"

**Lead with:** *"Per call, yes — about seven to eight times a direct access. But that's
thirty-three nanoseconds."*

> Both halves matter. The ratio sounds alarming and the absolute number usually isn't.
> Once per web request it's free; per row of a million-row import it's your bottleneck.
>
> And the cost is *per call*, so the fix is always the same: do the reflection once, cache a
> delegate, then call compiled code. That's what MVC, EF Core, the DI container and
> System.Text.Json all do.

*Measured: 67 ms vs 8 ms for 2,000,000 property reads, Release, slide 21.*

---

### 2. "Is Reflection.Emit faster than reflection?"

**Lead with:** *"The generated code is. Generating it isn't."*

> So it wins if you amortise over many calls — order of thousands. Below that, reflection
> wins outright.
>
> But here's the finding I'd actually act on: a cached delegate from
> `MethodInfo.CreateDelegate` measured *exactly* as fast as the emitted IL. Identical. So
> try `CreateDelegate` before you write a single OpCode.

---

### 3. "Does .NET use reflection internally?"

**Lead with:** *"Extensively — and almost always at startup, then cached."*

> MVC reflects over your controllers at startup. EF Core reflects over your `DbSet`
> properties on first use. The DI container reads constructor signatures.
> System.Text.Json reflects per type on first use.
>
> The important nuance: it's used for *discovery*, not for *execution* on the hot path. MVC
> does not call `MethodInfo.Invoke` per request — it builds a cached invoker at startup.

---

### 4. "Does dependency injection always use reflection?"

**Lead with:** *"No. Separate the pattern from the container."*

> Constructor injection needs no reflection at all — you can wire a whole app by hand with
> `new`. Containers use reflection to read constructors so you don't have to do that wiring.
> Some then generate code to call them; some don't; compile-time DI containers use no
> reflection whatever.
>
> I checked Microsoft's container on .NET 10 — the engine is `DynamicServiceProviderEngine`,
> which picks a strategy at runtime and falls back to reflection-only under AOT. So "DI uses
> reflection" is safe. "DI uses Reflection.Emit" is not.

---

### 5. "What's the difference between reflection and expression trees?"

**Lead with:** *"Reflection describes declarations. Expression trees represent code."*

> Reflection tells you `Customer` has an `Age` property. An expression tree tells you
> somebody wrote `c.Age > 18`.
>
> That's why EF Core needs expression trees and not reflection: to generate a `WHERE`
> clause you need the *expression*, not just the member list.
>
> They're often used together — the dynamic filter builder in the repo uses `GetProperty`
> to find the member, then builds a tree around it.

---

### 6. "Can reflection access private methods?"

**Lead with:** *"Yes, with `BindingFlags.NonPublic` — and that's allowed, not a loophole."*

> `private` is a rule the **compiler** enforces at compile time. Accessibility is an
> encapsulation feature. It is not a security boundary.
>
> Legitimate uses: deserializers restoring objects with no setters, ORMs, test fixtures.
> Illegitimate: routing around another library's invariants — which breaks on their next
> patch, because private members are part of no contract.

**If pushed — "so how do I protect a secret in-process?":**

> You can't, from code running in the same process. There is no supported in-process sandbox
> in modern .NET. You need a process or OS boundary.

---

### 7. "What's the difference between `DynamicMethod` and `MethodBuilder`?"

**Lead with:** *"Do you need a method, or a type?"*

> `DynamicMethod` is a single standalone method with no declaring type. Cheap, collectible,
> can't be saved, can't define fields. Right for accessors, converters, serializer fast
> paths.
>
> `MethodBuilder` is a method *on a `TypeBuilder`* — so you get fields, several methods,
> interfaces, and it can be persisted. Right for proxies and mocks, because a mock must *be*
> an `IFoo` to be passed where an `IFoo` is expected.

---

### 8. "What happens when generated IL is invalid?"

**Lead with:** *"It depends — and the asymmetry is the point."*

> A missing `ret` threw `InvalidProgramException` — but on the *first call*, not at `Emit`.
> So the failure surfaces far from the line that caused it.
>
> And `add` on an int and a string threw **nothing**. It returned a plausible-looking garbage
> number — the string's managed reference added to one, as an integer.
>
> Fully-trusted `DynamicMethod` IL is not type-verified. Your safety net is thinner than the
> compiler's, and sometimes there's no net at all.

**If asked "so how do I debug it?":**

> Write the C# first and read its IL in ILSpy. Track the expected stack depth as you emit.
> And generate with `PersistedAssemblyBuilder`, save it, and run ILVerify — that's the only
> way to get proper validation.

---

### 9. "What's the difference between IL and machine code?"

**Lead with:** *"IL is portable, stack-based and fully typed. Machine code is none of those."*

> IL targets the CLR's virtual machine; machine code targets a CPU. IL carries full type
> information, which is how the runtime can enforce memory safety. The JIT translates one
> into the other, per method, on first call.
>
> Why the extra step? One assembly runs on x64 and Arm64, Windows and Linux, and the JIT can
> specialise for the actual CPU.

**Nuance if they look like they want it:** ReadyToRun precompiles at publish with IL as a
fallback; tiered compilation compiles a hot method more than once; Native AOT ships no JIT;
the Mono interpreter never compiles at all.

---

### 10. "Can Reflection.Emit work with Native AOT?"

**Lead with:** *"No. There's no JIT, so there's nothing to compile generated IL."*

> `RuntimeFeature.IsDynamicCodeSupported` is false and the Emit APIs throw
> `PlatformNotSupportedException`. Same for `DispatchProxy` and `Expression.Compile()`.
>
> What to use instead: source generators. `[GeneratedRegex]`, `JsonSerializerContext`,
> `[LoggerMessage]` — they exist precisely to move this work to build time.

**The precision point, if you have 10 more seconds:**

> Expression trees can still be *built and read* under AOT — which is why EF Core can
> translate LINQ to SQL there, since translation only reads the tree. It's `Compile()` that
> needs the JIT.

---

### 11. "Can a plugin be loaded without restarting the application?"

**Lead with:** *"Loading, yes. Replacing, much harder."*

> Loading is `AssemblyLoadContext` plus `LoadFromAssemblyPath` — that's the demo.
>
> Unloading needs a *collectible* context, and you must drop **every** reference: no live
> plugin instances, no cached `Type` or `MethodInfo`, no subscribed event handlers, nothing
> in a host static field. Then `Unload()` — which is a *request*. In the demo the context
> collected on the second GC attempt.
>
> What production systems usually do instead: run plugins in a child process and restart
> that. Far more robust.

---

### 12. "Is `AssemblyLoadContext` a security boundary?"

**Lead with:** *"No. It isolates type identity and versions. It does not isolate privileges."*

> Once you call `LoadFromAssemblyPath`, that code runs with your process's full rights. It
> can read your files, open sockets, read your secrets, reflect against your private state,
> and call `Environment.Exit`.
>
> There's no supported in-process sandbox in modern .NET — CAS and AppDomain partial trust
> were .NET Framework and are gone. For untrusted code: a separate process with a reduced
> token, a container, a VM, or WASM.

**What it *is* good for:** keeping two versions of a library apart, and making sure the
plugin's `IFoo` is the *same type* as your `IFoo`.

---

### 13. "How does EF Core translate LINQ into SQL?"

**Lead with:** *"Because the query arrives as data, not as compiled code."*

> `IQueryable`'s `Where` takes `Expression<Func<T,bool>>`, not `Func<T,bool>`. That one type
> difference is the whole mechanism.
>
> EF then runs a chain of `ExpressionVisitor`s over the tree: bind member accesses to mapped
> properties, turn navigations into joins, translate method calls to provider-specific SQL
> functions, then print SQL. The translation is cached per query *shape*.
>
> And the provider decides the details — `string.Contains` became `instr(...)` on SQLite,
> not `LIKE`. Always check with `ToQueryString()`.

---

### 14. "What are the limitations of dynamic proxies?"

**Lead with:** *"Interfaces only — and self-calls aren't intercepted."*

> Three that matter. `DispatchProxy` can only proxy an interface, not a class. The base
> proxy class must not be sealed, because the generated type derives from it. And the one
> that catches everybody: a call from one method of the service to another is **not**
> intercepted, because `this` inside the service is the real object, not the proxy. That
> breaks naive caching in a way that's very hard to spot.
>
> Also: async needs explicit handling, every call pays `MethodInfo.Invoke`, and it doesn't
> work under Native AOT.
>
> Honestly? A hand-written decorator is right more often. Twenty lines of ordinary C#,
> debuggable and AOT-safe.

---

## Also likely, and quick

### "Should we use any of this in application code?"

> Mostly no. In application code the types are usually known, so generics and plain C# are
> better. The honest exceptions are plugin systems and genuinely data-driven features — a
> report builder whose columns come from config.

### "Our team banned reflection for performance. Reasonable?"

> Partly. Banning *per-call* reflection on a hot path is sound. Banning it at *startup* would
> mean banning ASP.NET Core, EF Core and the DI container. The useful rule is "no reflection
> per request", not "no reflection".

### "Isn't this all just `dynamic`?"

> No. `dynamic` defers *binding* but you still write the member name in your source. With
> reflection the name itself is data — from a config file, a database row, an HTTP request.
> And `dynamic` goes through the much heavier DLR.

### "Do unused attributes cost anything?"

> At runtime, essentially nothing — a few bytes of metadata, never constructed unless
> something asks. Not free for *trimming* though: an attribute referencing a type can keep
> that type alive.

### "Can I add attributes at runtime?"

> Not to an existing type — metadata of a loaded type is immutable. You can apply them to
> types you *generate*. For runtime configuration the usual answer is a fluent API, which is
> exactly why EF Core has one.

### "Source generators or reflection?"

> Shape known at build time → source generator, every time: zero runtime cost, type-checked,
> AOT-safe. Shape known only at runtime → reflection. Source generators can't help with a
> plugin, because that type doesn't exist at build time.

### "Why is my `Type.GetType` returning null?"

> Almost certainly because the name isn't assembly-qualified. `Type.GetType` searches only
> the calling assembly and CoreLib. Print `typeof(X).AssemblyQualifiedName` and look at the
> real string — and pass `throwOnError: true` so you get a named exception instead of a null.

### "Is any of this thread-safe?"

> Reading reflection objects is thread-safe. Your own caches around them are not — use
> `ConcurrentDictionary`. And `JsonSerializerOptions` is safe to share once it's been used;
> what's unsafe is mutating it afterwards.

---

## Handling the three awkward kinds of question

### You don't know

Say this, and mean it:

> *"I don't want to guess at that — it's exactly the kind of thing that changes between
> versions. Let me check it and come back to you."*

Then write it down in front of them, and actually follow up. That answer costs you nothing
and buys more credibility than a confident wrong one. **Never** improvise a version-specific
fact.

### Somebody contradicts you

Most likely on one of three things, and in all three you are on solid ground because you
ran the code:

| They say | You say |
|---|---|
| "You can save a dynamic assembly with `AssemblyBuilderAccess.Save`." | *"That was .NET Framework. On .NET 10 the enum has exactly two members — the demo prints them. Saving is `PersistedAssemblyBuilder` now, and the demo writes and reloads a real DLL."* |
| "`AssemblyLoadContext` sandboxes the plugin." | *"It isolates type identity and versions, not privileges. There's no supported in-process sandbox in modern .NET since CAS was removed."* |
| "DI uses Reflection.Emit." | *"It may, depending on version and platform — I read the engine field on .NET 10 and it picks a strategy at runtime, falling back to reflection-only under AOT. So 'uses reflection' is the claim I'd defend."* |

And if they are **right**, say so immediately and move on: *"You're right, thank you — let
me correct that."* The room respects that far more than a defence.

### The question that needs ten minutes

> *"That's a really good question and a bigger one than we have time for — grab me
> afterwards. Short version:"* …one sentence… *"and the long version is in the study guide
> under Part 3."*

---

## If nobody asks anything

Have one ready to prime the room:

> "Let me answer the question I'd be asking. If reflection is seven times slower, why does
> ASP.NET Core use it everywhere?" …then give answer 3.

Or point at the repo: *"There's a 129-page study guide and twelve runnable projects in
there. The one I'd actually run tonight is `05-DynamicMethod` — in Release — because it
makes the performance argument for you."*
