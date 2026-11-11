# Part 3 — Runtime Code Generation

**Session time: 12 minutes (slides 16–23)**
**Runnable examples: [`05-DynamicMethod`](../Code-Examples/05-DynamicMethod),
[`06-ReflectionEmit`](../Code-Examples/06-ReflectionEmit)**

> **Everything in this part was verified by running it on .NET 10.0.11 (SDK 10.0.400).**
> This matters more here than anywhere else in the session: much of the published
> material about `Reflection.Emit` describes .NET Framework APIs that **do not exist**
> on modern .NET. Where this guide contradicts an older tutorial, the difference is
> usually that.

---

# 1. What Is Runtime Code Generation?

## 1.1 Definition

**Runtime code generation** is creating new executable code — new IL, compiled to
machine code by the JIT — while the process is running.

Part 2 **read** metadata that already existed. Part 3 **writes** code that did not.

## 1.2 In simple English

Part 2 was reading a book's table of contents. Part 3 is writing a new chapter while
the book is being read, and then reading that chapter.

## 1.3 Why applications generate code dynamically

There is exactly one good reason, and several bad ones.

**The good reason: speed, when the shape is only known at run time.**

Reflection is flexible but slow per call, because every call re-does argument binding
and access checks. If you must perform the same reflective operation millions of times,
you can pay the reflection cost **once** to generate a specialised method, then call
that method directly. You convert a per-call cost into a one-off cost.

This is why it appears where it does:

| User | Generates |
|---|---|
| `System.Text.Json` (reflection mode) | fast property accessors per type |
| EF Core | entity materialisers and property accessors |
| DI containers | factory delegates per service |
| Moq, NSubstitute, FakeItEasy | mock **types** |
| Castle DynamicProxy | proxy **types** |
| `RegexOptions.Compiled` | a matcher for your specific pattern |
| ASP.NET Core | model binders, request delegates |

**A secondary reason: you need a TYPE that did not exist.** A mock or proxy must *be*
an `IFoo` to be passed where an `IFoo` is expected. No amount of reflection creates a
type; only code generation does.

**The bad reasons:** because it is clever; to avoid writing a `switch`; to build a
scripting language you could have taken off the shelf.

## 1.4 Runtime support requirements

Code generation needs a runtime that can compile code at run time. Two flags tell you:

```csharp
RuntimeFeature.IsDynamicCodeSupported   // can I create code at all?
RuntimeFeature.IsDynamicCodeCompiled    // will it be JIT-compiled to machine code?
```

Verified on this machine:

```text
RuntimeInformation.FrameworkDescription : .NET 10.0.11
RuntimeFeature.IsDynamicCodeSupported   : True
RuntimeFeature.IsDynamicCodeCompiled    : True
```

| Platform | `IsDynamicCodeSupported` | `IsDynamicCodeCompiled` | Effect |
|---|---|---|---|
| CoreCLR (server, desktop, container) | `true` | `true` | works, fast |
| **Native AOT** | **`false`** | `false` | **`Reflection.Emit` throws `PlatformNotSupportedException`** |
| Mono interpreter (iOS, some WASM) | `true` | **`false`** | works, but runs **interpreted** — the speed benefit is gone |
| Mono with JIT (Android) | `true` | `true` | works |

**The practical rule for library authors:** check `IsDynamicCodeSupported` and keep a
reflection-based fallback. That is exactly what `System.Text.Json` and the DI container
do — which is why they still function under AOT, just more slowly or via source
generation.

## 1.5 Audience questions

**Q: Is this the same as `eval` in JavaScript or Python?**
Conceptually similar — new code at run time — but much lower level. There is no C#
compiler involved: you emit IL instruction by instruction. The closest thing to `eval`
in .NET is hosting Roslyn (`CSharpScript.EvaluateAsync`), which compiles real C# at run
time. That is a different, heavier tool.

**Q: Is generated code slower than compiled code?**
After the JIT has compiled it, no — the JIT does not know or care that the IL was
generated rather than loaded from a file. The costs are the generation itself and the
first-call JIT.

**Q: Can I debug generated code?**
Barely. There is no source. You can attach symbols to a *persisted* assembly (§5), which
is the main practical reason to persist one.

## 1.6 Documentation

- Emitting dynamic methods and assemblies — <https://learn.microsoft.com/en-us/dotnet/fundamentals/reflection/emitting-dynamic-methods-and-assemblies>
- `RuntimeFeature.IsDynamicCodeSupported` — <https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.runtimefeature.isdynamiccodesupported>

---

# 2. Reflection vs Reflection.Emit

## 2.1 The comparison

| | **Reflection** (`System.Reflection`) | **Reflection.Emit** (`System.Reflection.Emit`) |
|---|---|---|
| **Purpose** | inspect and invoke existing code | create new code |
| **Direction** | read | write |
| **Key APIs** | `Type`, `MethodInfo`, `PropertyInfo`, `Activator` | `DynamicMethod`, `ILGenerator`, `TypeBuilder`, `MethodBuilder` |
| **Input** | an assembly's metadata | IL instructions you emit |
| **Output** | information, or an `Invoke` result | a callable method or a new `Type` |
| **Speed** | slow **per call** | slow **once**, then near-direct per call |
| **Complexity** | low — a day to learn | high — IL, the evaluation stack, verification |
| **Type safety** | checked at run time, throws clearly | **not always checked at all** (§8) |
| **Native AOT** | mostly works | **does not work** |
| **Debuggability** | poor | worse |
| **Use when** | you need to *know* something | you need to *do* something, repeatedly and fast |

## 2.2 They are partners, not rivals

