# Syllabus Coverage Plan

Every syllabus topic mapped to a slide, a study-guide section, and a runnable project.
"—" means the topic is conceptual and has no separate executable.

## Part 1 — Understanding Metaprogramming (6 min)

| # | Topic | Slide | Study guide | Code |
|---|---|---|---|---|
| 1.1 | Normal programming vs metaprogramming | 3, 4 | Part-1 §1 | `01-Basics` |
| 1.2 | Why we need metaprogramming (problem first) | 5 | Part-1 §2 | `01-Basics` |
| 1.3 | Developer benefits: automation, boilerplate, maintainability, extensibility | 6 | Part-1 §3 | `01-Basics` |
| 1.4 | When metaprogramming adds complexity | 6 | Part-1 §3.5 | — |
| 1.5 | Compile-time vs runtime metaprogramming; Roslyn source generators, Reflection, Reflection.Emit | 6 | Part-1 §4 | `09-Serialization` (source-gen vs reflection) |
| 1.6 | Implicit vs explicit metadata; metadata tables; attributes as declarative metadata | 7 | Part-1 §5 | `01-Basics` |

## Part 2 — Runtime Introspection (10 min)

| # | Topic | Slide | Study guide | Code |
|---|---|---|---|---|
| 2.1 | What is runtime introspection | 8 | Part-2 §1 | `02-Reflection` |
| 2.2 | .NET metadata: assemblies, modules, types, methods, fields, properties, ctors, custom attributes, manifest | 9 | Part-2 §2 | `02-Reflection` |
| 2.3 | Reflection API: `Type`, `Assembly`, `MethodInfo`, `PropertyInfo`, `ConstructorInfo`, `FieldInfo`, `BindingFlags` | 10 | Part-2 §3 | `02-Reflection` |
| 2.4 | Assembly & type discovery: `GetExecutingAssembly`, `GetTypes`, `typeof`, `GetType()`, `Type.GetType` + null reasons | 11 | Part-2 §4 | `02-Reflection` |
| 2.5 | Inspecting members + public vs non-public | 12 | Part-2 §5 | `02-Reflection` |
| 2.6 | `Activator.CreateInstance`, ctor selection, args, exceptions, vs `new` | 13 | Part-2 §6 | `02-Reflection` |
| 2.7 | Attributes: `AttributeUsage`, `AttributeTargets`, `AllowMultiple`, `Inherited`, `GetCustomAttributes` | 14 | Part-2 §7 | `03-Attributes` |
| 2.8 | Plugin discovery; trusted vs untrusted assemblies; ALC is not a sandbox | 15 | Part-2 §8 | `04-Plugin-Discovery` |

## Part 3 — Runtime Code Generation (12 min)

| # | Topic | Slide | Study guide | Code |
|---|---|---|---|---|
| 3.1 | What is runtime code generation; runtime support requirements | 16 | Part-3 §1 | `05-DynamicMethod` |
| 3.2 | Reflection vs Reflection.Emit comparison | 17 | Part-3 §2 | `05-DynamicMethod` (benchmark) |
| 3.3 | CIL/MSIL, managed assemblies, JIT, machine code, AOT exception | 18 | Part-3 §3 | — |
| 3.4 | IL instructions `ldarg.0/1`, `ldc.i4`, `add`, `mul`, `call`, `newobj`, `ret` + evaluation stack | 19 | Part-3 §3.4 | `05-DynamicMethod` |
| 3.5 | `DynamicMethod`, `ILGenerator`, `GetILGenerator`, `Emit`, `OpCodes`, `CreateDelegate` | 20, 21 | Part-3 §4 | `05-DynamicMethod` |
| 3.6 | `AssemblyBuilder`, `ModuleBuilder`, `TypeBuilder`, `MethodBuilder`, `CreateType`, `AssemblyBuilderAccess.Run` | 22 | Part-3 §5 | `06-ReflectionEmit` |
| 3.7 | Dynamic assembly **persistence** in .NET 10 (`PersistedAssemblyBuilder`) — verified | 22 | Part-3 §5.4 | `06-ReflectionEmit` |
| 3.8 | Generating types + methods at runtime, end to end | 23 | Part-3 §6 | `06-ReflectionEmit` |
| 3.9 | Dynamic arithmetic (add/multiply), delegates, tradeoffs & limits | 21 | Part-3 §7 | `05-DynamicMethod` |

