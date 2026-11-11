# Code Examples — Metaprogramming in .NET 10

Fourteen projects in one solution: **twelve runnable examples** (eleven console apps and
one web app), each printing an explained walkthrough of its topic, plus two support
libraries used by the plugin-discovery example.

Every example was built and executed on the machine that produced this package.
The captured console output of each one is in [`expected-output/`](expected-output/)
so you can diff your run against a known-good one.

---

## Prerequisites

| Requirement | Version used here |
|---|---|
| .NET SDK | **10.0.400** |
| .NET runtime | 10.0.11 |
| Database | none — example 11 uses SQLite **in-memory** |
| Packages | restored from nuget.org on first build |

Check your SDK:

```bash
dotnet --version        # expect 10.x
```

Everything targets `net10.0`. No Visual Studio required; the CLI is enough.

---

## Build everything

**macOS / Linux / Windows (bash, zsh):**

```bash
cd Code-Examples
dotnet restore Metaprogramming.slnx
dotnet build Metaprogramming.slnx -c Release
```

**Windows PowerShell** — identical commands, but note that `&&` is not a valid
PowerShell operator in Windows PowerShell 5.1, so run them on separate lines or
chain with `;`:

```powershell
Set-Location Code-Examples
dotnet restore Metaprogramming.slnx
dotnet build Metaprogramming.slnx -c Release
```

Expected result: `Build succeeded. 0 Warning(s) 0 Error(s)` for 14 projects (12 runnable examples + 2 libraries).

---

## Run an individual example

Run them in order — each one builds on the previous.

```bash
# Part 1 — Understanding metaprogramming
dotnet run --project 01-Basics

# Part 2 — Runtime introspection
dotnet run --project 02-Reflection
dotnet run --project 03-Attributes
dotnet run --project 04-Plugin-Discovery/PluginDiscovery.Host

# Part 3 — Runtime code generation
dotnet run --project 05-DynamicMethod
dotnet run --project 06-ReflectionEmit

# Part 4 — Real-world applications
dotnet run --project 07-DependencyInjection
dotnet run --project 08-ExpressionTrees
dotnet run --project 09-Serialization
dotnet run --project 11-EntityFrameworkCore
dotnet run --project 12-DynamicProxies
```

PowerShell uses exactly the same `dotnet run --project <path>` syntax. Only the
path separator differs if you type it by hand (`04-Plugin-Discovery\PluginDiscovery.Host`),
and forward slashes work in PowerShell too.

### The web example (10)

It has two modes:

```bash
# Self-test: starts the server, prints the discovery report, calls itself over
# HTTP, prints the responses, then exits. Use this one for a quick check.
dotnet run --project 10-ASPNetCoreMVC -- --selftest

# Server mode: prints the discovery report then keeps serving. Ctrl+C to stop.
dotnet run --project 10-ASPNetCoreMVC
```

In server mode it listens on <http://127.0.0.1:5199>:

| URL | Shows |
|---|---|
| `/api/reports` | a normal action |
| `/api/reports/2` | `{id:int}` route binding |
| `/api/reports/not-an-int` | 404 from the `:int` route **constraint** |
| `/api/reports/search?term=sales&take=2` | `[FromQuery]` binding + a default value |
| `/api/metadata/actions` | **the app describing its own routing table** |

### Benchmarks

Examples 05 and 09 measure things. Run them in **Release** or the numbers are
misleading:

```bash
dotnet run -c Release --project 05-DynamicMethod
dotnet run -c Release --project 09-Serialization
```