Almost all real Emit code starts with reflection:

```csharp
PropertyInfo property = typeof(Employee).GetProperty("Salary")!;  // REFLECTION: find the target
MethodInfo getter = property.GetGetMethod()!;                     // REFLECTION

var method = new DynamicMethod("GetSalary", typeof(decimal), [typeof(Employee)]);
ILGenerator il = method.GetILGenerator();
il.Emit(OpCodes.Ldarg_0);
il.Emit(OpCodes.Callvirt, getter);                                // EMIT: use what reflection found
il.Emit(OpCodes.Ret);

var fast = method.CreateDelegate<Func<Employee, decimal>>();      // from here on: no reflection
```

Reflection **discovers** the target; Emit **bakes it in**. The result is used
indefinitely with no further reflection.

## 2.3 The measured numbers

Verified, Release build, 2,000,000 iterations, reading one `decimal` property
(`05-DynamicMethod`):

```text
  technique                                total ms    relative to direct
  -----------------------------------------------------------------------
  direct C# property access                     8        1.0x
  PropertyInfo.GetValue                        67        8.4x
  MethodInfo.Invoke                            59        7.4x
  cached delegate (typed)                      10        1.2x
  emitted IL (typed, no box)                   10        1.2x
  emitted IL (object->object, boxes)           21        2.6x
```

**Read these five rows carefully — there are four lessons, and the third is the one
most talks get wrong.**

1. **Reflection per call costs ~7–8× a direct access.** Real, but note the units:
   67 ms for *two million* reads is ~33 nanoseconds each. "Reflection is slow" is true
   per call and often irrelevant in absolute terms. If you do it once per web request,
   it is free. If you do it per row of a million-row import, it is your bottleneck.

2. **A cached delegate and emitted IL land in the same speed class** (both 1.2×), and
   both are close to direct access. The win comes from *caching the work*, not from IL
   specifically.

3. **`MethodInfo.CreateDelegate` gets you emit-level speed with no IL at all.** Compare
   rows 4 and 5: identical. **Try this before writing a single `OpCode`.** This is the
   single most useful practical takeaway in Part 3.

4. **Boxing and signature shape dominate.** Rows 5 and 6 are the same property, the
   same technique, differing only in signature: `object → object` pays a `castclass` and
   a `decimal` box on every call, and that alone costs more than the choice between
   reflection and emit. Optimising "reflection vs emit" while ignoring boxing is
   optimising the wrong thing.

> **Caveat on these numbers.** They come from `Stopwatch` in a loop on one machine, in
> one run, in Release. They are honest to an order of magnitude and no better. Use
> **BenchmarkDotNet** for anything you intend to act on.

## 2.4 Audience questions

**Q: Is Reflection.Emit faster than reflection?**
The *generated code* is far faster per call. *Generating* it is slower than a single
reflective call. So: faster if you amortise over many calls, slower if you call once.
And a cached delegate usually gets you the same benefit for a fraction of the risk.

**Q: Where is the break-even?**
Depends on the operation, but the order of magnitude is thousands of calls. Below that,
reflection. If you cannot estimate the call count, you do not have a performance problem
yet.

**Q: Does .NET itself use Reflection.Emit?**
Yes — historically and currently, in the places listed in §1.3. But note that several of
those have been *migrating away* from it toward source generators precisely because of
AOT. The trend is away from runtime emit.

---

# 3. CIL / MSIL — The Language You Are Generating

## 3.1 Definition

**CIL** (Common Intermediate Language), also called **MSIL** or just **IL**, is the
stack-based instruction set that every .NET language compiles to. It is defined by
**ECMA-335**.

The three names mean the same thing. "CIL" is the standard's term, "MSIL" is the older
Microsoft one, "IL" is what people say.

## 3.2 The compilation pipeline

```text
   YOUR C#                        BUILD TIME                    RUN TIME
┌─────────────┐   Roslyn    ┌──────────────────┐   JIT     ┌──────────────────┐
│  Program.cs │ ──────────► │   Program.dll    │ ────────► │   machine code   │
│             │  (csc)      │  ┌────────────┐  │  (per     │  x64 / Arm64     │
│ int Add(…)  │             │  │ IL         │  │  method,  │                  │
│   => a + b; │             │  │ METADATA   │  │  on first │  push/pop become │
└─────────────┘             │  └────────────┘  │  call)    │  real registers  │
                            └──────────────────┘           └──────────────────┘
                                     │                              ▲
                                     │  PART 2 reads the metadata   │
                                     │  PART 3 adds new IL here ────┘
```

**Why the extra step?** One IL assembly runs on x64, Arm64, Windows, Linux and macOS.
The JIT specialises for the actual CPU at run time — it can use AVX-512 on a machine
that has it without you shipping a separate build.

## 3.3 Exceptions to the simple pipeline

The diagram above is the common case. Be accurate about the rest:

| Mode | How IL becomes machine code |
|---|---|
| **JIT** (default) | per method, on first call, at run time |
| **ReadyToRun (R2R)** | AOT-compiled at *publish* time, with IL kept as a fallback; the JIT can still re-optimise. Speeds up startup. |
| **Tiered compilation** (default on) | tier-0 quickly, then tier-1 optimised for hot methods — so a method is compiled *more than once* |
| **Native AOT** | compiled fully ahead of time; **no JIT ships**, so no runtime code generation at all |
| **Mono interpreter** | IL is interpreted, not compiled |

Tiered compilation is worth a mention when discussing benchmarks: it is why warm-up
loops matter, and why a naive first measurement is always wrong.

## 3.4 The evaluation stack

