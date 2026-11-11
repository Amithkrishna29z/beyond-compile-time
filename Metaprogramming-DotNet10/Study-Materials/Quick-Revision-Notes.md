# Quick Revision Notes

**Read this in the 15 minutes before you present.** Nothing new here — only what you
must have at your fingertips.

---

## The one sentence, if you forget everything else

> **A .NET assembly is self-describing. Metaprogramming reads that description
> (Part 2) or writes new code from it (Part 3). Every framework does it ONCE at
> startup, caches the result, and then runs compiled code.**

---

## One-sentence definitions

| Term | Definition |
|---|---|
| **Metadata** | The compiler-emitted description of every type and member, stored in the assembly itself. |
| **Metaprogramming** | Code that operates on program structure or behaviour instead of on data. |
| **Introspection** | Reading metadata at run time. |
| **Reflection** | The .NET API for introspection and reflective invocation. |
| **Reflection.Emit** | The API for generating new IL at run time. |
| **CIL / MSIL / IL** | The portable, stack-based instruction set every .NET language compiles to. |
| **Evaluation stack** | The stack every IL instruction pushes to and pops from. |
| **JIT** | The compiler that turns IL into machine code, per method, at first call. |
| **Attribute** | A class deriving from `Attribute`; applying it stores inert declarative data on a declaration. |
| **Expression tree** | Code represented as an inspectable, immutable object graph. |
| **Source generator** | A Roslyn component that adds C# source during the build. |
| **Shadow property** | A property in the EF Core model with no corresponding CLR property. |
| **Dynamic proxy** | A runtime-generated type implementing an interface and routing every call through one method. |
| **AssemblyLoadContext** | An isolation boundary for assembly loading and type identity — **not** a security sandbox. |

---

## API cheat-sheet

### Getting a `Type`

```csharp
typeof(Customer)                  // compile time, checked, fastest — PREFER THIS
instance.GetType()                // runtime type of an object
Type.GetType("Ns.Customer")       // from a string; needs ASSEMBLY-QUALIFIED name for other assemblies
assembly.GetType("Ns.Customer")   // look in one specific assembly
```

### Inspecting

```csharp
assembly.GetTypes()               // all types (throws ReflectionTypeLoadException — catch it!)
type.GetMethods() / GetProperties() / GetFields() / GetConstructors()
type.GetMethod("Name", [typeof(int)])    // specify signature for overloads
type.IsAssignableFrom(other)      // use this, NOT ==
property.GetValue(obj) / SetValue(obj, v)
method.Invoke(obj, [args])        // wraps exceptions in TargetInvocationException
```

### `BindingFlags`

```csharp
Public | Instance          // the common case
NonPublic | Instance       // private/protected members
Public | Static            // static members
DeclaredOnly               // exclude inherited
```

**Default with no flags = `Public | Instance | Static`.**
A "missing" member is almost always a `BindingFlags` bug.

### Creating

```csharp
Activator.CreateInstance<T>()                    // needs public parameterless ctor
Activator.CreateInstance(type, arg1, arg2)       // ctor chosen by argument types
Activator.CreateInstance(type, nonPublic: true)  // reaches a private ctor
ctor.Invoke([args])                              // explicit, and cacheable
ActivatorUtilities.CreateInstance<T>(provider, extraArgs)  // DI + your arguments
ActivatorUtilities.CreateFactory(type, argTypes)           // cache this on hot paths
```

### Attributes

```csharp
member.IsDefined(typeof(T))            // cheapest — constructs nothing
member.GetCustomAttribute<T>()         // one; THROWS AmbiguousMatchException if several
member.GetCustomAttributes<T>()        // all; use when AllowMultiple = true
member.GetCustomAttributesData()       // raw rows, NOTHING constructed — safe for untrusted
```

### Generating

```csharp
var dm = new DynamicMethod("Add", typeof(int), [typeof(int), typeof(int)]);
ILGenerator il = dm.GetILGenerator();
il.Emit(OpCodes.Ldarg_0);
il.Emit(OpCodes.Ldarg_1);
il.Emit(OpCodes.Add);
il.Emit(OpCodes.Ret);
var add = dm.CreateDelegate<Func<int, int, int>>();
```

### Expression trees

```csharp
Expression<Func<int, bool>> e = n => n > 18;
e.Body.NodeType                        // GreaterThan
Expression.Lambda<Func<int,int,int>>(body, a, b)
e.Compile()                            // EXPENSIVE — cache the delegate
```