These use `Stopwatch` in a loop, which is good enough to show an order of
magnitude but is *not* a rigorous benchmark. For real measurements use
[BenchmarkDotNet](https://benchmarkdotnet.org/).

---

## What each project demonstrates

| # | Project | Key APIs | The one thing to take away |
|---|---|---|---|
| 01 | `01-Basics` | `GetProperties`, `typeof`, `Type.GetType` | One reflective routine replaces N hand-written ones — and gives up compile-time safety to do it |
| 02 | `02-Reflection` | `Assembly`, `Type`, `MethodInfo`, `BindingFlags`, `Activator` | Every `Activator` failure is a *runtime* failure |
| 03 | `03-Attributes` | `AttributeUsage`, `GetCustomAttributes`, `CustomAttributeData` | Reading attributes *constructs* them; `GetCustomAttributesData` does not |
| 04 | `04-Plugin-Discovery` (3 projects) | `AssemblyLoadContext`, `AssemblyDependencyResolver` | ALC isolates **type identity**, not **privileges** — it is not a sandbox |
| 05 | `05-DynamicMethod` | `DynamicMethod`, `ILGenerator`, `OpCodes`, `CreateDelegate` | A cached delegate is usually as fast as emitted IL, with none of the risk |
| 06 | `06-ReflectionEmit` | `AssemblyBuilder`, `TypeBuilder`, `MethodBuilder`, `PersistedAssemblyBuilder` | Generate a type that implements a **known interface**, then never reflect again |
| 07 | `07-DependencyInjection` | `ServiceCollection`, lifetimes, `ActivatorUtilities` | A constructor signature *is* a machine-readable dependency list |
| 08 | `08-ExpressionTrees` | `Expression<T>`, `ExpressionVisitor`, `Compile`, `IQueryable` | "Cannot be expressed as a tree" (compile error) ≠ "cannot be translated to SQL" (runtime error) |
| 09 | `09-Serialization` | `System.Text.Json`, `JsonSerializerContext` | Same output, two mechanisms — the difference is *when* the metaprogramming ran |
| 10 | `10-ASPNetCoreMVC` | `ApplicationPartManager`, `IActionDescriptorCollectionProvider` | Reflection happens at **startup**; requests are served from a cached table |
| 11 | `11-EntityFrameworkCore` | `DbContext`, `IModel`, `ToQueryString` | The EF model is a *separate, richer* model built by reading CLR metadata |
| 12 | `12-DynamicProxies` | `DispatchProxy` | Metaprogramming gives you the **seam**, not the **policy** |

---

## Notable findings verified on this machine

These were checked by running code, not recalled from documentation. They are the
points most likely to be wrong in older tutorials.

1. **`AssemblyBuilderAccess` on .NET 10 has only `Run` and `RunAndCollect`.**
   `Save` and `RunAndSave` were .NET Framework members and do not exist. Code using
   them does not compile. (Example 06 prints the live enum members.)

2. **Saving a generated assembly *is* supported — via a different class.**
   `System.Reflection.Emit.PersistedAssemblyBuilder` (in the shared framework, no
   package needed) emitted a 2 KB DLL that was then loaded with `Assembly.LoadFrom`
   and invoked successfully. (Example 06, section 6.)

3. **Invalid IL is not always caught.** A missing `ret` throws
   `InvalidProgramException` on first call — but `add` on an `int` and a `string`
   produced **no error at all** and silently returned a garbage number derived from a
   managed reference. Fully-trusted `DynamicMethod` IL is not type-verified.
   (Example 05, section 8.)

4. **`Microsoft.Extensions.DependencyInjection` uses `DynamicServiceProviderEngine`**
   on this runtime — read out of the provider's private field. So "DI uses reflection"
   is safe to say; "DI uses Reflection.Emit" is not, because the engine is chosen at
   run time and falls back to a reflection-only engine when dynamic code is
   unavailable. (Example 07, section 8.)

5. **`JsonSerializerOptions` reuse matters less than it used to, but varying
   settings per call still costs ~7x.** Equivalent options instances share a
   process-wide metadata cache; genuinely different ones cannot.
   (Example 09, section 3 — measured, three ways.)

6. **A `DispatchProxy` base class must not be `sealed`** — the generated proxy
   derives from it, so sealing it throws `ArgumentException` at run time.
   .NET 10 also offers a non-generic `DispatchProxy.Create(Type, Type)`.
   (Example 12, section 8.)

7. **SQLite renders `string.Contains` as `instr(...) > 0`, not `LIKE`.**
   The *provider* chooses the translation, so read the SQL your provider actually
   emits with `ToQueryString()`. (Example 11, section 5B.)

---

## Platform limitations that apply to this code

| Constraint | Effect |
|---|---|
| **Native AOT** | Examples 05, 06 and 12 **cannot work**. `RuntimeFeature.IsDynamicCodeSupported` is `false` and `Reflection.Emit` / `DispatchProxy` throw `PlatformNotSupportedException`. |
| **Trimming** | Examples 01–04 and reflection-mode JSON risk having members trimmed away. Source-generated JSON (09) is trimming-safe. |
| **Mono interpreter** (iOS, some WASM) | `IsDynamicCodeCompiled` is `false`: emitted code runs *interpreted*, so the speed benefit disappears. |
| **`InvariantGlobalization=true`** | Set in `Directory.Build.props` for deterministic output. Dates print as `MM/dd/yyyy`. |

None of the examples opt into AOT or trimming, so all 14 projects build and run
as supplied.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `NETSDK1045: The current .NET SDK does not support targeting .NET 10.0` | SDK older than 10.0. Install the .NET 10 SDK. |
| `NU1202: Package ... is not compatible with net10.0` | You bumped a package to an 11.x version. The newest EF Core for `net10.0` is **10.0.15**; 11.x requires `net11.0`. |
| Example 04 prints "No plugins folder" | Run `dotnet build` on `PluginDiscovery.Host` first — a post-build target copies the plugin DLL into `bin/.../plugins`. |
| Example 04's unload reports `alive = True` after 10 attempts | A debugger is attached, or you ran it in a context that roots the locals. Run without the debugger. |
| Example 10 fails with "address already in use" | Port 5199 is taken. Change `UseUrls` in `10-ASPNetCoreMVC/Program.cs`. |
| Benchmarks show implausible numbers | You are in `Debug`. Add `-c Release`. |
| Timings differ from `expected-output/` | Expected. Only the *ratios* are the lesson; absolute milliseconds are machine-specific. |

More detail: [`../Presenter-Guide/Troubleshooting.md`](../Presenter-Guide/Troubleshooting.md).