IL has no registers you can name. Every instruction works on an **evaluation stack**.

```text
instruction            stack (top on the right)      effect
──────────────────────────────────────────────────────────────────────────
                       [ ]                           start: empty
ldarg.0                [ a ]                         push argument 0
ldarg.1                [ a, b ]                      push argument 1
add                    [ a+b ]                       pop 2, push their sum
ret                    [ ]                           pop the return value, return
```

**Two rules govern everything, and nearly every Emit bug is one of them:**

1. **Balance.** At `ret`, the stack must hold exactly the return value (or nothing, for
   `void`). Leaving something behind, or taking too much, is invalid IL.
2. **Types.** Operand types must suit the opcode. `add` on an `int` and a string
   reference is invalid — see §8 for what actually happens, which is worse than an
   error.

A useful habit: write the expected stack state as a comment after every `Emit` call, as
the examples in this package do. It turns an invisible bug into a visible one.

## 3.5 The instruction set the syllabus requires

### `ldarg` — load argument

Pushes a method argument.

| Instruction | Loads |
|---|---|
| `ldarg.0` | argument 0 |
| `ldarg.1` | argument 1 |
| `ldarg.2`, `ldarg.3` | arguments 2, 3 |
| `ldarg.s <n>` | argument *n* (n ≤ 255) |
| `ldarg <n>` | any argument |

> **The numbering trap.** For a **static** method, `ldarg.0` is the first real argument.
> For an **instance** method, `ldarg.0` is **`this`**, and the first real argument is
> `ldarg.1`. Getting this wrong is the most common beginner Emit bug. A `DynamicMethod`
> declared as in §4 is static; a `MethodBuilder` instance method is not. Compare the IL
> in example 05 (static) with example 06's constructor and `Scale` method (instance) —
> the difference is visible.

### `ldc.i4` — load constant (int32)

| Instruction | Bytes | Range |
|---|---|---|
| `ldc.i4.0` … `ldc.i4.8` | 1 | 0–8 |
| `ldc.i4.m1` | 1 | −1 |
| `ldc.i4.s <sbyte>` | 2 | −128…127 |
| `ldc.i4 <int32>` | 5 | any 32-bit value |

All push the same thing; they differ only in encoding size. (`ldc.i8`, `ldc.r4`,
`ldc.r8` exist for `long`, `float`, `double`.)

### `add` and `mul` — arithmetic

Pop two operands, push one result.

```text
before: [ 6, 7 ]   →   mul   →   after: [ 42 ]
```