## Part 4 — Real-World Applications (9 min)

| # | Topic | Slide | Study guide | Code |
|---|---|---|---|---|
| 4.1 | ASP.NET Core MVC controller/action discovery, application parts, startup vs request time | 24 | Part-4 §1 | `10-ASPNetCoreMVC` |
| 4.2 | DI: ctor inspection, resolution, registration, lifetimes, `ActivatorUtilities`; activation strategies differ | 24 | Part-4 §2 | `07-DependencyInjection` |
| 4.3 | EF Core 10: entity discovery, `DbContext`, `DbSet`, `ModelBuilder`, conventions, attributes, fluent API; model metadata vs CLR metadata | 25 | Part-4 §3 | `11-EntityFrameworkCore` |
| 4.4 | Serialization: `System.Text.Json`, attributes, reflection vs source-generated | 25 | Part-4 §4 | `09-Serialization` |
| 4.5 | Plugin architecture: `AssemblyLoadContext`, unloading limits, versioning, security | 26 | Part-4 §5 | `04-Plugin-Discovery` |
| 4.6 | Dynamic proxies: `DispatchProxy`, interception, interface-only limits | 26 | Part-4 §6 | `12-DynamicProxies` |
| 4.7 | Cross-cutting concerns: logging, caching, authorization, AOP; middleware vs proxies vs filters | 27 | Part-4 §7 | `12-DynamicProxies` |
| 4.8 | Expression trees: `Expression<TDelegate>`, nodes, `Compile`, `IQueryable`, LINQ translation | 27 | Part-4 §8 | `08-ExpressionTrees` |

## Required diagrams -> asset files

| Diagram | File (Assets/Diagrams) | Slide |
|---|---|---|
| Normal vs metaprogramming | `01-normal-vs-metaprogramming.svg/.png` | 4 |
| Compile-time vs runtime metaprogramming | `02-compiletime-vs-runtime.*` | 6 |
| .NET assembly metadata structure | `03-assembly-metadata.*` | 9 |
| Reflection API workflow | `04-reflection-workflow.*` | 10 |
| Assembly -> Types -> Members | `05-assembly-types-members.*` | 11 |
| `Activator.CreateInstance` workflow | `06-activator-workflow.*` | 13 |
| Custom attribute inspection | `07-attribute-inspection.*` | 14 |
| Plugin discovery architecture | `08-plugin-discovery.*` | 15 |
| Reflection vs Reflection.Emit | `09-reflection-vs-emit.*` | 17 |
| C# -> IL -> JIT -> machine code | `10-compilation-pipeline.*` | 18 |
| IL evaluation stack | `11-evaluation-stack.*` | 19 |
| DynamicMethod generation pipeline | `12-dynamicmethod-pipeline.*` | 20 |
| Runtime assembly/type generation | `13-runtime-type-generation.*` | 22 |
| DI resolution | `14-di-resolution.*` | 24 |
| MVC action discovery | `15-mvc-action-discovery.*` | 24 |
| Expression tree structure | `16-expression-tree.*` | 27 |
| Dynamic proxy interception | `17-dynamic-proxy.*` | 26 |

## Timing budget (37 min)

| Section | Slides | Minutes | Cumulative |
|---|---|---|---|
| Opening | 1–2 | 1.5 | 1.5 |
| Part 1 | 3–7 | 6.0 | 7.5 |
| Part 2 | 8–15 | 10.0 | 17.5 |
| Part 3 | 16–23 | 12.0 | 29.5 |
| Part 4 | 24–27 | 9.0 | 38.5 |
| Closing | 28 | — | — |

Opening + closing are absorbed inside the four part budgets (see
`Presenter-Guide/37-Minute-Timeline.md` for the reconciled per-slide plan that totals 37:00).