---

## Reflection vs Reflection.Emit

| | Reflection | Reflection.Emit |
|---|---|---|
| Purpose | **inspect** and invoke | **create** code |
| Namespace | `System.Reflection` | `System.Reflection.Emit` |
| Cost | slow **per call** | slow **once**, then near-direct |
| Complexity | low | high |
| Type safety | runtime-checked, throws clearly | **not always checked at all** |
| Native AOT | mostly works | **does not work** |
| Use when | you need to *know* something | you need to *do* something, fast, repeatedly |

**They are partners:** reflection *finds* the target, Emit *bakes it in*.

---

## Compile-time vs runtime

| | Source generators | Reflection | Reflection.Emit |
|---|---|---|---|
| Runs at | **build** | run | run |
| Output | C# source | information | IL |
| Runtime cost | **zero** | per call | once, then ~direct |
| Type-checked | **yes** | no | no |
| Native AOT | **yes** | partly | **no** |
| Trimming | safe | needs annotation | unsafe |
| Types unknown at build time | **no** | yes | yes |

---

## `DynamicMethod` workflow

```text
1. new DynamicMethod(name, returnType, parameterTypes)
2. GetILGenerator()
3. Emit(...) one instruction at a time
4. CreateDelegate<TDelegate>()
5. CACHE THE DELEGATE  ← the step people forget
```

## Runtime type-generation workflow

```text
1. AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run)
2. .DefineDynamicModule("MainModule")
3. .DefineType("Ns.MyType", TypeAttributes.Public | Class, typeof(object))
4. .AddInterfaceImplementation(typeof(IKnownInterface))   ← the key design move
5. .DefineField / .DefineConstructor / .DefineMethod / .DefineProperty
6. GetILGenerator() on each, Emit the bodies
7. .CreateType()           ← bakes it; nothing can be added after
8. Activator.CreateInstance(created, args), then CAST TO THE INTERFACE
```

**To save it:** `new PersistedAssemblyBuilder(name, coreAssembly)` … `.Save(path)`.

---

## IL instructions you must know

| Instruction | Stack effect |
|---|---|
| `ldarg.0` / `ldarg.1` | push argument 0 / 1 |
| `ldc.i4.N` / `ldc.i4.s n` / `ldc.i4 n` | push an int constant (1 / 2 / 5 bytes) |
| `add` / `sub` / `mul` / `div` | pop 2, push 1 |
| `call` | pop args, push result — target fixed at emit time |
| `callvirt` | same, but virtual dispatch **and a null check** |
| `newobj ctor` | pop args, allocate + construct, push the reference |
| `ldfld` / `stfld` | load / store an instance field |
| `castclass T` | checked cast (throws); `isinst` is the `as` version |
| `box` / `unbox.any` | value type ↔ heap object |
| `ret` | pop the return value and return |

### The Add method, in full

```text
ldarg.0     [ a ]
ldarg.1     [ a, b ]
add         [ a+b ]
ret         [ ]
```

### The two rules

1. **Balance.** At `ret` the stack holds exactly the return value.
2. **Types.** Operand types must suit the opcode.

### ⚠ The `ldarg` trap

- **Static** method → `ldarg.0` is the **first argument**.
- **Instance** method → `ldarg.0` is **`this`**; the first argument is `ldarg.1`.

---

## Verified .NET 10 facts (checked on this machine — not recalled)

| Fact |
|---|
| `AssemblyBuilderAccess` has **only** `Run` and `RunAndCollect`. `Save`/`RunAndSave` are **.NET Framework only** and do not exist. |
| Saving is done with **`PersistedAssemblyBuilder`** (in the shared framework, no package). Verified: wrote a 2 KB DLL, reloaded it, invoked it → 42. |
| A missing `ret` → `InvalidProgramException` **on first call**, not at `Emit`. |
| **`add` on an int and a string produced NO error** — it silently returned garbage. Fully-trusted `DynamicMethod` IL is **not type-verified**. |
| `ServiceProvider`'s engine here is **`DynamicServiceProviderEngine`** — so say "DI uses reflection", not "DI uses Reflection.Emit". |
| A `DispatchProxy` base class **must not be `sealed`**; .NET 10 also has `DispatchProxy.Create(Type, Type)`. |
| SQLite renders `string.Contains` as **`instr(...) > 0`**, not `LIKE`. The *provider* decides. |
| `JsonSerializerOptions`: new-but-identical = 1.4×; new-and-**different** = **7.2×**. Equivalent instances share a process-wide cache. |