`sub`, `div`, `rem` follow the same pattern. Variants: `add.ovf` throws on overflow
(what C#'s `checked` emits); `div.un` is unsigned.

### `call` and `callvirt` — invoke a method

Pops the arguments (and the receiver, for instance methods), pushes the return value if
there is one.

| | `call` | `callvirt` |
|---|---|---|
| Target decided | now, at emit time | at run time, from the object's real type |
| Null check | no | **yes** — throws `NullReferenceException` |
| Used for | static and non-virtual instance methods | virtual and interface methods |

### `newobj` — allocate and construct

One instruction that allocates the object, runs the constructor with the popped
arguments, and pushes the reference.

```text
before: [ 3, 4 ]   →   newobj Point::.ctor(int,int)   →   after: [ Point ]
```

This single instruction is what `new`, `Activator.CreateInstance`, a DI container and a
deserializer all ultimately have to perform. Emitting it once and caching the delegate
is how containers avoid paying reflection costs per resolve.

### `ret` — return

Pops the return value (if the signature has one) and returns. Every path must end in
`ret` (or `throw`, or an unconditional branch to one).

### Others you will meet immediately

| Instruction | Does |
|---|---|
| `ldfld` / `stfld` | load / store an instance field |
| `ldsfld` / `stsfld` | load / store a static field |
| `castclass T` | checked reference cast; throws `InvalidCastException` |
| `isinst T` | like `as` — pushes null instead of throwing |
| `box T` / `unbox.any T` | value type ↔ heap object |
| `ldstr "text"` | push a string literal |
| `ldnull` | push null |
| `br`, `brtrue`, `brfalse`, `beq`, `blt` | branches (used with `ILGenerator.DefineLabel`) |
| `ldloc` / `stloc` | load / store a local (declared with `DeclareLocal`) |
| `nop` | nothing — a placeholder |

## 3.6 Audience questions

**Q: Is IL the same as machine code?**
No. IL is a portable, CPU-independent, stack-based instruction set, and it carries full
type information. Machine code is CPU-specific, register-based, and has no type
information. The JIT translates the first into the second.

**Q: Can I read the IL of my own C#?**
Yes, and you should before writing any: ILSpy, `ildasm`, or sharplab.io. Write the C#
you want, read its IL, then emit that. Far more reliable than reasoning from first
principles.

**Q: Is IL human-writable?**
`ilasm` accepts IL source, and people do write it, but it is a specialised skill.
`ILGenerator` is the normal route from a .NET program.

**Q: Why a stack machine and not registers?**
A stack machine is simpler to verify and to target from a compiler, and it does not
commit to a register count — leaving the JIT free to allocate real registers for the
actual CPU.

## 3.7 Documentation

- `OpCodes` — every instruction — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes>
- Managed code — <https://learn.microsoft.com/en-us/dotnet/standard/managed-code>

---

# 4. Lightweight Code Generation — `DynamicMethod`

## 4.1 Definition

A **`DynamicMethod`** is a single method with **no declaring type**, created and
compiled at run time. It is the cheapest form of runtime code generation.

## 4.2 The four APIs

| API | Role |
|---|---|
| `new DynamicMethod(name, returnType, parameterTypes)` | declare the signature |
| `GetILGenerator()` | get the writer for its body |
| `ILGenerator.Emit(OpCode, …)` | append one instruction |
| `CreateDelegate<T>()` | finish it and get something callable |

## 4.3 The canonical example, instruction by instruction

```csharp
var method = new DynamicMethod(
    name: "Add",                                 // for stack traces and debugging only
    returnType: typeof(int),
    parameterTypes: [typeof(int), typeof(int)]);

ILGenerator il = method.GetILGenerator();

il.Emit(OpCodes.Ldarg_0);
il.Emit(OpCodes.Ldarg_1);
il.Emit(OpCodes.Add);
il.Emit(OpCodes.Ret);

var add = method.CreateDelegate<Func<int, int, int>>();
```

Verified output, with the stack state after each instruction:

```text
Signature: int Add(int a, int b)

  IL                 | evaluation stack after the instruction (top on the right)
  -------------------+-------------------------------------------------------
  ldarg.0            | [ a ]                 push argument 0
  ldarg.1            | [ a, b ]              push argument 1
  add                | [ a+b ]               pop two, push their sum
  ret                | [ ]                   pop the return value and return

add(2, 3)    = 5
add(40, 2)   = 42
add(-7, 7)   = 0
```

**Line by line:**

| Line | Why |
|---|---|
| `name: "Add"` | Cosmetic. Appears in stack traces; has no semantic effect. |
| `returnType`, `parameterTypes` | The signature. The IL you emit must match it, and `CreateDelegate`'s type argument must match it too. |
| `GetILGenerator()` | The body writer. Calling it twice returns the same generator. |
| `Ldarg_0`, `Ldarg_1` | The two arguments. This method is **static**, so argument 0 is the first real argument. |
| `Add` | Pops two, pushes one. The stack goes from depth 2 to depth 1. |
| `Ret` | Pops the `int` return value. Stack depth 0. Balanced. |
| `CreateDelegate<Func<int,int,int>>()` | Hands the IL to the runtime and returns a delegate. From here the per-call cost is a normal delegate invocation. |

## 4.4 Constants

Target: `int f(int x) => (x * 2) + 10;`

```text
  IL                 | stack after
  -------------------+----------------------
  ldarg.0            | [ x ]
  ldc.i4.2           | [ x, 2 ]        one-byte form for small constants
  mul                | [ x*2 ]
  ldc.i4.s 10        | [ x*2, 10 ]     short form, operand fits in a signed byte
  add                | [ x*2+10 ]
  ret                | [ ]

  f(0) = 10
  f(1) = 12
  f(16) = 42
  f(100) = 210
```

Note `il.Emit(OpCodes.Ldc_I4_S, (sbyte)10)` — the cast matters, because `Emit` has
overloads per operand type and picking the wrong one produces wrong IL.

## 4.5 Calling an existing method

Target: `int f(int a, int b) => Math.Max(a, b) * 10;`

```text
  IL                              | stack after
  --------------------------------+----------------------
  ldarg.0                         | [ a ]
  ldarg.1                         | [ a, b ]
  call int32 Math::Max(int32,int32)| [ max ]        popped 2 args, pushed 1 result
  ldc.i4.s 10                     | [ max, 10 ]
  mul                             | [ max*10 ]
  ret                             | [ ]

  f(3, 9)  = 90
  f(42, 1) = 420
```

This is the most valuable technique in practice: **keep the emitted IL tiny and `call`
into normal C# helpers for anything non-trivial.** Debuggable C# does the work; the
generated method is just the glue.

## 4.6 Constructing an object

Target: `object f(int x, int y) => new Point(x, y);`

```text
  IL                          | stack after
  ----------------------------+----------------------
  ldarg.0                     | [ x ]
  ldarg.1                     | [ x, y ]
  newobj Point::.ctor(int,int)| [ Point ]      popped 2 args, pushed the new reference
  ret                         | [ ]

  f(3, 4) = Point(3, 4)
  f(0, 0) = Point(0, 0)
```

## 4.7 The realistic use — a generated accessor

Target: `object f(object o) => (object)((Employee)o).Salary;`

```text
  ldarg.0                      | [ o ]            the untyped instance
  castclass Employee           | [ Employee ]     checked cast, throws if wrong type
  callvirt get_Salary()        | [ decimal ]      run the property getter
  box decimal                  | [ object ]       value type -> heap object
  ret                          | [ ]

  emitted getter  -> 94500
  PropertyInfo    -> 94500
  direct property -> 94500
```

`object → object` is the signature a serializer or mapper needs, because it does not
know the types at compile time. It is also why it is slower than the typed version
(§2.3, rows 5 and 6): the cast and the box are real work on every call.

## 4.8 Where the "runtime" in runtime code generation really shows

The opcode is **data**, so one factory can build all four operators from a string that
did not exist at compile time:

```csharp
public static Func<int, int, int> Build(string op)
{
    OpCode opcode = op switch
    {
        "+" => OpCodes.Add,
        "-" => OpCodes.Sub,
        "*" => OpCodes.Mul,
        "/" => OpCodes.Div,
        _ => throw new ArgumentException("Unsupported operator: " + op, nameof(op))
    };

    var method = new DynamicMethod("Op" + op, typeof(int), [typeof(int), typeof(int)]);
    ILGenerator il = method.GetILGenerator();
    il.Emit(OpCodes.Ldarg_0);
    il.Emit(OpCodes.Ldarg_1);
    il.Emit(opcode);
    il.Emit(OpCodes.Ret);

    return method.CreateDelegate<Func<int, int, int>>();
}
```

Verified:

```text
  Build("+")(84, 2) = 86
  Build("-")(84, 2) = 82
  Build("*")(84, 2) = 168
  Build("/")(84, 2) = 42
```

That is the honest shape of the technique: **a small interpreter of your data, turned
into machine code once, then reused.** A `switch` over the operator would be simpler
here — the technique pays off when the "data" is a whole expression, a mapping
configuration, or a type's property list.

## 4.9 Advantages and limitations

**Advantages:** cheapest form; collectible (garbage-collected when unreferenced); can
be given restricted skip-visibility to reach private members; near-direct speed once
compiled.

**Limitations:** one method only — no fields, no types, no second method; cannot be
saved to disk; unverified IL (§8); poor debugging; needs dynamic code support.

## 4.10 Common mistakes

1. **`ldarg` numbering** on instance vs static methods (§3.5).
2. **Forgetting `ret`.** `InvalidProgramException` on first call.
3. **Unbalanced stack.** Same symptom, harder to spot.
4. **Mismatched delegate type** in `CreateDelegate`.
5. **Wrong `Emit` overload** — `Ldc_I4_S` wants an `sbyte`.
6. **Generating per call** instead of caching the delegate. This single mistake can make
   the "optimisation" slower than the reflection it replaced.
7. **`call` where `callvirt` is needed** — a virtual method invoked non-virtually
   silently calls the wrong override.

## 4.11 Audience questions

**Q: What is the difference between `DynamicMethod` and `MethodBuilder`?**
`DynamicMethod` is a standalone method with no declaring type — cheap, collectible, not
savable, and it cannot define fields or types. `MethodBuilder` is a method *on a
`TypeBuilder`*, so you can build a whole type with state and several methods, and it can
be persisted. Use `DynamicMethod` for accessors and converters; `MethodBuilder` when you
need a **type** (proxies, mocks).

**Q: Can `DynamicMethod` access private members?**
Yes, with the `restrictedSkipVisibility` constructor overload. This is how serializers
set private backing fields. It is a capability, not a security hole — as Part 2 §6.4
notes, private was never a security boundary.

**Q: Are dynamic methods garbage-collected?**
Yes — that is a real advantage over a non-collectible dynamic assembly. When no delegate
or reference remains, it can be collected.

**Q: Should I use this or an expression tree?**
Expression tree, almost always. Same speed class, type-checked as you build, printable,
and it fails loudly rather than silently (§8). Part 4 §8 covers them. Use `DynamicMethod`
only when you need IL that expression trees cannot express.

## 4.12 Documentation

- `DynamicMethod` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.dynamicmethod>
- `ILGenerator` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.ilgenerator>

---

# 5. Full Assembly Generation

## 5.1 The builder hierarchy

```text
  AssemblyBuilder   one logical assembly, with a name and a version
      │
      └── ModuleBuilder    one physical container of types (normally exactly one)
              │
              └── TypeBuilder     one class, struct, interface or enum
                      │
                      ├── FieldBuilder        a field
                      ├── ConstructorBuilder  a constructor
                      ├── MethodBuilder       a method (holds an ILGenerator)
                      └── PropertyBuilder     a property (needs get/set methods)
```

This is the Part 2 metadata hierarchy, built in reverse. Part 2 **read**
Assembly → Type → Member; here you **write** the same structure.

**One rule governs it:** `TypeBuilder.CreateType()` "bakes" the type. Before that call
the type does not exist and cannot be instantiated. After it, nothing more can be added.

## 5.2 What .NET 10 actually supports — verified

This is the part where older documentation will mislead you. Verified by running
example 06:

```text
AssemblyBuilderAccess members that exist on this runtime:
  AssemblyBuilderAccess.Run
  AssemblyBuilderAccess.RunAndCollect

Does the runtime AssemblyBuilder have a Save(string) method? no
Concrete runtime builder type: RuntimeAssemblyBuilder
AssemblyBuilder is declared in: System.Private.CoreLib
```

> **`AssemblyBuilderAccess` on .NET 10 has only `Run` and `RunAndCollect`.**
> `Save` and `RunAndSave` were **.NET Framework** members. They do not exist.
> Code like this does **not compile** on modern .NET:
>
> ```csharp
> // .NET Framework only — DOES NOT COMPILE on .NET Core / .NET 5+
> var ab = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndSave);
> ab.Save("Output.dll");
> ```

| Member | Meaning |
|---|---|
| `Run` | in-memory, executable, **not** collectible |
| `RunAndCollect` | in-memory, executable, **collectible** (can be unloaded) |

Use `RunAndCollect` if the generated code's lifetime is bounded — otherwise a
`Run` assembly lives for the life of the process. Note that **caching a generated
`Type` keeps its whole load context alive**, exactly as in Part 2 §9.6.

## 5.3 Building a complete type, stage by stage

Goal — generate the equivalent of this C#, without a compiler:

```csharp
public class Scaler : IScaler
{
    private readonly int _factor;
    public Scaler(int factor) { _factor = factor; }
    public int Factor => _factor;
    public int Scale(int value) => _factor * value;
}
```

### Stage 1 — the assembly

```csharp
var assemblyName = new AssemblyName("Metaprogramming.Generated") { Version = new Version(1, 0, 0, 0) };
AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
```

```text
Stage 1  AssemblyBuilder.DefineDynamicAssembly("Metaprogramming.Generated", Run)
         IsDynamic = True, Location = "" (empty: it is not a file)
```

### Stage 2 — the module

```csharp
ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
```

### Stage 3 — the type, declaring the interface

```csharp
TypeBuilder typeBuilder = moduleBuilder.DefineType(
    name: "Metaprogramming.Generated.Scaler",
    attr: TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
    parent: typeof(object));

typeBuilder.AddInterfaceImplementation(typeof(IScaler));
```

`AddInterfaceImplementation` is the key design move — see §6.

### Stage 4 — the field

```csharp
FieldBuilder factorField = typeBuilder.DefineField(
    "_factor", typeof(int), FieldAttributes.Private | FieldAttributes.InitOnly);
```

`FieldAttributes.InitOnly` **is** `readonly`.

### Stage 5 — the constructor

```csharp
ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
    MethodAttributes.Public | MethodAttributes.HideBySig
        | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
    CallingConventions.Standard,
    [typeof(int)]);

constructorBuilder.DefineParameter(1, ParameterAttributes.None, "factor");

ILGenerator ctorIl = constructorBuilder.GetILGenerator();
ConstructorInfo objectCtor = typeof(object).GetConstructor(Type.EmptyTypes)!;

ctorIl.Emit(OpCodes.Ldarg_0);              // [ this ]
ctorIl.Emit(OpCodes.Call, objectCtor);     // [ ]          base.ctor()
ctorIl.Emit(OpCodes.Ldarg_0);              // [ this ]
ctorIl.Emit(OpCodes.Ldarg_1);              // [ this, factor ]
ctorIl.Emit(OpCodes.Stfld, factorField);   // [ ]          this._factor = factor
ctorIl.Emit(OpCodes.Ret);                  // [ ]
```

Two things that bite:

- **Every constructor must chain to a base constructor.** Omitting
  `call object::.ctor()` produces a type that fails in confusing ways. C# does this for
  you invisibly; here it is your job.
- **`ldarg.0` is `this`.** The `factor` parameter is `ldarg.1`. Contrast with §4.3,
  where `ldarg.0` was the first argument because that method was static.

`SpecialName | RTSpecialName` are required for constructors — they tell the runtime this
is a constructor, not a method called `.ctor`.

### Stage 6 — the interface method

```csharp
MethodBuilder scaleMethod = typeBuilder.DefineMethod(
    "Scale",
    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final
        | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
    typeof(int),
    [typeof(int)]);

scaleMethod.DefineParameter(1, ParameterAttributes.None, "value");

ILGenerator scaleIl = scaleMethod.GetILGenerator();
scaleIl.Emit(OpCodes.Ldarg_0);              // [ this ]
scaleIl.Emit(OpCodes.Ldfld, factorField);   // [ _factor ]
scaleIl.Emit(OpCodes.Ldarg_1);              // [ _factor, value ]
scaleIl.Emit(OpCodes.Mul);                  // [ _factor*value ]
scaleIl.Emit(OpCodes.Ret);                  // [ ]

typeBuilder.DefineMethodOverride(scaleMethod, typeof(IScaler).GetMethod(nameof(IScaler.Scale))!);
```

An interface implementation **must be `Virtual`**. `DefineMethodOverride` binds your
method to the interface slot explicitly — the runtime would usually infer it from a
matching name and signature, but being explicit is safer and is *required* when the
names differ.

### Stage 7 — the property

```csharp
MethodBuilder factorGetter = typeBuilder.DefineMethod(
    "get_Factor",
    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
    typeof(int), Type.EmptyTypes);

ILGenerator getterIl = factorGetter.GetILGenerator();
getterIl.Emit(OpCodes.Ldarg_0);
getterIl.Emit(OpCodes.Ldfld, factorField);
getterIl.Emit(OpCodes.Ret);

PropertyBuilder factorProperty = typeBuilder.DefineProperty(
    "Factor", PropertyAttributes.HasDefault, typeof(int), null);
factorProperty.SetGetMethod(factorGetter);
```

Exactly as Part 2 §2.4 said: **a property is metadata binding a name to accessor
methods.** You write the method; the property just points at it.

### Stage 8 — bake it

```csharp
Type created = typeBuilder.CreateType();
```

```text
Stage 8  CreateType()  ->  Metaprogramming.Generated.Scaler
```

## 5.4 Saving a generated assembly on .NET 10 — `PersistedAssemblyBuilder`

Saving **is** supported, through a different class. Verified end to end:

```csharp
var persisted = new PersistedAssemblyBuilder(
    new AssemblyName("Metaprogramming.Saved") { Version = new Version(1, 0, 0, 0) },
    coreAssembly: typeof(object).Assembly);

ModuleBuilder module = persisted.DefineDynamicModule("Metaprogramming.Saved.dll");
TypeBuilder typeBuilder = module.DefineType("Metaprogramming.Saved.MathHelpers",
    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed);

MethodBuilder add = typeBuilder.DefineMethod(
    "Add", MethodAttributes.Public | MethodAttributes.Static, typeof(int), [typeof(int), typeof(int)]);
ILGenerator addIl = add.GetILGenerator();
addIl.Emit(OpCodes.Ldarg_0);
addIl.Emit(OpCodes.Ldarg_1);
addIl.Emit(OpCodes.Add);
addIl.Emit(OpCodes.Ret);

typeBuilder.CreateType();
persisted.Save(outputPath);
```

Verified:

```text
Wrote a real assembly to disk:
  path  : ...\06-ReflectionEmit\bin\Debug\net10.0\Metaprogramming.Saved.dll
  exists: True
  bytes : 2048

Loaded that file back in and invoked it:
  assembly  : Metaprogramming.Saved v1.0.0.0
  IsDynamic : False  (false - it came from a file)
  Add(20,22)= 42
  Square(9) = 81
```

A real 2 KB DLL, loaded with `Assembly.LoadFrom` and invoked.

**Details that matter:**

- It lives in the `System.Reflection.Emit` assembly, part of the **shared framework** —
  **no package reference needed** on `net10.0`.
- `Save` overloads: `Save(string)` and `Save(Stream)`. There is also
  `GenerateMetadata(...)` for building a PE file yourself (to add a strong name or
  debug directory).
- **It is for WRITING, not running.** Do not expect to execute its types directly;
  save, then load the file — or use the `Run` builder instead.
- `Save` needs complete IL: call `CreateType()` on **every** `TypeBuilder` first.
- Note `TypeAttributes.Abstract | Sealed` — that pair is how a **`static class`** is
  represented in metadata.

**Why persistence is worth knowing about** — it turns IL debugging from guesswork into
inspection:

1. Open the result in **ILSpy** or `ildasm` and **read the IL you emitted**.
2. Run **ILVerify** over it to catch invalid IL properly, instead of waiting for
   `InvalidProgramException` (§8).
3. Build-time tools can emit an assembly for later consumption.

## 5.5 Documentation

- `AssemblyBuilder` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder>
- `AssemblyBuilderAccess` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilderaccess>
- `PersistedAssemblyBuilder` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.persistedassemblybuilder>
- `TypeBuilder` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.typebuilder>
- `MethodBuilder` — <https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.methodbuilder>

---

# 6. Using the Generated Type — The Pattern That Makes This Practical

## 6.1 Two ways to call it

```csharp
object? instance = Activator.CreateInstance(generated, 7);

// (a) Reflectively — works, but slow per call.
MethodInfo scale = generated.GetMethod("Scale")!;
scale.Invoke(instance, [6]);

// (b) Through the interface — ordinary, fast interface dispatch.
var scaler = (IScaler)instance!;
scaler.Scale(6);
```

Verified:

```text
Activator.CreateInstance(generated, 7) -> Metaprogramming.Generated.Scaler

Reflective invocation:
  Scale(6)  = 42
  Scale(10) = 70
  Factor    = 7

Through the IScaler interface - no reflection at the call site:
  scaler.Scale(6)  = 42
  scaler.Scale(10) = 70

Each instance keeps its own _factor, as a real field should:
  new Scaler(2).Scale(5) = 10
  new Scaler(3).Scale(5) = 15
  new Scaler(100).Scale(5) = 500
```

## 6.2 The pattern

> **Generate a type that implements an interface you already know, then talk to it
> through that interface.**
> Reflection is used **once**, at creation. Never on the hot path.

This is not a trick; it is the design behind every real user of `TypeBuilder`:

| Library | Generates a type implementing | You then use |
|---|---|---|
| Moq, NSubstitute | your interface | your interface |
| Castle DynamicProxy | your interface (or a subclass) | your interface |
| `DispatchProxy` (Part 4 §6) | your interface | your interface |
| EF Core lazy-loading proxies | your entity type | your entity type |

## 6.3 The generated type is indistinguishable from a compiled one

Part 2's reflection code works on it unchanged:

```text
Name            : Metaprogramming.Generated.Scaler
Assembly        : Metaprogramming.Generated (IsDynamic=True)
Module          : <In Memory Module>
BaseType        : Object
Interfaces      : IScaler
IsSealed        : True

Constructors:
  .ctor(Int32 factor)

Methods (declared only):
  Int32 Scale(Int32 value)  [virtual]
  Int32 get_Factor()

Properties:
  Int32 Factor { get; }

Fields (including private):
  private readonly Int32 _factor
```

Generated metadata and compiled metadata are **the same kind of thing**. That is the
closing idea of Part 3: Part 2 read these tables; Part 3 wrote them.

---

# 7. Practical Example — Dynamic Arithmetic

Covered in §4.8. The summary of what distinguishes a dynamic method from a normal C#
method:

| | Normal C# method | `DynamicMethod` |
|---|---|---|
| Exists | from compile time | from when you create it |
| Declaring type | a real type | **none** |
| Source | your `.cs` file | your `Emit` calls |
| Type-checked | by the compiler | not reliably (§8) |
| In metadata | yes, in the assembly | no — it is not in any assembly you can save |
| Collectible | no | **yes** |
| Debuggable | fully | barely |
| Speed once JIT-ed | full | **the same** |
| Can be created from run-time data | no | **yes** — the whole point |

---

# 8. What Happens When the IL Is Wrong

This section matters more than any other in Part 3, because the failure modes are
**asymmetric** and the dangerous one is silent.

## 8.1 Case A — a missing `ret`

```csharp
var missingRet = new DynamicMethod("MissingRet", typeof(int), [typeof(int), typeof(int)]);
ILGenerator il1 = missingRet.GetILGenerator();
il1.Emit(OpCodes.Ldarg_0);
il1.Emit(OpCodes.Ldarg_1);
il1.Emit(OpCodes.Add);
// deliberately NO il1.Emit(OpCodes.Ret);
```

Verified:

```text
Case A: a method that never returns (no 'ret' at the end).
  CreateDelegate succeeded - nothing has been verified yet.
  Calling it...
  InvalidProgramException on FIRST CALL: Common Language Runtime detected an invalid program.
```

Note **when** it failed: not at `Emit`, not at `CreateDelegate`, but on the **first
call** — because that is when the JIT compiles the method. Your bug surfaces far from
the line that caused it.

## 8.2 Case B — a type mismatch, and this one is worse

```csharp
var typeMismatch = new DynamicMethod("TypeMismatch", typeof(int), [typeof(int)]);
ILGenerator il2 = typeMismatch.GetILGenerator();
il2.Emit(OpCodes.Ldarg_0);
il2.Emit(OpCodes.Ldstr, "not a number");   // push a STRING
il2.Emit(OpCodes.Add);                     // add an int and a string reference?
il2.Emit(OpCodes.Ret);
```

Verified:

```text
Case B: a stack TYPE mismatch - 'add' on an int32 and a string reference.
This one is worse than an exception. Watch what actually happens.
  No exception. broken(1) returned 299021057, and again 299021057.
  That number is the string's managed reference added to 1 as an integer.
  It is meaningless, it is not stable across runs, and nothing warned us.
```

**No exception. A plausible-looking number. Nothing warned us.**

> **IL emitted by a fully-trusted `DynamicMethod` is not type-verified.** A mistake can
> silently produce garbage instead of failing. Memory-unsafe IL in this position can
> corrupt process state rather than throw.

That asymmetry is the real reason to be cautious with `Emit`: your safety net is thinner
than the C# compiler's, and in some cases there is no net at all. Compare with
expression trees (Part 4 §8), where `Expression.Add(intNode, stringNode)` throws
**immediately**, at build time of the tree.

## 8.3 How to debug emitted IL in practice

In order of usefulness:

1. **Write the C# first, read its IL.** Compile the method you want in a scratch
   project, open it in ILSpy or sharplab.io, and emit *that*. Never reason from first
   principles.
2. **Track the stack depth as you emit.** Keep a counter, or write the expected stack
   state in a comment on every line (as these examples do). Assert it returns to zero.
3. **Persist and verify.** Generate with `PersistedAssemblyBuilder`, `Save` it, and run
   **ILVerify** or `ildasm` over the file. This is the only way to get proper IL
   validation, and it is the strongest argument for knowing §5.4 exists.
4. **Keep emitted methods tiny.** Put real logic in normal C# and `call` it (§4.5). The
   less IL you write, the less IL can be wrong.
5. **Prefer expression trees.** They are type-checked as you build them.

---

# 9. Limits and Tradeoffs

## 9.1 `DynamicMethod` vs the builders

| | `DynamicMethod` | `AssemblyBuilder`/`TypeBuilder`/`MethodBuilder` |
|---|---|---|
| Creates | one method | a whole **type** |
| Fields, properties, several methods | no | yes |
| Implements an interface | no | **yes** |
| Collectible | yes | only with `RunAndCollect` |
| Savable | no | yes, via `PersistedAssemblyBuilder` |
| Ceremony | ~5 lines | ~60 lines |
| Right for | accessors, converters, mappers, serializer fast paths | proxies, mocks, generated models |

## 9.2 Costs you accept with either

| Cost | Detail |
|---|---|
| **Native AOT** | Unavailable. `IsDynamicCodeSupported` is `false` and these APIs throw `PlatformNotSupportedException`. |
| **Trimming** | The trimmer cannot see what your IL will reference, or types that do not exist yet. Annotate with `DynamicallyAccessedMembers`, or do not trim. |
| **First-use latency** | Generating plus JIT-ing costs real milliseconds. Cache the generated `Type` or delegate; never regenerate per request. |
| **Memory** | A `Run` assembly is **never** collected. A cached generated `Type` keeps its whole context alive. |
| **Debuggability** | No source, limited breakpoints, opaque stack frames. |
| **Maintainability** | IL is write-mostly. Expect the next developer to fear it — and to be right to. |
| **Safety** | §8. Some invalid IL is not caught at all. |

## 9.3 The decision order

Work down this list. Stop at the first one that works.

```text
1. Plain C#, generics, or a cached delegate from MethodInfo.CreateDelegate
     -> covers the large majority of cases, at emit-level speed (§2.3)

2. Expression trees  (Part 4 §8)
     -> same speed class, readable, type-checked, printable
     -> can be BUILT and READ under AOT; only Compile() needs the JIT

3. DispatchProxy  (Part 4 §6)
     -> if you only need to intercept an INTERFACE

4. A Roslyn source generator
     -> if the shape is known at BUILD time: zero runtime cost, AOT-safe

5. Reflection.Emit
     -> only when the shape is known ONLY at run time AND you need a TYPE
```

Most people who reach for step 5 needed step 1 or 2.

---

## Part 3 summary

1. Runtime code generation creates new IL while the process runs — the write half of
   metaprogramming.
2. The one good reason is **performance when the shape is only known at run time**: pay
   reflection once, then call generated code. The other is needing a **type** that did
   not exist.
3. It requires `RuntimeFeature.IsDynamicCodeSupported`, which is **`false` under Native
   AOT**.
4. IL is a **stack machine**. Balance the stack; match the operand types.
5. `ldarg.0` is the first argument in a **static** method and **`this`** in an instance
   method. This is the most common beginner bug.
6. `DynamicMethod` → one method, cheap, collectible, not savable.
   `TypeBuilder` → whole types; call `CreateType()` to bake.
7. On .NET 10, `AssemblyBuilderAccess` has only **`Run`** and **`RunAndCollect`**.
   Saving is done with **`PersistedAssemblyBuilder`** — verified here by writing,
   reloading and invoking a real DLL.
8. **Invalid IL may not be caught.** A missing `ret` throws on first call; a type
   mismatch silently returned garbage.
9. The practical pattern: **generate a type implementing a known interface, then use the
   interface.**
10. Measured: a cached delegate is as fast as emitted IL. **Try `CreateDelegate` before
    writing any IL.**

**Next:** [Part 4 — Real-World Applications](Part-4-Real-World-Applications.md)