---

## Measured performance (Release, 2,000,000 property reads)

```text
  direct C# property access                     8 ms    1.0x
  PropertyInfo.GetValue                        67 ms    8.4x
  MethodInfo.Invoke                            59 ms    7.4x
  cached delegate (typed)                      10 ms    1.2x
  emitted IL (typed, no box)                   10 ms    1.2x
  emitted IL (object->object, boxes)           21 ms    2.6x
```

**Four takeaways:**

1. Reflection ≈ 7–8× per call — but 67 ms for **two million** reads is ~33 ns each.
2. A **cached delegate is as fast as emitted IL**. Try `MethodInfo.CreateDelegate`
   **before** writing any IL.
3. **Boxing and signature shape dominate** — rows 5 vs 6 are the same technique.
4. Generation is not free. One call → reflection wins.

---

## Real-world use cases

| Framework | What it reflects | When |
|---|---|---|
| ASP.NET Core MVC | controllers, actions, routing attributes | **startup** |
| DI container | constructor parameters | startup + first resolve |
| EF Core | `DbSet<T>`, annotations, fluent config | **first use**, then cached |
| `System.Text.Json` | properties and attributes | **first use** per type, cached |
| xUnit / NUnit | `[Fact]` / `[Test]` methods | test discovery |
| Moq / Castle | generates proxy **types** | first use |
| Plugin hosts | types implementing a contract | on load |

---

## Common limitations

| Limitation | Detail |
|---|---|
| **No compile-time safety** | a typo in a string is a run-time failure |
| **Slow per call** | unless you cache — and every framework caches |
| **Native AOT** | `Reflection.Emit`, `DispatchProxy`, `Expression.Compile()` all unavailable |
| **Trimming** | the trimmer cannot see reflective usage; annotate or do not trim |
| **Debuggability** | opaque frames, no source for generated code |
| **Unverified IL** | some invalid IL silently produces garbage |
| **`private` is not security** | `BindingFlags.NonPublic` reads it, legitimately |
| **ALC is not a sandbox** | isolates type identity, not privileges |
| **Unload is cooperative** | one cached `Type` keeps the context alive forever |
| **Proxy self-calls** | a service calling its own method bypasses the proxy |
| **Translation ≠ expressibility** | compile error vs provider runtime error are different failures |

---

## The decision order (say this on slide 28)

```text
1. Plain C#, generics, or a cached delegate      ← covers most cases
2. Expression trees                              ← if you need a FUNCTION
3. DispatchProxy                                 ← if you only need to intercept an INTERFACE
4. A Roslyn source generator                     ← if the shape is known at BUILD time
5. Reflection.Emit                               ← only if you need a TYPE, known only at RUN time
```

**Most people who reach for 5 needed 1 or 2.**

---

## Timing reminder

| Part | Slides | Minutes | Cumulative |
|---|---|---|---|
| Opening | 1–2 | 1:30 | 1:30 |
| Part 1 | 3–7 | 5:00 | 6:30 |
| Part 2 | 8–15 | 9:30 | 16:00 |
| Part 3 | 16–23 | 11:30 | 27:30 |
| Part 4 | 24–27 | 8:00 | 35:30 |
| Closing | 28 | 1:30 | **37:00** |

**Checkpoints:** Part 2 must START by 6:30. Part 3 must START by 16:00.
Part 4 must START by 27:30. **If you are late at 16:00, cut Part 2's demo, not Part 3.**

---

## The three lines you must not get wrong

1. **"`AssemblyLoadContext` is not a security sandbox."** It isolates type identity, not
   privileges.
2. **"`AssemblyBuilderAccess.Save` does not exist on .NET."** Use
   `PersistedAssemblyBuilder`.
3. **"DI uses reflection"** — not "DI uses Reflection.Emit". The engine is chosen at run
   time and falls back under AOT.

---

## If someone asks something you do not know

> *"I don't want to guess at that — it's exactly the kind of thing that changes between
> versions. Let me check it against the docs and come back to you."*

Then actually do. That answer costs nothing and buys more credibility than a confident
wrong one. There is a worked list in
[`Presenter-Guide/Audience-QA.md`](../Presenter-Guide/Audience-QA.md).
